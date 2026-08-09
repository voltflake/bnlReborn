using System.Net;
using System.Net.Sockets;
using NetCoreServer;
using BNLReloadedServer.Logging;

namespace BNLReloadedServer.Servers;

public class RegionServer(IPAddress address, int port) : AsyncTaskTcpServer(address, port)
{
    protected override TcpSession CreateSession() => new RegionSession(this);

    protected override void OnStarting() => Log.Info(LogCat.Server, $"Region server starting on {Address}:{Port}...");

    protected override void OnStarted() => Log.Info(LogCat.Server, "Region server started");

    protected override void OnError(SocketError error) => 
        Log.Error(LogCat.Server, $"Region server socket error: {error}");
}