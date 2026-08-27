using System.Net;
using System.Net.Sockets;
using NetCoreServer;
using BNLReloadedServer.Logging;

namespace BNLReloadedServer.Servers;

public class MasterServer(IPAddress address, int port) : AsyncTaskTcpServer(address, port)
{
    protected override TcpSession CreateSession() => new MasterSession(this);

    protected override void OnStarting() => Log.Info(LogCat.Server, $"Master server starting on {Address}:{Port}...");

    protected override void OnStarted() => Log.Info(LogCat.Server, "Master server started");

    protected override void OnError(SocketError error) =>
        Log.Error(LogCat.Server, $"Master server socket error: {error}");
}
