using SQLite;

namespace BNLReloadedServer.Database;

[Table("PlayerPresence")]
public class PlayerPresenceRecord
{
    [PrimaryKey]
    [Column("player_id")]
    public uint PlayerId { get; set; }

    [Column("last_online")]
    public DateTimeOffset? LastOnline { get; set; }
}
