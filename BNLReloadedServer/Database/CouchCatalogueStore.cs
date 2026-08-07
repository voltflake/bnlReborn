using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BNLReloadedServer.BaseTypes;
using BNLReloadedServer.ProtocolHelpers;
using CouchDB.Driver;

namespace BNLReloadedServer.Database;

/// <summary>
/// The catalogue's only source of truth: every card is pulled from CouchDB over HTTP.
/// <see cref="Load"/> also refreshes <see cref="CatalogueBlob"/>, the serialized form
/// handed to clients, so a reload is immediately visible to anyone logging in.
/// </summary>
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

    /// <summary>
    /// Serializes the in-memory catalogue to the same JSON shape CouchDB stores. Currently
    /// unwired — kept so an export can be hung off the control panel, which already holds
    /// both this store and the ServerCatalogue needed to feed it.
    /// </summary>
    public string ToJson(IEnumerable<Card> cards) =>
        JsonSerializer.Serialize(cards, serializerOptions).Replace("\\u00A0", "\u00A0");

    /// <summary>Writes <see cref="ToJson"/> to <c>Cache/&lt;export_cdb_name&gt;</c>.</summary>
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

        // Every card's Key is the CRC32 of its id, derived rather than stored, so it has to be
        // recomputed for the whole catalogue on every load.
        foreach (var card in cards)
        {
            card.Key = Catalogue.Key(card.Id ?? string.Empty);
        }

        CatalogueBlob.Set(Serialize(cards));

        return cards;
    }

    private static byte[] Serialize(List<Card> cards)
    {
        using var memStream = new MemoryStream();
        using var writer = new BinaryWriter(memStream);
        writer.Write((byte)0);
        writer.WriteList(cards, Card.WriteVariant);
        writer.Flush();
        // ToArray, not GetBuffer — GetBuffer hands back the whole capacity and would pad the
        // payload with megabytes of zeroes for every client to inflate and walk past.
        using var zipped = memStream.ToArray().Zip(0);
        return zipped.ToArray();
    }
}
