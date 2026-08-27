using System.Net;
using System.Net.Sockets;
using NetCoreServer;
using BNLReloadedServer.Logging;

namespace BNLReloadedServer.Servers;

public class MatchServer(IPAddress address, int port) : AsyncTaskTcpServer(address, port)
{
    protected override TcpSession CreateSession() => new MatchSession(this);

    protected override void OnStarting() => Log.Info(LogCat.Server, $"Match server starting on {Address}:{Port}...");

    protected override void OnStarted() => Log.Info(LogCat.Server, "Match server started");

    protected override void OnError(SocketError error) =>
        Log.Error(LogCat.Server, $"Match server socket error: {error}");
}
