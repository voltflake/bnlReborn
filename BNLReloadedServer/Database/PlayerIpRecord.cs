using SQLite;

namespace BNLReloadedServer.Database;

// One row per address a player has logged in from, rather than one per login: a player who
// reconnects all day bumps Hits instead of burying the table. Both columns are indexed because
// the lookup runs in both directions — which addresses a player used, and who else used one.
[Table("PlayerIps")]
public class PlayerIpRecord
{
    [PrimaryKey, AutoIncrement]
    [Column("id")]
    public int Id { get; set; }

    [Indexed]
    [Column("player_id")]
    public uint PlayerId { get; set; }

    [Indexed]
    [Column("ip")]
    public string Ip { get; set; } = string.Empty;

    [Column("first_seen")]
    public DateTimeOffset FirstSeen { get; set; }

    [Column("last_seen")]
    public DateTimeOffset LastSeen { get; set; }

    [Column("hits")]
    public int Hits { get; set; }
}
