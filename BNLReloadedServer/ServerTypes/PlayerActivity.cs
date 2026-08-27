namespace BNLReloadedServer.ServerTypes;

public record PlayerActivity(
    int Online,
    int InMenu,
    List<ModePlayerCount> ByMode,
    int Spectating);

public record ModePlayerCount(string ModeId, string? ModeName, int Players);
