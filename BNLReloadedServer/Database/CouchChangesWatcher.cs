using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CouchDB.Driver;
using BNLReloadedServer.Logging;

namespace BNLReloadedServer.Database;

public readonly record struct CouchChange(string DocumentId, bool Deleted);

public class CouchChangesWatcher(string endpoint, string dbName, BasicCredentials credentials, Action<CouchChange> onChanged)
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = Timeout.InfiniteTimeSpan
    };

    public void Start(CancellationToken cancellationToken) => _ = Task.Run(() => RunLoop(cancellationToken), cancellationToken);

    private async Task RunLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await ListenOnce(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                Log.Warn(LogCat.Catalogue, $"CouchDB connection lost ({ex.Message}), retrying in 5s...");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    private async Task ListenOnce(CancellationToken cancellationToken)
    {
        var url = $"{endpoint.TrimEnd('/')}/{dbName}/_changes?feed=continuous&since=now&heartbeat=30000";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.ASCII.GetBytes($"{credentials.Username}:{credentials.Password}")));

        using var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        Log.Info(LogCat.Catalogue, "Listening for catalogue changes...");

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line == null) return;
            if (string.IsNullOrWhiteSpace(line)) continue;

            var change = TryGetChange(line);
            if (change == null)
            {
                Log.Warn(LogCat.Catalogue, "Ignoring a CouchDB change with no document id");
                continue;
            }

            Log.Info(LogCat.Catalogue, change.Value.Deleted
                ? $"Card deleted: {change.Value.DocumentId}"
                : $"Card changed: {change.Value.DocumentId}");

            try
            {
                onChanged(change.Value);
            }
            catch (Exception ex)
            {
                Log.Error(LogCat.Catalogue, "Catalogue change failed, keeping the current catalogue", ex);
            }
        }
    }

    private static CouchChange? TryGetChange(string changeLine)
    {
        try
        {
            using var doc = JsonDocument.Parse(changeLine);
            if (!doc.RootElement.TryGetProperty("id", out var idProp) || idProp.GetString() is not { } id)
                return null;

            var deleted = doc.RootElement.TryGetProperty("deleted", out var deletedProp) &&
                          deletedProp.ValueKind == JsonValueKind.True;
            return new CouchChange(id, deleted);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
