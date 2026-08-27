using BNLReloadedServer.ProtocolHelpers;

namespace BNLReloadedServer.BaseTypes;

public enum MatchJoinKind : byte { Initial, Backfill, Reconnect }
public enum MatchLeaveKind : byte { MatchEnded, Disconnect, Quit, Inactivity, Kicked }
public enum MatchEndReason : byte { Unknown, ObjectivesDestroyed, Surrender, ObjectivesCompleted, Abandoned }

public sealed class CompletedMatchPresence
{
    public int Sequence { get; set; }
    public ulong JoinedAt { get; set; }
    public ulong? LeftAt { get; set; }
    public MatchJoinKind JoinKind { get; set; }
    public MatchLeaveKind? LeaveKind { get; set; }
    public TeamType Team { get; set; }
    public Key HeroKey { get; set; }
    public Key SkinKey { get; set; }
    public Dictionary<int, Key> Devices { get; set; } = [];
    public List<Key> Perks { get; set; } = [];
    public Dictionary<Key, int> DeviceLevels { get; set; } = [];
}

public sealed class CompletedMatchPlayer
{
    public uint PlayerId { get; set; }
    public string Nickname { get; set; } = string.Empty;
    public ulong? SquadId { get; set; }
    public bool WasInitial { get; set; }
    public bool WasBackfiller { get; set; }
    public bool IsWinner { get; set; }
    // Rating means are stored in their native TrueSkill scale.  The control panel formats them
    // in display MMR points, just as it does everywhere else.
    public double? StartingRatingMean { get; set; }
    public double? RatingDelta { get; set; }
    public Dictionary<PlayerMatchStatType, int> Stats { get; set; } = [];
    public int TotalScore { get; set; }
    public List<CompletedMatchPresence> Presences { get; set; } = [];
}

public sealed class CompletedMatchTeam
{
    public TeamType Team { get; set; }
    public bool IsWinner { get; set; }
    public int CubesAtStart { get; set; }
    public int CubesRemaining { get; set; }
    public bool BaseDestroyed { get; set; }
}

public sealed class CompletedMatchRecord
{
    public string MatchId { get; set; } = string.Empty;
    public Key MapKey { get; set; }
    public Key GameModeKey { get; set; }
    public ulong StartedAt { get; set; }
    public ulong EndedAt { get; set; }
    public TeamType Winner { get; set; }
    public MatchEndReason EndReason { get; set; }
    public List<CompletedMatchTeam> Teams { get; set; } = [];
    public List<CompletedMatchPlayer> Players { get; set; } = [];

    public void Write(BinaryWriter writer)
    {
        writer.Write(MatchId);
        MapKey.Write(writer);
        GameModeKey.Write(writer);
        writer.Write(StartedAt);
        writer.Write(EndedAt);
        writer.WriteByteEnum(Winner);
        writer.WriteList(Teams, (w, t) =>
        {
            w.WriteByteEnum(t.Team); w.Write(t.IsWinner); w.Write(t.CubesAtStart);
            w.Write(t.CubesRemaining); w.Write(t.BaseDestroyed);
        });
        writer.WriteList(Players, (w, p) =>
        {
            w.Write(p.PlayerId); w.Write(p.Nickname); w.Write(p.SquadId.HasValue);
            if (p.SquadId.HasValue) w.Write(p.SquadId.Value);
            w.Write(p.WasInitial); w.Write(p.WasBackfiller); w.Write(p.IsWinner);
            w.WriteMap(p.Stats, w.WriteByteEnum, w.Write); w.Write(p.TotalScore);
            w.WriteList(p.Presences, (pw, presence) =>
            {
                pw.Write(presence.Sequence); pw.Write(presence.JoinedAt); pw.Write(presence.LeftAt.HasValue);
                if (presence.LeftAt.HasValue) pw.Write(presence.LeftAt.Value);
                pw.WriteByteEnum(presence.JoinKind); pw.Write(presence.LeaveKind.HasValue);
                if (presence.LeaveKind.HasValue) pw.WriteByteEnum(presence.LeaveKind.Value);
                pw.WriteByteEnum(presence.Team); presence.HeroKey.Write(pw); presence.SkinKey.Write(pw);
                pw.WriteMap(presence.Devices, pw.Write, Key.WriteRecord);
                pw.WriteList(presence.Perks, Key.WriteRecord);
                pw.WriteMap(presence.DeviceLevels, Key.WriteRecord, pw.Write);
            });
        });
        // Appended so an old master can safely ignore it and a new master can still accept an
        // archive sent by an older region during a rolling restart.
        writer.WriteMap(Players.Where(player => player.StartingRatingMean.HasValue)
            .ToDictionary(player => player.PlayerId, player => player.StartingRatingMean!.Value), writer.Write, writer.Write);
        writer.WriteByteEnum(EndReason);
    }

    public static CompletedMatchRecord ReadRecord(BinaryReader reader)
    {
        var result = new CompletedMatchRecord
        {
            MatchId = reader.ReadString(),
            MapKey = Key.ReadRecord(reader),
            GameModeKey = Key.ReadRecord(reader),
            StartedAt = reader.ReadUInt64(),
            EndedAt = reader.ReadUInt64(),
            Winner = reader.ReadByteEnum<TeamType>()
        };
        result.Teams = reader.ReadList<CompletedMatchTeam, List<CompletedMatchTeam>>(() => new CompletedMatchTeam
        {
            Team = reader.ReadByteEnum<TeamType>(),
            IsWinner = reader.ReadBoolean(),
            CubesAtStart = reader.ReadInt32(),
            CubesRemaining = reader.ReadInt32(),
            BaseDestroyed = reader.ReadBoolean()
        });
        result.Players = reader.ReadList<CompletedMatchPlayer, List<CompletedMatchPlayer>>(() =>
        {
            var player = new CompletedMatchPlayer { PlayerId = reader.ReadUInt32(), Nickname = reader.ReadString() };
            if (reader.ReadBoolean()) player.SquadId = reader.ReadUInt64();
            player.WasInitial = reader.ReadBoolean(); player.WasBackfiller = reader.ReadBoolean();
            player.IsWinner = reader.ReadBoolean();
            player.Stats = reader.ReadMap<PlayerMatchStatType, int, Dictionary<PlayerMatchStatType, int>>(
                reader.ReadByteEnum<PlayerMatchStatType>, reader.ReadInt32);
            player.TotalScore = reader.ReadInt32();
            player.Presences = reader.ReadList<CompletedMatchPresence, List<CompletedMatchPresence>>(() =>
            {
                var presence = new CompletedMatchPresence { Sequence = reader.ReadInt32(), JoinedAt = reader.ReadUInt64() };
                if (reader.ReadBoolean()) presence.LeftAt = reader.ReadUInt64();
                presence.JoinKind = reader.ReadByteEnum<MatchJoinKind>();
                if (reader.ReadBoolean()) presence.LeaveKind = reader.ReadByteEnum<MatchLeaveKind>();
                presence.Team = reader.ReadByteEnum<TeamType>(); presence.HeroKey = Key.ReadRecord(reader);
                presence.SkinKey = Key.ReadRecord(reader);
                presence.Devices = reader.ReadMap<int, Key, Dictionary<int, Key>>(reader.ReadInt32, Key.ReadRecord);
                presence.Perks = reader.ReadList<Key, List<Key>>(Key.ReadRecord);
                presence.DeviceLevels = reader.ReadMap<Key, int, Dictionary<Key, int>>(Key.ReadRecord, reader.ReadInt32);
                return presence;
            });
            return player;
        });
        if (reader.BaseStream.Position < reader.BaseStream.Length)
        {
            var startingRatings = reader.ReadMap<uint, double, Dictionary<uint, double>>(
                reader.ReadUInt32, reader.ReadDouble);
            foreach (var player in result.Players)
                if (startingRatings.TryGetValue(player.PlayerId, out var rating)) player.StartingRatingMean = rating;
        }
        if (reader.BaseStream.Position < reader.BaseStream.Length)
            result.EndReason = reader.ReadByteEnum<MatchEndReason>();
        return result;
    }
}
