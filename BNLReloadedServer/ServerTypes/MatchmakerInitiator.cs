using BNLReloadedServer.BaseTypes;
using BNLReloadedServer.Database;
using Moserware.Skills;

namespace BNLReloadedServer.ServerTypes;

public class MatchmakerInitiator(CardGameMode gameMode, List<PlayerQueueData> team1, List<PlayerQueueData> team2)
    : IGameInitiator
{
    // A freed slot is not offered up straight away - the match may be ending, or the player may be
    // on their way back. Nothing is lost by letting it settle, and a wasted backfill costs a real
    // player a queue pop.
    private const double SlotSettleSeconds = 10;

    private readonly List<PlayerQueueData> _team1 = team1.ToList();
    private readonly List<PlayerQueueData> _team2 = team2.ToList();

    private bool _backfillReady;

    private DateTimeOffset? _firstSlotFreed;

    public string? GameInstanceId { get; set; }
    
    public void StartIntoMatch()
    {
    }

    public void ClearInstance(string? instanceId)
    {
    }

    public bool AddPlayer(PlayerQueueData player, TeamType team)
    {
        switch (team)
        {
            case TeamType.Team1:
                if (_team1.Count >= gameMode.PlayersPerTeam)
                {
                    return false;
                }
                _team1.Add(player);
                break;
            case TeamType.Team2:
                if (_team2.Count >= gameMode.PlayersPerTeam)
                {
                    return false;
                }
                _team2.Add(player);
                break;
            case TeamType.Neutral:
            default:
                return false;
        }

        if (_team1.Count >= gameMode.PlayersPerTeam && _team2.Count >= gameMode.PlayersPerTeam)
        {
            _firstSlotFreed = null;
        }

        return true;
    }

    public void RemovePlayer(uint playerId)
    {
        // Only the first departure starts the clock. Stamping every one would mean a match bleeding
        // players a few seconds apart never accumulates a quiet window, so it would never be offered
        // backfill at all - the opposite of the point.
        if (_team1.RemoveAll(p => p.PlayerId == playerId) + _team2.RemoveAll(p => p.PlayerId == playerId) > 0)
        {
            _firstSlotFreed ??= DateTimeOffset.Now;
        }
    }

    public TeamType GetTeamForPlayer(uint playerId) =>
        _team1.Any(p => p.PlayerId == playerId) 
            ? TeamType.Team1 
            : _team2.Any(p => p.PlayerId == playerId) 
                ? TeamType.Team2 
                : TeamType.Neutral;

    public bool IsPlayerSpectator(uint playerId) => false;

    public bool IsPlayerBackfill(uint playerId) =>
        !(team1.Any(p => p.PlayerId == playerId) || team2.Any(p => p.PlayerId == playerId));

    public Key GetGameMode() => gameMode.Key;

    public bool CanSwitchHero() => false;

    public bool IsMapEditor() => false;

    public float GetResourceCap() => gameMode.MatchMode.GetCard<CardMatch>()?.ResourceCap ?? 7500;

    public float GetResourceAmount() => gameMode.MatchMode.GetCard<CardMatch>()?.InitResource ?? 2000;

    public long? GetBuildPhaseEndTime(DateTimeOffset startTime) => startTime.AddSeconds((long)(gameMode.MatchMode.GetCard<CardMatch>()?.Data switch
    {
        MatchDataShieldCapture matchDataShieldCapture => matchDataShieldCapture.Build1Time,
        MatchDataShieldRush2 matchDataShieldRush2 => matchDataShieldRush2.Build1Time,
        MatchDataTimeTrial matchDataTimeTrial => matchDataTimeTrial.PrestartTime,
        MatchDataTutorial matchDataTutorial => matchDataTutorial.BuildTime,
        _ => 0
    })).ToUnixTimeMilliseconds();

    public float GetRespawnMultiplier() => 0;

    public bool IsSuperSupplies() => false;

    public bool NeedsBackfill() => (_team1.Count < gameMode.PlayersPerTeam || _team2.Count < gameMode.PlayersPerTeam) &&
                                   _backfillReady && (_firstSlotFreed is null ||
                                                      (DateTimeOffset.Now - _firstSlotFreed.Value).TotalSeconds >=
                                                      SlotSettleSeconds);
    
    public void SetBackfillReady(bool backfillReady) => _backfillReady = backfillReady;

    public (Dictionary<uint, Rating> team1, Dictionary<uint, Rating> team2) GetTeamRatings() => (
        _team1.ToDictionary(k => k.PlayerId, v => v.Rating), _team2.ToDictionary(k => k.PlayerId, v => v.Rating));
}