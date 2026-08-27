using System.Net;
using BNLReloadedServer.BaseTypes;
using BNLReloadedServer.Logging;
using CouchDB.Driver;

namespace BNLReloadedServer.Database;

public interface IConfigDatabase
{
    public string MasterHost();
    public string MasterPublicHost();
    public IPAddress MasterIp();
    public RegionGuiInfo GetRegionInfo();
    public string ExportCdbName();
    public string CouchDbEndpoint();
    public BasicCredentials CouchDbCredentials();
    public string CouchDbDatabaseName();
    public LogLevel MinLogLevel();
    public bool UseMaxDeviceLevel();
    public bool UseRaycastExplosions();
    public int ReconnectGraceSeconds();
    public int PingIntervalSeconds();
    public int MaxMissedPings();
    public bool ControlPanelEnabled();
    public int ControlPanelPort();
    public IReadOnlyList<ControlPanelUser> ControlPanelUsers();
}
