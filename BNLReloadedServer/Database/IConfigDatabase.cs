using System.Net;
using BNLReloadedServer.BaseTypes;
using BNLReloadedServer.Logging;
using CouchDB.Driver;

namespace BNLReloadedServer.Database;

public interface IConfigDatabase
{
    public bool IsMaster();
    public bool DoRunServer();
    public string MasterHost();
    public string MasterPublicHost();
    public IPAddress MasterIp();
    public string RegionHost();
    public string RegionPublicHost();
    public IPAddress RegionIp();
    public IPAddress RegionPublicIp();
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
    public bool DoReadline();
    public bool ControlPanelEnabled();
    public string ControlPanelHost();
    public int ControlPanelPort();
    public IReadOnlyList<ControlPanelUser> ControlPanelUsers();
}
