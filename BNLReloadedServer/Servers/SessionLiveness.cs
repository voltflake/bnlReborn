using BNLReloadedServer.Database;
using BNLReloadedServer.Service;
using NetCoreServer;
using BNLReloadedServer.Logging;

namespace BNLReloadedServer.Servers;

internal static class SessionLiveness
{
    public static CancellationTokenSource? Start(IServicePing ping, TcpSession session, string label,
        SessionSender sender, SessionReader reader)
    {
        var interval = Databases.ConfigDatabase.PingIntervalSeconds();
        var maxMissed = Databases.ConfigDatabase.MaxMissedPings();
        if (interval <= 0 || maxMissed <= 0) return null;

        var cts = new CancellationTokenSource();
        _ = Run(ping, session, label, sender, reader, TimeSpan.FromSeconds(interval), maxMissed, cts.Token);
        return cts;
    }

    private static async Task Run(IServicePing ping, TcpSession session, string label, SessionSender sender,
        SessionReader reader, TimeSpan interval, int maxMissed, CancellationToken token)
    {
        // The same budget either way: a session gets this long to prove it is still there, whether
        // it does so by answering probes or by saying anything at all.
        var silenceBudget = interval * maxMissed;

        try
        {
            using var timer = new PeriodicTimer(interval);
            while (await timer.WaitForNextTickAsync(token))
            {
                if (sender.AssociatedPlayerId == null)
                {
                    if (reader.SinceLastPacket <= silenceBudget) continue;

                    Log.Warn(LogCat.Conn, $"{label} session {session.Id} sent nothing for " +
                                          $"{silenceBudget.TotalSeconds:0}s without logging in, disconnecting it");
                    session.Disconnect();
                    return;
                }

                if (ping.SendLivenessProbe() <= maxMissed) continue;

                Log.Warn(LogCat.Conn, $"{label} session {session.Id} missed {maxMissed} pings, disconnecting it");
                session.Disconnect();
                return;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            Log.Error(LogCat.Conn, $"{label} session {session.Id} liveness probe failed", e);
        }
    }
}
