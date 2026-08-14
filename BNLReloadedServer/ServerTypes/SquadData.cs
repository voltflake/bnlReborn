using BNLReloadedServer.BaseTypes;
using BNLReloadedServer.Database;
using BNLReloadedServer.Servers;
using BNLReloadedServer.Service;

namespace BNLReloadedServer.ServerTypes;

public class SquadData(ulong squadId, Key gameModeKey, ISender sender, IServicePlayer squadUpdater) : Updater
{
    // Only the updater thread writes this, but session threads read it while queueing and inviting,
    // so every mutation swaps in a fresh list instead of editing the live one.
    private volatile List<SquadPlayerUpdate> _players = [];

    // The session each member is subscribed with. A reconnect hands the player a new session id,
    // and the old one has to be dropped or the squad keeps talking to a dead socket.
    private readonly Dictionary<uint, Guid> _playerGuids = new();

    public required ChatRoom ChatRoom { get; init; }

    public Key GameMode { get; private set; } = gameModeKey;

    public int PlayerCount => _players.Count;

    private readonly IPlayerDatabase _playerDatabase = Databases.PlayerDatabase;

    public void AddPlayer(uint playerId, bool isOwner) => EnqueueAction(() =>
    {
        AddPlayerNoEnqueue(playerId, isOwner);
        SendSquadUpdate();
    });

    public void AddPlayers(List<uint> players, uint? ownerId) => EnqueueAction(() =>
    {
        foreach (var playerId in players)
        {
            AddPlayerNoEnqueue(playerId, ownerId == playerId);
        }

        SendSquadUpdate();
    });

    private void AddPlayerNoEnqueue(uint playerId, bool isOwner)
    {
        if (_players.Any(p => p.PlayerId == playerId)) return;

        var playerData = _playerDatabase.GetPlayerDataNoWait(playerId);
        var playerGuid = Databases.RegionServerDatabase.GetSessionGuid(playerId);

        // The cap is checked here as well as at the call site because it is per game mode, and the
        // leader can change the mode while an invite is in flight. Anyone who cannot be seated has
        // their membership handed back, or they are left pointing at a squad that does not list them.
        if (playerData == null || playerGuid == null ||
            _players.Count >= CatalogueHelper.MaxPlayersInSquad(GameMode))
        {
            Databases.RegionServerDatabase.ClearSquadId(playerId, squadId);
            return;
        }

        _players =
        [
            .._players,
            new SquadPlayerUpdate
            {
                PlayerId = playerId,
                IsLeader = isOwner,
                SteamId = playerData.SteamId,
                Nickname = playerData.Nickname,
                PlayerLevel = playerData.Progression.PlayerProgress?.Level ?? 0,
                HeroesLevels = playerData.Progression.HeroesProgress?.Select(k => k.Value.Level).ToList() ?? [],
                SelectedBadges = playerData.Badges,
                Graveyard = playerData.GraveyardPermanent ?? false,
                MmBanEnd = playerData.MatchmakerBanEnd ?? 0
            }
        ];

        _playerGuids[playerId] = playerGuid.Value;
        sender.Subscribe(playerGuid.Value);
        ChatRoom.AddToRoom(playerGuid.Value, Databases.RegionServerDatabase.GetChatService(playerId));
    }

    public void RemovePlayer(uint playerId) => EnqueueAction(() =>
    {
        RemovePlayerNoEnqueue(playerId);

        // A squad of one is what the client shows for a player with no squad at all, so there is
        // nothing left to keep alive.
        if (_players.Count <= 1)
        {
            CloseNoEnqueue();
            return;
        }

        SendSquadUpdate();
    });

    private void RemovePlayerNoEnqueue(uint playerId)
    {
        var player = _players.FirstOrDefault(p => p.PlayerId == playerId);
        if (player == null) return;

        var remaining = _players.Where(p => p.PlayerId != playerId).ToList();
        if (player.IsLeader && remaining.Count > 0)
        {
            remaining[0].IsLeader = true;
        }

        _players = remaining;

        if (_playerGuids.Remove(playerId, out var playerGuid))
        {
            sender.Unsubscribe(playerGuid);
            ChatRoom.RemoveFromRoom(playerGuid, Databases.RegionServerDatabase.GetChatService(playerId));
        }

        // Only tell the player their squad is gone if they have not moved to another one in the
        // meantime: leaving is asynchronous, so the new membership may already be recorded.
        if (Databases.RegionServerDatabase.ClearSquadId(playerId, squadId))
        {
            Databases.RegionServerDatabase.GetPlayerService(playerId)?.SendUpdateSquad(null);
        }
    }

    // A reconnect gives the player a new session; the one this squad holds is dead.
    public void RebindPlayer(uint playerId, Guid newGuid) => EnqueueAction(() =>
    {
        if (_players.All(p => p.PlayerId != playerId)) return;

        if (_playerGuids.TryGetValue(playerId, out var oldGuid) && oldGuid != newGuid)
        {
            sender.Unsubscribe(oldGuid);
            // No room-remove notification: it would go to the session that just went away.
            ChatRoom.RemoveFromRoom(oldGuid, null);
        }

        _playerGuids[playerId] = newGuid;
        sender.Subscribe(newGuid);
        ChatRoom.ResendRoom(newGuid, Databases.RegionServerDatabase.GetChatService(playerId));
        SendSquadUpdate();
    });

    // For a client that may have missed the broadcast, such as one still loading the main menu.
    public void SendUpdateTo(uint playerId) => EnqueueAction(() =>
    {
        if (_players.All(p => p.PlayerId != playerId)) return;

        Databases.RegionServerDatabase.GetPlayerService(playerId)?.SendUpdateSquad(new SquadUpdate
        {
            GameMode = GameMode,
            Players = _players
        });
    });

    public List<uint> GetPlayers() => _players.Select(p => p.PlayerId).ToList();

    public bool Contains(uint playerId) => _players.Any(p => p.PlayerId == playerId);

    public void ChangeGameMode(Key gameModeKey) => EnqueueAction(() =>
    {
        GameMode = gameModeKey;
        SendSquadUpdate();
    });

    private void SendSquadUpdate() => squadUpdater.SendUpdateSquad(new SquadUpdate
    {
        GameMode = GameMode,
        Players = _players
    });

    private void CloseNoEnqueue()
    {
        foreach (var playerId in _players.Select(p => p.PlayerId).ToList())
        {
            RemovePlayerNoEnqueue(playerId);
        }

        ChatRoom.ClearRoom();
        Databases.RegionServerDatabase.CloseSquad(squadId);
    }

    public bool IsOwner(uint playerId)
    {
        var player = _players.FirstOrDefault(p => p.PlayerId == playerId);
        return player?.IsLeader ?? false;
    }
}
