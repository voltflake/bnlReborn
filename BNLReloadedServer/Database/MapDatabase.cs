using System.Text.Json;
using BNLReloadedServer.BaseTypes;
using BNLReloadedServer.ProtocolHelpers;

namespace BNLReloadedServer.Database;

/// <summary>
/// Maps live on disk as <c>Maps/&lt;map_id&gt;.bnlbin</c> — zlib'd UTF-8 JSON of a
/// <see cref="MapCustomData"/>, the same container the game client reads and writes.
/// Only <see cref="MapCustomData.Map"/> is ever read back; the envelope's name, description
/// and publish flags exist so the file stays a valid client map, not as a second source of
/// truth. Card metadata comes from the catalogue.
/// </summary>
public class MapDatabase : IMapDatabase
{
    private const string MapExtension = ".bnlbin";

    private static string MapPath { get; } = Path.Combine(Databases.BaseFolderPath, "Maps");

    private readonly Dictionary<Key, string> _maps = new();

    private void ScanMaps()
    {
        _maps.Clear();
        if (!Directory.Exists(MapPath)) return;

        foreach (var file in Directory.GetFiles(MapPath, $"*{MapExtension}", SearchOption.TopDirectoryOnly))
        {
            _maps[new Key(Path.GetFileNameWithoutExtension(file))] = file;
        }
    }

    private Dictionary<Key, string> Maps
    {
        get
        {
            if (_maps.Count == 0) ScanMaps();
            return _maps;
        }
    }

    /// <summary>Map ids, i.e. bare file names — <see cref="Key"/> only carries the hash.</summary>
    public List<string> GetMapIds() => Maps.Values.Select(Path.GetFileNameWithoutExtension).OfType<string>().ToList();

    private MapCustomData? LoadCustomData(Key key)
    {
        if (!Maps.TryGetValue(key, out var mapFile) || !File.Exists(mapFile)) return null;

        try
        {
            var json = File.ReadAllBytes(mapFile).UnZip();
            return JsonSerializer.Deserialize<MapCustomData>(json, JsonHelper.DefaultSerializerSettings);
        }
        catch (Exception e)
        {
            Console.WriteLine($"[MapDatabase] Failed to read '{mapFile}': {e.Message}");
            return null;
        }
    }

    public MapData? LoadMapData(Key key) => LoadCustomData(key)?.Map;

    public void SaveMap(string key, MapData mapData)
    {
        Directory.CreateDirectory(MapPath);
        var mapFile = Path.Combine(MapPath, key + MapExtension);

        // Preserve whatever envelope the map already had — an editor save must not clobber
        // the map's name and description just because it only carries block data.
        var customData = LoadCustomData(new Key(key)) ?? new MapCustomData
        {
            Name = key,
            Description = string.Empty,
            MapId = key
        };
        customData.Map = mapData;
        customData.MapId = key; // the file name is the map's identity, so never let a stale id ride along

        var json = JsonSerializer.SerializeToUtf8Bytes(customData, JsonHelper.DefaultSerializerSettings);
        using (var fs = File.Create(mapFile))
        {
            using var zipped = json.Zip(0);
            zipped.CopyTo(fs);
        }

        _maps[new Key(key)] = mapFile;

        Console.WriteLine($"[MapEditor] Saved map '{key}' to {mapFile}");
    }
}
