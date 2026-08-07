using BNLReloadedServer.BaseTypes;

namespace BNLReloadedServer.Database;

public abstract class CatalogueStore
{
    /// <summary>
    /// Every card's Key is the CRC32 of its id, derived rather than stored, so it has to be
    /// (re)computed for the whole catalogue on every load.
    /// </summary>
    protected static void RehashKeys(IEnumerable<Card> cards)
    {
        foreach (var card in cards)
        {
            card.Key = Catalogue.Key(card.Id ?? string.Empty);
        }
    }

    public abstract void Store(IEnumerable<Card> cards);
    public abstract List<Card> Load();
}
