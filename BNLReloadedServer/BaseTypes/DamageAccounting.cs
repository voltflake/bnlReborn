namespace BNLReloadedServer.BaseTypes;

public static class DamageAccounting
{
    public static DamageCreditType ResolveSourceCredit(DamageCreditType currentCredit, bool sourceIsBlock,
        DamageCreditType sourceUnitCredit)
    {
        if (currentCredit != DamageCreditType.None)
        {
            return currentCredit;
        }

        return sourceIsBlock ? DamageCreditType.Block : sourceUnitCredit;
    }

    public static DamageCreditType ResolveSpawnCredit(bool builtDevice, DeviceType deviceType,
        bool treatHitsAsOwnerHits, DamageCreditType ownerCredit)
    {
        if (builtDevice)
        {
            return DamageCreditType.Block;
        }

        if (treatHitsAsOwnerHits && ownerCredit != DamageCreditType.None)
        {
            return ownerCredit;
        }

        return deviceType != DeviceType.None ? DamageCreditType.Block : ownerCredit;
    }

    public static DamageCreditType ResolveImpactCredit(DamageCreditType recordedCredit, bool attackerIsPlayer,
        DeviceType? attackerDeviceType)
    {
        if (recordedCredit != DamageCreditType.None)
        {
            return recordedCredit;
        }

        if (attackerIsPlayer)
        {
            return DamageCreditType.Hero;
        }

        return attackerDeviceType is not null and not DeviceType.None
            ? DamageCreditType.Block
            : DamageCreditType.None;
    }

    public static float Removed(float previous, float current) => MathF.Max(previous - current, 0f);

    public static bool IsEnemyPlayerDamage(TeamType targetTeam, TeamType attackerTeam) =>
        targetTeam != attackerTeam;
}
