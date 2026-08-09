using BNLReloadedServer.Database;
using BNLReloadedServer.Service;
using NetCoreServer;
using BNLReloadedServer.Logging;

namespace BNLReloadedServer.Servers;

internal static class SessionLiveness
{
    public static CancellationTokenSource? Start(IServicePing ping, TcpSession session, string label)
    {
        var interval = Databases.ConfigDatabase.PingIntervalSeconds();
        var maxMissed = Databases.ConfigDatabase.MaxMissedPings();
        if (interval <= 0 || maxMissed <= 0) return null;

        var cts = new CancellationTokenSource();
        _ = Run(ping, session, label, TimeSpan.FromSeconds(interval), maxMissed, cts.Token);
        return cts;
    }

    private static async Task Run(IServicePing ping, TcpSession session, string label, TimeSpan interval,
        int maxMissed, CancellationToken token)
    {
        try
        {
            using var timer = new PeriodicTimer(interval);
            while (await timer.WaitForNextTickAsync(token))
            {
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
