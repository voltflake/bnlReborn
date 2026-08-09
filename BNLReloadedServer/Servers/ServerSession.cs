using System.Net.Sockets;
using BNLReloadedServer.Service;
using NetCoreServer;
using BNLReloadedServer.Logging;

namespace BNLReloadedServer.Servers;

// Shared plumbing for every session type: sender task wiring, liveness probing, and a
// teardown path that runs exactly once no matter how many threads reach it.
public abstract class ServerSession : TcpSession
{
    private int _teardownStarted;
    private CancellationTokenSource? _livenessCts;
    private bool _connected;

    // Resolved once while the socket is up — RemoteEndPoint is gone by the time anything wants to
    // name the peer in a teardown line — and on first use rather than at connect, because the
    // receive callback can get there before OnConnected does.
    private volatile string? _peer;

    private string? Peer => _peer ??= ReadPeer();

    protected SessionSender Sender { get; }

    protected string Label { get; }

    protected abstract SessionReader Reader { get; }

    // Null for session types that do not probe for liveness.
    protected abstract IServicePing? LivenessPing { get; }

    protected abstract void OnTeardown();

    protected ServerSession(AsyncTaskTcpServer server, string label) : base(server)
    {
        Label = label;
        var senderTask = new AsyncSenderTask(this);
        server.AddSenderTask(Id, senderTask);
        Sender = new SessionSender(server, Id, senderTask);
    }

    protected override void OnConnected()
    {
        _connected = true;
        _ = Peer;

        if (LivenessPing is { } ping)
        {
            _livenessCts = SessionLiveness.Start(ping, this, Label, Sender, Reader);
            // A socket failure can tear this session down while we are still in here. Teardown
            // would have seen a null field, so the probe we just started is ours to stop.
            if (Volatile.Read(ref _teardownStarted) != 0)
                StopLiveness();
        }

        Log.Info(LogCat.Conn, $"{Label} session {Id} connected{From}");
    }

    private string From => Peer is { } peer ? $" from {peer}" : string.Empty;

    private string? ReadPeer()
    {
        try
        {
            return Socket.RemoteEndPoint?.ToString();
        }
        catch (Exception)
        {
            // A socket that died between accept and here has no address left to report.
            return null;
        }
    }

    // Disconnect() gates on a plain IsConnected check, so the liveness timer, the receive
    // completion and a failing Send can all reach here at once. The first thread in does the
    // teardown; the rest fall straight through.
    protected override void OnDisconnected()
    {
        if (Interlocked.Exchange(ref _teardownStarted, 1) != 0) return;

        StopLiveness();
        OnTeardown();

        if (_connected)
            Log.Info(LogCat.Conn, $"{Label} session {Id} disconnected{From}");

        _connected = false;
    }

    private void StopLiveness()
    {
        var cts = Interlocked.Exchange(ref _livenessCts, null);
        if (cts == null) return;

        cts.Cancel();
        cts.Dispose();
    }

    protected override void OnReceived(byte[] buffer, long offset, long size)
    {
        if (size <= 0) return;

        using var peer = Log.WithPeer(Peer);
        Reader.ProcessPacket(buffer, offset, size);
    }

    protected override void OnError(SocketError error)
    {
        Log.Error(LogCat.Conn, $"{Label} session {Id} socket error: {error}{From}");
    }
}
