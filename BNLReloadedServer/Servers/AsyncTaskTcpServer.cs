using System.Collections.Concurrent;
using System.Net;
using NetCoreServer;

namespace BNLReloadedServer.Servers;

public class AsyncTaskTcpServer(IPAddress address, int port) : TcpServer(address, port)
{
    // Written from session construction on the accept thread and from disconnect teardown on
    // whichever thread got there, read from game logic threads: has to be concurrent.
    private readonly ConcurrentDictionary<Guid, AsyncSenderTask> _senderTasks = new();

    public void AddSenderTask(Guid senderId, AsyncSenderTask task) =>
        _senderTasks.AddOrUpdate(senderId, task, (_, existing) =>
        {
            existing.Stop();
            return task;
        });

    private void RemoveSenderTask(Guid senderId)
    {
        if (_senderTasks.TryRemove(senderId, out var value))
        {
            value.Stop();
        }
    }

    protected override void OnDisconnected(TcpSession session)
    {
        RemoveSenderTask(session.Id);
    }

    public AsyncSenderTask? FindAsyncSenderTask(Guid guid) => _senderTasks.GetValueOrDefault(guid);
}
