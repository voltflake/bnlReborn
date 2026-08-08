using System.Net;
using System.Text.Json;
using BNLReloadedServer.BaseTypes;
using BNLReloadedServer.ProtocolHelpers;
using CouchDB.Driver;

namespace BNLReloadedServer.Database;

public class ConfigDatabase : IConfigDatabase
{
    private readonly Configs _configs;
    private readonly IPAddress _masterIp;
    private readonly IPAddress _regionIp;
    private readonly IPAddress _regionPublicIp;
    
    public ConfigDatabase()
    {
        var configs = JsonSerializer.Deserialize<Configs>(File.ReadAllText(Databases.ConfigsFilePath),
            JsonHelper.DefaultSerializerSettings);
        _configs = configs ?? throw new FileNotFoundException("Configs file not found");
        _masterIp = IPAddress.Parse(_configs.MasterHost);
        _regionIp = IPAddress.Parse(_configs.RegionHost);
        _regionPublicIp = IPAddress.Parse(_configs.RegionPublicHost);
    }

    public bool IsMaster() => _configs.IsMaster;
    
    public bool DoRunServer() => _configs.RunServer;

    public string MasterHost() => _configs.MasterHost;
    public string MasterPublicHost() => _configs.MasterPublicHost;

    public IPAddress MasterIp() => _masterIp;

    public string RegionHost() => _configs.RegionHost;
    
    public string RegionPublicHost() => _configs.RegionPublicHost;

    public IPAddress RegionIp() => _regionIp;

    public IPAddress RegionPublicIp() => _regionPublicIp;

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

    public bool DebugMode() => _configs.DebugMode;

    public bool UseMaxDeviceLevel() => _configs.UseMaxDeviceLevel;

    public bool UseRaycastExplosions() => _configs.UseRaycastExplosions;
    
    public int ReconnectGraceSeconds() => _configs.ReconnectGraceSeconds;

    public int PingIntervalSeconds() => _configs.PingIntervalSeconds;

    public int MaxMissedPings() => _configs.MaxMissedPings;

    public bool DoReadline() => _configs.DoReadline;
    
    public bool ControlPanelEnabled() => _configs.ControlPanelEnabled;
    
    public string ControlPanelHost() => _configs.ControlPanelHost;
    
    public int ControlPanelPort() => _configs.ControlPanelPort;

    public string ControlPanelPasswordHash() => _configs.ControlPanelPasswordHash ?? string.Empty;
}