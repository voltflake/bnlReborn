using System.Diagnostics;
using System.Threading.Channels;

namespace BNLReloadedServer.ServerTypes;

public abstract class Updater
{
    // Actions share one queue with the game tick, so anything slower than this visibly stalls the simulation.
    private const int SlowActionMillis = 25;

    private readonly Channel<Action> _updateActions = Channel.CreateUnbounded<Action>();

    protected Updater() => _ = RunUpdater(_updateActions.Reader, GetType().Name);

    private static async Task RunUpdater(ChannelReader<Action> actions, string ownerName)
    {
        try
        {
            await foreach (var action in actions.ReadAllAsync())
            {
                try
                {
                    var start = Stopwatch.GetTimestamp();
                    action();
                    var elapsed = Stopwatch.GetElapsedTime(start);
                    if (elapsed.TotalMilliseconds >= SlowActionMillis)
                    {
                        Console.WriteLine($"Slow action: {ownerName} queue, {DescribeAction(action)} took " +
                                          $"{elapsed.TotalMilliseconds:F0}ms, {actions.Count} queued");
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
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
    
    /// Lambdas compile to "&lt;OriginatingMethod&gt;b__0" inside a "&lt;&gt;c__DisplayClass" nested in the real type,
    /// so unwrap both to get back something like "GameInstance.UnitMoved".
    private static string DescribeAction(Action action)
    {
        var method = action.Method;
        var name = method.Name;
        if (name.StartsWith('<') && name.IndexOf('>') > 1)
            name = name[1..name.IndexOf('>')];

        var type = method.DeclaringType;
        while (type is not null && type.Name.StartsWith('<'))
            type = type.DeclaringType;

        return type is null ? name : $"{type.Name}.{name}";
    }

    public bool EnqueueAction(Action func) => _updateActions.Writer.TryWrite(func);

    public void Stop() => _updateActions.Writer.TryComplete();
}