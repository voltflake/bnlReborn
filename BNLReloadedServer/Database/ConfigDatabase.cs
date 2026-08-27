using System.Net;
using System.Text.Json;
using BNLReloadedServer.BaseTypes;
using BNLReloadedServer.Logging;
using BNLReloadedServer.ProtocolHelpers;
using CouchDB.Driver;

namespace BNLReloadedServer.Database;

public class ConfigDatabase : IConfigDatabase
{
    private readonly Configs _configs;
    private readonly IReadOnlyList<ControlPanelUser> _controlPanelUsers;
    private readonly IPAddress _masterIp;
    
    public ConfigDatabase()
    {
        var configs = JsonSerializer.Deserialize<Configs>(File.ReadAllText(Databases.ConfigsFilePath),
            JsonHelper.DefaultSerializerSettings);
        _configs = configs ?? throw new FileNotFoundException("Configs file not found");
        _controlPanelUsers = File.Exists(Databases.ControlPanelUsersFilePath)
            ? JsonSerializer.Deserialize<List<ControlPanelUser>>(
                File.ReadAllText(Databases.ControlPanelUsersFilePath), JsonHelper.DefaultSerializerSettings) ?? []
            : [];
        _masterIp = IPAddress.Parse(_configs.MasterHost);
    }

    public string MasterHost() => _configs.MasterHost;
    public string MasterPublicHost() => _configs.MasterPublicHost;

    public IPAddress MasterIp() => _masterIp;

    public RegionGuiInfo GetRegionInfo() => new()
    {
        Icon = _configs.RegionIcon,
        Name = new LocalizedString
        {
            Text = _configs.RegionName,
            Data = new Dictionary<Locale, LocalizedEntry>
            {
                {
                    Locale.en, new LocalizedEntry
                    {
                        Original = _configs.RegionName,
                        Translation = _configs.RegionName
                    }
                }
            }
        }
    };

    public string ExportCdbName() => _configs.ExportCdbName ?? "cdb_export.json";

    public string CouchDbEndpoint() => _configs.CouchDbEndpoint ?? string.Empty;

    public BasicCredentials CouchDbCredentials() =>
            new(_configs.CouchDbUsername ?? string.Empty, _configs.CouchDbPassword ?? string.Empty);

    public string CouchDbDatabaseName() => _configs.CouchDbDatabaseName ?? string.Empty;

    /// <summary>
    /// log_level when it is set, otherwise the old debug_mode flag: existing configs keep the
    /// verbosity they already had without being edited.
    /// </summary>
    public LogLevel MinLogLevel() => string.IsNullOrWhiteSpace(_configs.LogLevel)
        ? _configs.DebugMode ? LogLevel.Debug : LogLevel.Info
        : LogNames.ParseLevel(_configs.LogLevel);

    public bool UseMaxDeviceLevel() => _configs.UseMaxDeviceLevel;

    public bool UseRaycastExplosions() => _configs.UseRaycastExplosions;
    
    public int ReconnectGraceSeconds() => _configs.ReconnectGraceSeconds;

    public int PingIntervalSeconds() => _configs.PingIntervalSeconds;

    public int MaxMissedPings() => _configs.MaxMissedPings;

    public bool ControlPanelEnabled() => _configs.ControlPanelEnabled;
    
    public int ControlPanelPort() => _configs.ControlPanelPort;

    public IReadOnlyList<ControlPanelUser> ControlPanelUsers() => _controlPanelUsers;
}
