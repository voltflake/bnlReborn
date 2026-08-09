using System.Net.Sockets;
using BNLReloadedServer.Database;
using NetCoreServer;
using BNLReloadedServer.Logging;

namespace BNLReloadedServer.Servers;

internal class MasterSession : TcpSession
{
    private readonly SessionReader _reader;
    private bool _connected;

    public MasterSession(AsyncTaskTcpServer server) : base(server)
    {
        var senderTask = new AsyncSenderTask(this);
        server.AddSenderTask(Id,  senderTask);
        var sender = new SessionSender(server, Id, senderTask);
        var serviceDispatcher = new MasterServiceDispatcher(sender, Id);
        _reader = new SessionReader(serviceDispatcher,
            "Master server received packet with incorrect length");
    }

    protected override void OnConnected()
    {
        _connected = true;
        Log.Info(LogCat.Conn, $"Master session {Id} connected");
    }

    protected override void OnDisconnected()
    {
        if (_connected)
            Log.Info(LogCat.Conn, $"Master session {Id} disconnected");

        _connected = false;

        Databases.MasterServerDatabase.RemoveRegionServer(Id.ToString());
    }

    protected override void OnReceived(byte[] buffer, long offset, long size)
    {
        if (size <= 0) return;
        
        _reader.ProcessPacket(buffer, offset, size);
    }

    protected override void OnError(SocketError error)
    {
        Log.Error(LogCat.Conn, $"Master session {Id} socket error: {error}");
    }
}