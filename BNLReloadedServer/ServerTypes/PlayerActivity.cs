namespace BNLReloadedServer.ServerTypes;

/// <summary>
/// Where everyone connected to this region currently is, counted in one pass so the
/// buckets agree with each other. Subtracting two separately-sampled responses can go
/// negative between them; this cannot.
/// </summary>
/// <param name="Online">Connected users, the denominator for everything below.</param>
/// <param name="InMenu">
/// Connected and not in a game instance. Queueing happens *in* the menu, so someone
/// waiting for a match is still counted here — the queues themselves are reported by
/// /api/queues, and taking them out of this figure would mean joining a queue made a
/// player vanish from both.
/// </param>
/// <param name="ByMode">
/// One entry per game mode that currently holds anyone. A custom game counts its lobby
/// as well as its match, because joining a custom lobby already sets the player's
/// instance id.
/// </param>
/// <param name="Spectating">Connected users actively spectating a game. These users are
/// intentionally excluded from <paramref name="ByMode"/> so activity buckets do not
/// double-count them.</param>
public record PlayerActivity(
    int Online,
    int InMenu,
    List<ModePlayerCount> ByMode,
    int Spectating);

public record ModePlayerCount(string ModeId, string? ModeName, int Players);
