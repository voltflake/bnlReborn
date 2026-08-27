using System.Net;
using BNLReloadedServer.BaseTypes;
using BNLReloadedServer.Logging;
using CouchDB.Driver;

namespace BNLReloadedServer.Database;

public class DummyConfigDatabase : IConfigDatabase
{


    public string MasterHost() => "127.0.0.1";
    public string MasterPublicHost() => "127.0.0.1";

    public IPAddress MasterIp() => IPAddress.Parse(MasterHost());

    public RegionGuiInfo GetRegionInfo() => new()
    {
        Icon = "server_namericaeast",
        Name = new LocalizedString
        {
            Text = "Test",
            Data = new Dictionary<Locale, LocalizedEntry>
            {
                {
                    Locale.en, new LocalizedEntry
                    {
                        Original = "Test",
                        Translation = "Test"
                    }
                }
            }
        }
    };

    public string ExportCdbName() => "cdb_export.json";


    

    public string CouchDbEndpoint() => "http://localhost:5984";

    public BasicCredentials CouchDbCredentials() => new("admin", "admin");

    public string CouchDbDatabaseName() => "test";
    
    public LogLevel MinLogLevel() => LogLevel.Debug;

    public bool UseMaxDeviceLevel() => false;

    public bool UseRaycastExplosions() => false;
    
    public int ReconnectGraceSeconds() => 180;

    public int PingIntervalSeconds() => 2;

    public int MaxMissedPings() => 11;

    public bool ControlPanelEnabled() => true;
    
    public int ControlPanelPort() => 8080;

    public IReadOnlyList<ControlPanelUser> ControlPanelUsers() =>
        [new ControlPanelUser { Username = "admin", Password = "admin" }];
}
