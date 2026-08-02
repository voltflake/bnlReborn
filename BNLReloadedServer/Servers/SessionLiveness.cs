using BNLReloadedServer.Database;
using BNLReloadedServer.Service;
using NetCoreServer;

namespace BNLReloadedServer.Servers;

/// <summary>
/// A client that vanishes without closing its socket - a pulled cable, a killed VM - leaves the session
/// open on our side. TCP keepalive never helps in a match: it only probes idle connections, and the zone
/// keeps writing to that socket, so the connection stays with the retransmit timer until it gives up
/// minutes later. Meanwhile the player holds a slot in the match with nothing to end their turn in it.
///
/// So we ask ourselves. Every interval the session pings the client, which answers from its network
/// thread; once enough probes in a row go unanswered we drop the session, and the ordinary disconnect
/// path - grace timer included - takes it from there.
/// </summary>
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

                Console.WriteLine($"{label} TCP session with Id {session.Id} missed {maxMissed} pings, disconnecting it!");
                session.Disconnect();
                return;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
}
