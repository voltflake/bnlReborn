using BNLReloadedServer.Database;
using BNLReloadedServer.Service;
using BNLReloadedServer.Logging;

namespace BNLReloadedServer.Servers;
public class MasterServiceDispatcher(ISender sender, Guid sessionId) : IServiceDispatcher
{
    private readonly ServiceLogin _serviceLogin = new(sender, sessionId);
    private readonly ServiceMasterServer _serviceMasterServer = new(sender, sessionId);

    private static bool OnUnsupported(ServiceId? serviceEnum, byte raw)
    {
        Log.Warn(LogCat.Net, $"Master session received unsupported serviceId: {Log.EnumName(serviceEnum, raw)}");
        return false;
    }
    
    public bool Dispatch(BinaryReader reader)
    {
        var serviceId = reader.ReadByte();
        ServiceId? serviceEnum = null;
        if (Enum.IsDefined(typeof(ServiceId), serviceId))
        {
            serviceEnum = (ServiceId)serviceId;
        }

        Log.Debug(LogCat.Net, $"Service ID: {Log.EnumName(serviceEnum, serviceId)}");

        return serviceEnum switch
        {
            ServiceId.ServiceLogin => _serviceLogin.Receive(reader),
            ServiceId.ServiceServer => _serviceMasterServer.Receive(reader),
            _ => OnUnsupported(serviceEnum, serviceId)
        };
    }
}