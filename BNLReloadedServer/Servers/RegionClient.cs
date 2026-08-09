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
        Log.Info(LogCat.Conn, "Region client connecting to master...");
    }

    protected override void OnConnected()
    {
        _connected = true;
        Log.Info(LogCat.Conn, $"Region client connected to master as session {Id}");

        var host = Databases.ConfigDatabase.RegionPublicHost();
        var guiInfo = Databases.ConfigDatabase.GetRegionInfo();
        
        Databases.PlayerDatabase.SetRegionServerService(_serviceDispatcher.ServiceRegionServer);
        _serviceDispatcher.ServiceRegionServer.SendRegionInfo(host, guiInfo);
    }

    protected override void OnDisconnected()
    {
        if (_connected)
            Log.Info(LogCat.Conn, $"Region client disconnected from master (session {Id})");

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
        
        _reader.ProcessPacket(buffer, offset, size);
    }

    protected override void OnError(SocketError error)
    {
        Log.Error(LogCat.Conn, $"Region client socket error: {error}");
    }

    private bool _stop;
}
