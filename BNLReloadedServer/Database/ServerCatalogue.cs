using System.Collections.Frozen;
using BNLReloadedServer.BaseTypes;
using BNLReloadedServer.ProtocolHelpers;

namespace BNLReloadedServer.Database;

public class ServerCatalogue : Catalogue
{
    private FrozenDictionary<Key, Card> _db;
    private readonly Lock _updateLock = new();

    public ServerCatalogue()
    {
        _db = FrozenDictionary<Key, Card>.Empty;
    }
    
    public override Card? GetCard(Key key)
    {
        return _db.GetValueOrDefault(key);
    }

    public override IEnumerable<Card> All => _db.Values;

    public void Replicate(List<Card> cards)
    {
        lock (_updateLock)
        {
            var tempDict = new Dictionary<Key, Card>(KeyEqualityComparer.Instance);
            foreach (var card in cards)
            {
                if (card.Id == null) continue;
                card.Key = Key(card.Id);
                tempDict.Add(card.Key, card);
            }
            _db = tempDict.ToFrozenDictionary();
            Replicated = true;
            CatalogueBlob.Set(cards);
        }
    }

    public void UpdateCard(Card card)
    {
        if (card.Id == null) return;
        lock (_updateLock)
        {
            card.Key = Key(card.Id);
            var cards = _db.Values.Where(existing => existing.Id != card.Id).Append(card).ToList();
            ValidateIncrementalChange(cards);

            var tempDict = new Dictionary<Key, Card>(_db, KeyEqualityComparer.Instance)
            {
                [card.Key] = card
            };
            _db = tempDict.ToFrozenDictionary();
            CatalogueBlob.Set(cards);
        }
    }

    public bool RemoveCard(string id)
    {
        lock (_updateLock)
        {
            var key = Key(id);
            if (!_db.TryGetValue(key, out var existing) || existing.Id != id) return false;

            var cards = _db.Values.Where(card => card.Id != id).ToList();
            ValidateIncrementalChange(cards);

            var tempDict = new Dictionary<Key, Card>(_db, KeyEqualityComparer.Instance);
            tempDict.Remove(key);
            _db = tempDict.ToFrozenDictionary();
            CatalogueBlob.Set(cards);
            return true;
        }
    }

    private static void ValidateIncrementalChange(List<Card> cards)
    {
        var problems = CatalogueValidator.Validate(cards);
        if (problems.Count > 0)
            throw new InvalidOperationException($"Rejected incremental catalogue change — {string.Join("; ", problems)}");
    }
}
