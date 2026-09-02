namespace BNLReloadedServer.Logging;

public static class TaskLoggingExtensions
{
    public static void ObserveFailure(this Task task, LogCat category, string message)
    {
        _ = task.ContinueWith(
            completed => Log.Error(category, message, completed.Exception!.GetBaseException()),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }
}
