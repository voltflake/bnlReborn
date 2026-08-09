using BNLReloadedServer.Database;
using BNLReloadedServer.Service;

namespace BNLReloadedServer.Servers;

public class MatchSession : ServerSession
{
    private readonly MatchServiceDispatcher _serviceDispatcher;
    private readonly SessionReader _reader;

    public MatchSession(AsyncTaskTcpServer server) : base(server, "Match")
    {
        _serviceDispatcher = new MatchServiceDispatcher(Sender, Id);
        _reader = new SessionReader(_serviceDispatcher,
            "Match server received packet with incorrect length");
    }

    protected override SessionReader Reader => _reader;

    protected override IServicePing LivenessPing => _serviceDispatcher.Ping;

    protected override void OnTeardown() => Databases.RegionServerDatabase.RemoveMatchServices(Id);
}
