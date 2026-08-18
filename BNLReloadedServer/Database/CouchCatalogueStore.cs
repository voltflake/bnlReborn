using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;
using BNLReloadedServer.BaseTypes;
using CouchDB.Driver;

namespace BNLReloadedServer.Database;

public class CouchCatalogueStore(
    CouchClient fromDb,
    string dbName,
    string toPath,
    JsonSerializerOptions serializerOptions)
{
    private static readonly HttpClient _httpClient = new();

    private class AllDocsResponse
    {
        [JsonPropertyName("rows")]
        public List<DocRow> Rows { get; set; } = [];
    }

    private class DocRow
    {
        [JsonPropertyName("doc")]
        public JsonElement Doc { get; set; }
    }

    public string ToJson(IEnumerable<Card> cards) =>
        JsonSerializer.Serialize(cards, serializerOptions).Replace("\\u00A0", "\u00A0");

    public void Store(IEnumerable<Card> cards) => File.WriteAllText(toPath, ToJson(cards));

    public List<Card> Load()
    {
        var url = $"{fromDb.Endpoint.OriginalString.TrimEnd('/')}/{dbName}/_all_docs?include_docs=true";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        var creds = Databases.ConfigDatabase.CouchDbCredentials();
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.ASCII.GetBytes($"{creds.Username}:{creds.Password}")));

        var response = _httpClient.SendAsync(request).Result;
        response.EnsureSuccessStatusCode();
        var allDocs = response.Content.ReadFromJsonAsync<AllDocsResponse>().Result!;

        List<Card> cards = [];
        foreach (var row in allDocs.Rows)
        {
            if (!row.Doc.TryGetProperty("category", out _)) continue;
            var card = JsonSerializer.Deserialize<Card>(row.Doc.GetRawText(), serializerOptions);
            if (card != null) cards.Add(card);
        }

        foreach (var card in cards)
        {
            card.Key = Catalogue.Key(card.Id ?? string.Empty);
        }

        var problems = CatalogueValidator.Validate(cards);
        if (problems.Count > 0)
        {
            throw new InvalidOperationException(
                $"Rejected catalogue fetched from '{dbName}' — {string.Join("; ", problems.Take(10))}" +
                (problems.Count > 10 ? $" (+{problems.Count - 10} more)" : string.Empty));
        }

        MapPoolReconciler.Reconcile(cards);

        return cards;
    }

    public Card? LoadCard(string documentId)
    {
        var escapedId = Uri.EscapeDataString(documentId);
        var url = $"{fromDb.Endpoint.OriginalString.TrimEnd('/')}/{dbName}/{escapedId}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        var creds = Databases.ConfigDatabase.CouchDbCredentials();
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.ASCII.GetBytes($"{creds.Username}:{creds.Password}")));

        using var response = _httpClient.SendAsync(request).Result;
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();

        using var stream = response.Content.ReadAsStream();
        using var document = JsonDocument.Parse(stream);
        if (!document.RootElement.TryGetProperty("category", out _)) return null;

        var card = JsonSerializer.Deserialize<Card>(document.RootElement.GetRawText(), serializerOptions);
        if (card == null) return null;
        card.Key = Catalogue.Key(card.Id ?? string.Empty);
        return card;
    }

    public async Task UpdateMapPoolAsync(string pool, IReadOnlyList<string> mapIds)
    {
        if (pool is not ("friendly" or "ranked" or "custom"))
            throw new ArgumentException($"Map pool '{pool}' is not editable", nameof(pool));

        var url = $"{fromDb.Endpoint.OriginalString.TrimEnd('/')}/{dbName}/map_list";
        using var get = CreateRequest(HttpMethod.Get, url);
        using var current = await _httpClient.SendAsync(get);
        current.EnsureSuccessStatusCode();

        var document = JsonNode.Parse(await current.Content.ReadAsStringAsync())?.AsObject()
            ?? throw new InvalidOperationException("CouchDB returned an invalid map_list document");
        document[pool] = new JsonArray(mapIds.Select(id => (JsonNode?)id).ToArray());

        using var put = CreateRequest(HttpMethod.Put, url);
        put.Content = new StringContent(document.ToJsonString(serializerOptions), Encoding.UTF8, "application/json");
        using var response = await _httpClient.SendAsync(put);
        response.EnsureSuccessStatusCode();
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        var creds = Databases.ConfigDatabase.CouchDbCredentials();
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.ASCII.GetBytes($"{creds.Username}:{creds.Password}")));
        return request;
    }
}
