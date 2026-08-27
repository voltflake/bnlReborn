using BNLReloadedServer.ProtocolHelpers;

namespace BNLReloadedServer.BaseTypes;

public class AbilityBehaviorCast : AbilityBehavior
{
    public override AbilityBehaviorType Type => AbilityBehaviorType.Cast;

    public AbilityApplication? Application { get; set; }

    public InstEffect? HitEffect { get; set; }

    public override void Write(BinaryWriter writer)
    {
        new BitField(true, HitEffect != null).Write(writer);
        AbilityApplication.WriteVariant(writer, Application!);
        if (HitEffect == null)
            return;
        InstEffect.WriteVariant(writer, HitEffect);
    }

    public override void Read(BinaryReader reader)
    {
        var bitField = new BitField(2);
        bitField.Read(reader);
        Application = !bitField[0] ? null : AbilityApplication.ReadVariant(reader);
        HitEffect = bitField[1] ? InstEffect.ReadVariant(reader) : null;
    }

    public static void WriteRecord(BinaryWriter writer, AbilityBehaviorCast value)
    {
        value.Write(writer);
    }

    public static AbilityBehaviorCast ReadRecord(BinaryReader reader)
    {
        var abilityBehaviorCast = new AbilityBehaviorCast();
        abilityBehaviorCast.Read(reader);
        return abilityBehaviorCast;
    }
}
