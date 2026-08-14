using BNLReloadedServer.BaseTypes;
using BNLReloadedServer.Database;
using BNLReloadedServer.ProtocolHelpers;
using BNLReloadedServer.Servers;
using BNLReloadedServer.Logging;

namespace BNLReloadedServer.Service;

public class ServiceLeaderboard(ISender sender) : IServiceLeaderboard
{
    private enum ServiceLeaderboardId : byte
    {
        MessageGetTimeTrialLeaderboard = 0,
        MessageGetLeagueLeaderboard = 1
    }
    
    private static BinaryWriter CreateWriter()
    {
        var memStream = new MemoryStream();
        var writer = new BinaryWriter(memStream);
        writer.Write((byte)ServiceId.ServiceLeaderboard);
        return writer;
    }

    public void SendGetTimeTrialLeaderboard(ushort rpcId, Dictionary<Key, List<TtLeaderboardRecord>>? data,
        ELeaderboardUpdateCooldown? eLeaderboardUpdateCooldown = null, string? error = null)
    {
        using var writer = CreateWriter();
        writer.Write((byte)ServiceLeaderboardId.MessageGetTimeTrialLeaderboard);
        writer.Write(rpcId);
        if (data != null)
        {
            writer.Write((byte) 0);
            writer.WriteMap(data, Key.WriteRecord, item => writer.WriteList(item, TtLeaderboardRecord.WriteRecord));
        }
        else if (eLeaderboardUpdateCooldown != null)
        {
            writer.Write((byte)1);
            ELeaderboardUpdateCooldown.WriteRecord(writer, eLeaderboardUpdateCooldown);
        }
        else
        {
            writer.Write(byte.MaxValue);
            writer.Write(error!);
        }
        sender.Send(writer);
    }

    private void ReceiveGetTimeTrialLeaderboard(BinaryReader reader)
    {
        var rpcId = reader.ReadUInt16();

        var result = Databases.PlayerDatabase.GetTimeTrialLeaderboard().Result;
        if (result == null)
        {
            SendGetTimeTrialLeaderboard(rpcId, null, new ELeaderboardUpdateCooldown());
            return;
        }

        // The panel indexes this by every course map it finds in the catalogue, so a course nobody
        // has finished yet has to come back as an empty board rather than a missing key.
        var boards = new Dictionary<Key, List<TtLeaderboardRecord>>();
        foreach (var course in CatalogueHelper.GlobalLogic.TimeTrial?.Courses ?? [])
        {
            boards[course.Map] = result.GetValueOrDefault(course.Map) ?? [];
        }

        SendGetTimeTrialLeaderboard(rpcId, boards);
    }

    public void SendGetLeagueLeaderboard(ushort rpcId, List<LeagueLeaderboardRecord>? data, ELeaderboardUpdateCooldown? eLeaderboardUpdateCooldown = null,
        string? error = null)
    {
        using var writer = CreateWriter();
        writer.Write((byte)ServiceLeaderboardId.MessageGetLeagueLeaderboard);
        writer.Write(rpcId);
        if (data != null)
        {
            writer.Write((byte) 0);
            writer.WriteList(data, LeagueLeaderboardRecord.WriteRecord);
        }
        else if (eLeaderboardUpdateCooldown != null)
        {
            writer.Write((byte)1);
            ELeaderboardUpdateCooldown.WriteRecord(writer, eLeaderboardUpdateCooldown);
        }
        else
        {
            writer.Write(byte.MaxValue);
            writer.Write(error!);
        }
        sender.Send(writer);
    }

    private void ReceiveGetLeagueLeaderboard(BinaryReader reader)
    {
        var rpcId = reader.ReadUInt16();

        var result = Databases.PlayerDatabase.GetLeaderboard().Result;
        if (result != null)
        {
            SendGetLeagueLeaderboard(rpcId, result);
        }
        else
        {
            SendGetLeagueLeaderboard(rpcId, null, new ELeaderboardUpdateCooldown());
        }
    }
    
    public bool Receive(BinaryReader reader)
    {
        var serviceLeaderboardId = reader.ReadByte();
        ServiceLeaderboardId? leaderboardEnum = null;
        if (Enum.IsDefined(typeof(ServiceLeaderboardId), serviceLeaderboardId))
        {
            leaderboardEnum = (ServiceLeaderboardId)serviceLeaderboardId;
        }

        Log.Debug(LogCat.Net, $"ServiceLeaderboardId: {Log.EnumName(leaderboardEnum, serviceLeaderboardId)}");

        switch (leaderboardEnum)
        {
            case ServiceLeaderboardId.MessageGetTimeTrialLeaderboard:
                ReceiveGetTimeTrialLeaderboard(reader);
                break;
            case ServiceLeaderboardId.MessageGetLeagueLeaderboard:
                ReceiveGetLeagueLeaderboard(reader);
                break;
            default:
                Log.Warn(LogCat.Net, $"Unknown service leaderboard id {Log.EnumName(leaderboardEnum, serviceLeaderboardId)}");
                return false;
        }
        
        return true;
    }
}