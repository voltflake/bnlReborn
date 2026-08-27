using BNLReloadedServer.BaseTypes;

namespace BNLReloadedServer.Database;

public class Configs
{
    public required string MasterHost { get; init; }
    public required string MasterPublicHost { get; init; }
    public required string RegionName { get; init; }
    public required string RegionIcon { get; init; }
    public string? ExportCdbName { get; init; }
    public string? CouchDbEndpoint { get; init; }
    public string? CouchDbUsername { get; init; }
    public string? CouchDbPassword { get; init; }
    public string? CouchDbDatabaseName { get; init; }
    public bool DebugMode { get; init; }

    public string? LogLevel { get; init; }

    public bool UseMaxDeviceLevel { get; init; }
    public bool UseRaycastExplosions { get; init; }
    public int ReconnectGraceSeconds { get; init; } = 180;
    public int PingIntervalSeconds { get; init; } = 2;
    public int MaxMissedPings { get; init; } = 11;
    public bool ControlPanelEnabled { get; init; }
    public int ControlPanelPort { get; init; } = 8080;
}
