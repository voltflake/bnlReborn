using System.Text.Json.Serialization;

namespace BNLReloadedServer.BaseTypes;

public enum UnitLabel
{
    Objective = 1,
    ShieldGenerator = 2,
    ShieldGeneratorDestroyed = 3,
    DropPointResource = 4,
    DropPointBlockbuster = 5,
    DropPointBase = 6,
    SupplyResource = 7,
    SupplyBlockbuster = 8,
    Base = 9,
    BaseTutorial = 10,
    [JsonStringEnumMemberName("line_1")]
    Line1 = 11,
    [JsonStringEnumMemberName("line_2")]
    Line2 = 12,
    [JsonStringEnumMemberName("line_3")]
    Line3 = 13,
    LineBase = 14,
    NinjaNest = 15,
    Repairable = 16,
    [JsonStringEnumMemberName("srv2_objective_1")]
    Srv2Objective1 = 17,
    [JsonStringEnumMemberName("srv2_objective_2")]
    Srv2Objective2 = 18,
    HealthSupply = 19,
    AmmmoSupply = 20,
    RespawnPoint = 21,
    NoBuildZone = 22,
    AstroDisc = 23,
    TutorialCheckpoint = 24,
    DestroyOnMatchEnd = 25,
    EngineerTurret = 26,
    ObjectiveCapturer = 27,
    IgnoreFriendlyFireCrosshair = 28,
    PlayerDamageSource = 29,
    DisabledInBuildPhase = 30
}