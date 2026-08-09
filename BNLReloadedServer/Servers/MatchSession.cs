using System.Net.Sockets;
using BNLReloadedServer.Database;
using NetCoreServer;
using BNLReloadedServer.Logging;

namespace BNLReloadedServer.Servers;

public class MatchSession : TcpSession
{
    private readonly SessionSender _sender;
    private readonly SessionReader _reader;
    private readonly MatchServiceDispatcher _serviceDispatcher;
    private CancellationTokenSource? _livenessCts;
    private bool _connected;

    public MatchSession(AsyncTaskTcpServer server) : base(server)
    {
        var senderTask = new AsyncSenderTask(this);
        server.AddSenderTask(Id, senderTask);
        _sender = new SessionSender(server, Id, senderTask);
        _serviceDispatcher = new MatchServiceDispatcher(_sender, Id);
        _reader = new SessionReader(_serviceDispatcher,
            "Match server received packet with incorrect length");
    }

    protected override void OnConnected()
    {
        _connected = true;
        _livenessCts = SessionLiveness.Start(_serviceDispatcher.Ping, this, "Match", _sender, _reader);
        Log.Info(LogCat.Conn, $"Match session {Id} connected");
    }

    protected override void OnDisconnected()
    {
        _livenessCts?.Cancel();
        _livenessCts?.Dispose();
        _livenessCts = null;
        Databases.RegionServerDatabase.RemoveMatchServices(Id);
        if (_connected)
            Log.Info(LogCat.Conn, $"Match session {Id} disconnected");

        _connected = false;
    }

    protected override void OnReceived(byte[] buffer, long offset, long size)
    {
        if (size <= 0) return;
        
        _reader.ProcessPacket(buffer, offset, size);
    }

    protected override void OnError(SocketError error)
    {
        Log.Error(LogCat.Conn, $"Match session {Id} socket error: {error}");
    }
}