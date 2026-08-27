namespace BNLReloadedServer.Service;

public interface IServicePing : IService
{
    public void SendServerPing();
    public int SendLivenessProbe();
    public void SendClientPong();
}
