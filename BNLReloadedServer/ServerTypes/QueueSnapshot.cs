namespace BNLReloadedServer.ServerTypes;

/// <summary>
/// A read-only view of one matchmaking queue, for observers such as the control panel.
/// Nothing in the matchmaker consumes this — it exists so a reader never has to hold a
/// reference into the live <c>QueueData</c>, which is mutated without locking.
/// </summary>
/// <param name="ModeId">The game mode card's id, e.g. <c>game_mode_ranked</c>.</param>
/// <param name="ModeName">Its display name, or null if the card is missing.</param>
/// <param name="State">
/// <c>waiting</c>, <c>confirming</c>, <c>backfilling</c> or <c>pop_failed</c>, or
/// <c>unavailable</c> when the queue could not be read this tick.
/// </param>
/// <param name="ConfirmDeadline">
/// Unix ms by which the popped players must accept, null while nothing has popped. The
/// same value the client receives as <c>MatchmakerState.ConfirmationTimeout</c>.
/// </param>
public record QueueSnapshot(
    string ModeId,
    string? ModeName,
    int PlayerCount,
    string State,
    ulong? ConfirmDeadline,
    List<QueuedPlayerSnapshot> Players);

/// <param name="JoinTime">Unix ms, so the wait can tick between pushed snapshots.</param>
/// <param name="Confirming">
/// True for the players in the popped line-up. Anyone who joined behind them is still
/// waiting, so a queue can hold both at once.
/// </param>
public record QueuedPlayerSnapshot(
    uint PlayerId,
    string? Nickname,
    long JoinTime,
    bool Confirming);
