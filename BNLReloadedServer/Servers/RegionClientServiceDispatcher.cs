using BNLReloadedServer.Database;
using BNLReloadedServer.Service;
using BNLReloadedServer.Logging;

namespace BNLReloadedServer.Servers;

public class RegionClientServiceDispatcher(ISender sender) : IServiceDispatcher
{
    public ServiceRegionServer ServiceRegionServer { get; } = new(sender);

    private static bool OnUnsupported(ServiceId? serviceId)
    {
        Log.Warn(LogCat.Net, $"Region client session received unsupported serviceId: {serviceId}");
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

        Log.Debug(LogCat.Net, $"Service ID: {serviceEnum.ToString()}");

        return serviceEnum switch
        {
            ServiceId.ServiceServer => ServiceRegionServer.Receive(reader),
            _ => OnUnsupported(serviceEnum)
        };
    }
}