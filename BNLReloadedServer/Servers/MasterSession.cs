using BNLReloadedServer.Database;
using BNLReloadedServer.Service;

namespace BNLReloadedServer.Servers;

internal class MasterSession : ServerSession
{
    private readonly SessionReader _reader;

    public MasterSession(AsyncTaskTcpServer server) : base(server, "Master")
    {
        _reader = new SessionReader(new MasterServiceDispatcher(Sender, Id, () => PeerAddress),
            "Master server received packet with incorrect length");
    }

    protected override SessionReader Reader => _reader;

    protected override IServicePing? LivenessPing => null;

    protected override void OnTeardown() { }
}
