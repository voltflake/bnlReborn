using System.Net.Sockets;
using BNLReloadedServer.Database;
using NetCoreServer;

namespace BNLReloadedServer.Servers;

internal class RegionSession : TcpSession
{
    private readonly SessionSender _sender;
    private readonly SessionReader _reader;
    private readonly RegionServiceDispatcher _serviceDispatcher;
    private CancellationTokenSource? _livenessCts;
    private bool _connected;

    public RegionSession(AsyncTaskTcpServer server) : base(server)
    {
        var senderTask = new AsyncSenderTask(this);
        server.AddSenderTask(Id, senderTask);
        _sender = new SessionSender(server, Id, senderTask);
        _serviceDispatcher = new RegionServiceDispatcher(_sender, Id);
        _reader = new SessionReader(_serviceDispatcher, Databases.ConfigDatabase.DebugMode(),
            "Region server received packet with incorrect length");
    }

    protected override void OnConnected()
    {
        _connected = true;
        _livenessCts = SessionLiveness.Start(_serviceDispatcher.Ping, this, "Region");
        Console.WriteLine($"Region TCP session with Id {Id} connected!");
    }

    protected override void OnDisconnected()
    {
        _livenessCts?.Cancel();
        _livenessCts?.Dispose();
        _livenessCts = null;

        if (_sender.AssociatedPlayerId != null)
        {
            Databases.RegionServerDatabase.RemoveUser(_sender.AssociatedPlayerId.Value);
            Databases.PlayerDatabase.RemovePlayer(_sender.AssociatedPlayerId.Value);
        }
            
        Databases.RegionServerDatabase.RemoveServices(Id);

        if (_connected)
        {
            Console.WriteLine($"Region TCP session with Id {Id} disconnected!");
        }
        
        _connected = false;
    }

    protected override void OnReceived(byte[] buffer, long offset, long size)
    {
        if (size <= 0) return;
        
        _reader.ProcessPacket(buffer, offset, size);
    }

    protected override void OnError(SocketError error)
    {
        Console.WriteLine($"Region TCP session caught an error with code {error}");
    }
}