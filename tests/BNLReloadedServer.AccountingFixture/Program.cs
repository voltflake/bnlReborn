using BNLReloadedServer.BaseTypes;

static void Equal<T>(T expected, T actual, string scenario) where T : notnull
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{scenario}: expected {expected}, got {actual}");
    }
}

// Authoritative source stamping, including client impacts which arrive without
// the server-only field.
Equal(DamageCreditType.Hero,
    DamageAccounting.ResolveSourceCredit(DamageCreditType.Hero, true, DamageCreditType.Block),
    "existing impact credit is immutable");
Equal(DamageCreditType.Block,
    DamageAccounting.ResolveSourceCredit(DamageCreditType.None, true, DamageCreditType.Hero),
    "block source stamps block credit");
Equal(DamageCreditType.Hero,
    DamageAccounting.ResolveSourceCredit(DamageCreditType.None, false, DamageCreditType.Hero),
    "player source stamps hero credit");

// Spawn chains cover direct shots, hero AoE, constructed devices, and AoE
// spawned by those devices. Context is required because Doc's gas-cloud unit is
// shared by both hero abilities and the gas trap.
Equal(DamageCreditType.Hero,
    DamageAccounting.ResolveSpawnCredit(false, DeviceType.None, false, DamageCreditType.Hero),
    "hero projectile/AoE inherits hero credit");
Equal(DamageCreditType.Block,
    DamageAccounting.ResolveSpawnCredit(true, DeviceType.None, false, DamageCreditType.Hero),
    "built device overrides a catalogue card with no device type");
Equal(DamageCreditType.Block,
    DamageAccounting.ResolveSpawnCredit(false, DeviceType.None, false, DamageCreditType.Block),
    "device-spawned AoE inherits block credit");
Equal(DamageCreditType.Block,
    DamageAccounting.ResolveSpawnCredit(false, DeviceType.Device, false, DamageCreditType.Hero),
    "device catalogue type receives block credit");
Equal(DamageCreditType.Hero,
    DamageAccounting.ResolveSpawnCredit(false, DeviceType.Device, true, DamageCreditType.Hero),
    "owner-hit device inherits hero credit");
Equal(DamageCreditType.Block,
    DamageAccounting.ResolveSpawnCredit(false, DeviceType.Device, true, DamageCreditType.Block),
    "owner-hit child preserves block owner credit");

// Legacy fallback remains valid for impacts that do not pass through an effect
// source (or were produced by older code paths).
Equal(DamageCreditType.Hero,
    DamageAccounting.ResolveImpactCredit(DamageCreditType.None, true, DeviceType.None),
    "legacy player fallback");
Equal(DamageCreditType.Block,
    DamageAccounting.ResolveImpactCredit(DamageCreditType.None, false, DeviceType.Device),
    "legacy device fallback");
Equal(DamageCreditType.Hero,
    DamageAccounting.ResolveImpactCredit(DamageCreditType.Hero, false, DeviceType.Device),
    "recorded provenance wins after the source unit disappears");

// Score only what was removed, never requested overkill.
Equal(25f, DamageAccounting.Removed(25f, 0f), "health overkill is capped");
Equal(0f, DamageAccounting.Removed(0f, 0f), "zero damage stays zero");
var shieldAndHealthRemoved = DamageAccounting.Removed(20f, 0f) + DamageAccounting.Removed(100f, 85f);
Equal(35f, shieldAndHealthRemoved, "shield spillover counts both actual pools once");

// Friendly fire remains visible in friendly-fire stats but must not increase
// the attacker's positive hero/block damage counters.
Equal(false, DamageAccounting.IsEnemyPlayerDamage(TeamType.Team1, TeamType.Team1),
    "friendly damage is not positive credited damage");
Equal(true, DamageAccounting.IsEnemyPlayerDamage(TeamType.Team2, TeamType.Team1),
    "enemy damage is positive credited damage");

Console.WriteLine("Damage accounting fixture passed (17 assertions).");
