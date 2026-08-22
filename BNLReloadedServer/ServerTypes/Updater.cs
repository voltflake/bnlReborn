using System.Diagnostics;
using System.Threading.Channels;
using BNLReloadedServer.Logging;

namespace BNLReloadedServer.ServerTypes;

public abstract class Updater
{
    private const int SlowActionMillis = 25;
    private const int StuckActionMillis = 5000;
    private const long LargeActionAllocationBytes = 1024 * 1024;

    private readonly Channel<Action> _updateActions = Channel.CreateUnbounded<Action>();
    private readonly object _watchdogLock = new();
    private readonly System.Threading.Timer _watchdog;
    private Action? _currentAction;
    private long _currentActionStarted;

    protected string DiagnosticName { get; set; }
    protected int QueuedActionCount => _updateActions.Reader.Count;

    protected Updater()
    {
        DiagnosticName = GetType().Name;
        _watchdog = new System.Threading.Timer(ReportStuckAction, null, Timeout.Infinite, Timeout.Infinite);
        _ = RunUpdater(_updateActions.Reader);
    }

    private async Task RunUpdater(ChannelReader<Action> actions)
    {
        try
        {
            await foreach (var action in actions.ReadAllAsync())
            {
                try
                {
                    if (TrackQueuedActionSources)
                        ActionDequeued(action.Method);
                    var start = Stopwatch.GetTimestamp();
                    var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                    lock (_watchdogLock)
                    {
                        _currentAction = action;
                        _currentActionStarted = start;
                        _watchdog.Change(StuckActionMillis, Timeout.Infinite);
                    }
                    try
                    {
                        action();
                    }
                    finally
                    {
                        ClearWatchdog();
                    }
                    var elapsed = Stopwatch.GetElapsedTime(start);
                    var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
                    if (elapsed.TotalMilliseconds >= SlowActionMillis || allocated >= LargeActionAllocationBytes)
                    {
                        var kind = elapsed.TotalMilliseconds >= SlowActionMillis ? "Slow action" : "Allocation-heavy action";
                        Log.Info(LogCat.Perf, $"{kind}: {DiagnosticName} queue, {DescribeAction(action)} took " +
                                              $"{elapsed.TotalMilliseconds:F0}ms, allocated {allocated / 1024d:F0}KB, " +
                                              $"{actions.Count} queued");
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception e)
                {
                    Log.Error(LogCat.Server, $"Queued action failed on the {DiagnosticName} queue", e);
                }
            }
        }
        catch (OperationCanceledException)
        {

        }
        catch (Exception e)
        {
            Log.Error(LogCat.Server, $"{DiagnosticName} update loop stopped", e);
        }
        finally
        {
            _watchdog.Dispose();
        }
    }

    private void ReportStuckAction(object? _)
    {
        string? message = null;
        lock (_watchdogLock)
        {
            if (_currentAction is null) return;
            var elapsed = Stopwatch.GetElapsedTime(_currentActionStarted);
            message = $"Stuck action: {DiagnosticName} queue, {DescribeAction(_currentAction)} still running " +
                      $"after {elapsed.TotalSeconds:F1}s, {_updateActions.Reader.Count} queued";
        }
        Log.Error(LogCat.Perf, message);
    }

    private void ClearWatchdog()
    {
        lock (_watchdogLock)
        {
            _watchdog.Change(Timeout.Infinite, Timeout.Infinite);
            _currentAction = null;
        }
    }
    
    private static string DescribeAction(Action action)
    {
        return DescribeMethod(action.Method);
    }

    protected static string DescribeMethod(System.Reflection.MethodInfo method)
    {
        var name = method.Name;
        if (name.StartsWith('<') && name.IndexOf('>') > 1)
            name = name[1..name.IndexOf('>')];

        var type = method.DeclaringType;
        while (type is not null && type.Name.StartsWith('<'))
            type = type.DeclaringType;

        return type is null ? name : $"{type.Name}.{name}";
    }

    protected virtual bool TrackQueuedActionSources => false;

    protected virtual void ActionQueued(System.Reflection.MethodInfo method) { }

    protected virtual void ActionDequeued(System.Reflection.MethodInfo method) { }

    protected virtual string DescribeQueuedActionSources() => string.Empty;

    public virtual bool EnqueueAction(Action func)
    {
        if (!TrackQueuedActionSources)
            return _updateActions.Writer.TryWrite(func);

        ActionQueued(func.Method);
        if (_updateActions.Writer.TryWrite(func)) return true;
        ActionDequeued(func.Method);
        return false;
    }

    public void Stop()
    {
        if (_updateActions.Writer.TryComplete())
            ClearWatchdog();
    }
}
