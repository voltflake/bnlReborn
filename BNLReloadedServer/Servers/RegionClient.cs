using System.Net.Sockets;
using BNLReloadedServer.Database;
using TcpClient = NetCoreServer.TcpClient;
using BNLReloadedServer.Logging;

namespace BNLReloadedServer.Servers;

public class RegionClient : TcpClient
{
    private readonly RegionClientServiceDispatcher _serviceDispatcher;
    private readonly SessionReader _reader;
    private bool _connected;
    private int _teardownStarted;
    private volatile string? _peer;

    private string? Peer => _peer ??= ReadPeer();

    public RegionClient(string address, int port) : base(address, port)
    {
        var sender = new ClientSender(this);
        _serviceDispatcher = new RegionClientServiceDispatcher(sender);
        _reader = new SessionReader(_serviceDispatcher,
            "Region client server received packet with incorrect length");
    }
    
    public void DisconnectAndStop()
    {
        _stop = true;
        DisconnectAsync();
        while (IsConnected)
            Thread.Yield();
    }

    protected override void OnConnecting()
    {
        // No socket yet, so the address comes from where we are dialling rather than from the peer.
        Log.Info(LogCat.Conn, $"Region client connecting to master {Address}:{Port}...");
    }

    protected override void OnConnected()
    {
        // Re-armed on every connect: unlike a server session this client outlives its
        // connections and has to tear down again after each one.
        Volatile.Write(ref _teardownStarted, 0);
        _connected = true;
        Log.Info(LogCat.Conn, $"Region client connected to master {Describe}");

        var host = Databases.ConfigDatabase.RegionPublicHost();
        var guiInfo = Databases.ConfigDatabase.GetRegionInfo();
        
        Databases.PlayerDatabase.SetRegionServerService(_serviceDispatcher.ServiceRegionServer);
        _serviceDispatcher.ServiceRegionServer.SendRegionInfo(host, guiInfo);
    }

    // Disconnect() gates on a plain IsConnected check, so the receive completion and an explicit
    // Disconnect() can both land here. The loser would otherwise block a socket callback thread
    // on the wait below and fire its own reconnect.
    protected override void OnDisconnected()
    {
        if (Interlocked.Exchange(ref _teardownStarted, 1) != 0) return;

        if (_connected)
            Log.Info(LogCat.Conn, $"Region client disconnected from master {Describe}");

        _connected = false;
        
        // Wait for a while...
        Task.Delay(1000).Wait();

        // Try to connect again
        if (!_stop)
            ConnectAsync();
    }

    protected override void OnReceived(byte[] buffer, long offset, long size)
    {
        if (size <= 0) return;

        using var peer = Log.WithPeer(Peer);
        _reader.ProcessPacket(buffer, offset, size);
    }

    private string Describe => Peer is { } peer ? $"{peer} ({Log.ShortId(Id)})" : Log.ShortId(Id);

    private string? ReadPeer()
    {
        try
        {
            return Socket.RemoteEndPoint?.ToString();
        }
        catch (Exception)
        {
            return null;
        }
    }

    protected override void OnError(SocketError error)
    {
        Log.Error(LogCat.Conn, $"Region client {Describe} socket error: {error}");
    }

    private bool _stop;
}
