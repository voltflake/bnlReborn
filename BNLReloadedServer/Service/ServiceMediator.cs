using BNLReloadedServer.Servers;
using BNLReloadedServer.Logging;

namespace BNLReloadedServer.Service;

public class ServiceMediator(ISender sender) : IServiceMediator
{
    private enum ServiceMediatorId : byte
    {
        MessageEnableDisconnect = 0
    }

    private static BinaryWriter CreateWriter()
    {
        var memStream = new MemoryStream();
        var writer = new BinaryWriter(memStream);
        writer.Write((byte)ServiceId.ServiceMediator);
        return writer;
    }

    public void SendEnableDisconnect()
    {
        using var writer = CreateWriter();
        writer.Write((byte)ServiceMediatorId.MessageEnableDisconnect);
        sender.Send(writer);
    }

    public bool Receive(BinaryReader reader)
    {
        var serviceMediatorId = reader.ReadByte();
        Log.Debug(LogCat.Net, $"ServiceMediatorId: {serviceMediatorId}");
        Log.Warn(LogCat.Net, $"Mediator service received unsupported serviceId: {serviceMediatorId}");
        return false;
    }

}
