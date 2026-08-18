using BNLReloadedServer.BaseTypes;

namespace BNLReloadedServer.ServerTypes;

internal sealed class MatchParticipationTracker
{
    private readonly Dictionary<uint, CompletedMatchPlayer> _players = [];
    public DateTimeOffset? StartedAt { get; private set; }
    public bool HasParticipant(uint playerId) => _players.ContainsKey(playerId);

    public void Start(DateTimeOffset startedAt, IEnumerable<PlayerLobbyState> initialPlayers)
    {
        if (StartedAt.HasValue) return;
        StartedAt = startedAt;
        foreach (var player in initialPlayers) Join(player, startedAt, MatchJoinKind.Initial, false);
    }

    public void Join(PlayerLobbyState state, DateTimeOffset at, MatchJoinKind kind, bool backfiller)
    {
        if (!StartedAt.HasValue) return;
        if (!_players.TryGetValue(state.PlayerId, out var player))
        {
            player = new CompletedMatchPlayer
            {
                PlayerId = state.PlayerId,
                Nickname = state.Nickname ?? string.Empty,
                SquadId = state.SquadId,
                WasInitial = kind == MatchJoinKind.Initial,
                WasBackfiller = backfiller
            };
            _players[state.PlayerId] = player;
        }
        else
        {
            player.WasBackfiller |= backfiller;
            if (player.Presences.LastOrDefault()?.LeftAt is null) return;
        }

        player.Presences.Add(new CompletedMatchPresence
        {
            Sequence = player.Presences.Count,
            JoinedAt = (ulong)at.ToUnixTimeMilliseconds(),
            JoinKind = kind,
            Team = state.Team,
            HeroKey = state.Hero,
            SkinKey = state.SkinKey,
            Devices = new Dictionary<int, Key>(state.Devices ?? []),
            Perks = [.. state.Perks ?? []],
            DeviceLevels = new Dictionary<Key, int>(state.DeviceLevels ?? [])
        });
    }

    public void Leave(uint playerId, DateTimeOffset at, MatchLeaveKind reason)
    {
        var presence = _players.GetValueOrDefault(playerId)?.Presences.LastOrDefault();
        if (presence is null || presence.LeftAt.HasValue) return;
        presence.LeftAt = (ulong)at.ToUnixTimeMilliseconds();
        presence.LeaveKind = reason;
    }

    public void SetResult(uint playerId, bool winner, Dictionary<PlayerMatchStatType, int>? stats, int total)
    {
        if (!_players.TryGetValue(playerId, out var player)) return;
        player.IsWinner = winner;
        player.Stats = new Dictionary<PlayerMatchStatType, int>(stats ?? []);
        player.TotalScore = total;
    }

    public List<CompletedMatchPlayer> Complete(DateTimeOffset endedAt)
    {
        foreach (var player in _players.Values)
        {
            var open = player.Presences.LastOrDefault();
            if (open is not null && !open.LeftAt.HasValue)
            {
                open.LeftAt = (ulong)endedAt.ToUnixTimeMilliseconds();
                open.LeaveKind = MatchLeaveKind.MatchEnded;
            }
        }
        return _players.Values.ToList();
    }
}
