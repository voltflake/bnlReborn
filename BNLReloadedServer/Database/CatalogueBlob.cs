using BNLReloadedServer.BaseTypes;
using BNLReloadedServer.ProtocolHelpers;

namespace BNLReloadedServer.Database;

public static class CatalogueBlob
{
    public sealed record Snapshot(byte[] Data, uint Hash);

    private static volatile Snapshot _current = new([], 0);

    public static Snapshot Current => _current;

    public static void Set(List<Card> cards)
    {
        var data = Serialize(cards);
        _current = new Snapshot(data, Crc32.GetHash(data));
    }

    private static byte[] Serialize(List<Card> cards)
    {
        using var memStream = new MemoryStream();
        using var writer = new BinaryWriter(memStream);
        writer.Write((byte)0);
        writer.WriteList(cards, Card.WriteVariant);
        writer.Flush();
        using var zipped = memStream.ToArray().Zip(0);
        return zipped.ToArray();
    }
}
