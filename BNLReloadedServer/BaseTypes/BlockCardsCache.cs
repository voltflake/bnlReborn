using BNLReloadedServer.Database;
using BNLReloadedServer.Logging;

namespace BNLReloadedServer.BaseTypes;

public static class BlockCardsCache
{
    private static readonly Lazy<CardBlock[]> LazyCache = new(InitCache);

    private static readonly HashSet<ushort> ReportedUnknown = [];

    private static CardBlock[] InitCache()
    {
        var dictionary = new Dictionary<ushort, CardBlock>();
        foreach (var card in Databases.Catalogue.All.Where((Func<Card, bool>)(a => a is CardBlock)))
        {
            var cardBlock = (CardBlock)card;
            dictionary[cardBlock.BlockId] = cardBlock;
        }

        var cache = new CardBlock[65536];
        for (var key = 0; key < cache.Length; ++key)
        {
            if (dictionary.TryGetValue((ushort)key, out var cardBlock))
                cache[key] = cardBlock;
        }
        return cache;
    }

    // A map outlives the catalogue that built it, so an id can arrive with no card behind it.
    // BlockBinary reads Card unguarded from Solid, Passable, Grounded and a dozen other places,
    // so returning null here turns a stale map into a NullReferenceException somewhere far away.
    // Air is the one substitute that cannot break stability or collision.
    public static CardBlock GetCard(ushort blockId)
    {
        var card = LazyCache.Value[blockId];
        if (card != null) return card;

        lock (ReportedUnknown)
        {
            if (ReportedUnknown.Add(blockId))
                Log.Warn(LogCat.Map, $"Block id {blockId} has no card in the catalogue — substituting air");
        }

        return LazyCache.Value[0];
    }
}
