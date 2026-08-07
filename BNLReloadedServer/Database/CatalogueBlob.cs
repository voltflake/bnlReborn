using BNLReloadedServer.ProtocolHelpers;

namespace BNLReloadedServer.Database;

/// <summary>
/// The serialized catalogue as the client consumes it: a zlib'd card list plus its CRC32.
/// Rebuilt in memory every time the catalogue is loaded from CouchDB — clients compare the
/// hash at login and only pull the payload when it has moved.
/// </summary>
public static class CatalogueBlob
{
    private static volatile byte[] _data = [];
    private static volatile uint _hash;

    public static byte[] Data => _data;

    public static uint Hash => _hash;

    public static void Set(byte[] data)
    {
        _data = data;
        _hash = Crc32.GetHash(data);
    }
}
