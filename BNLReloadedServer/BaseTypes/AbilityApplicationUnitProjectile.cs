using BNLReloadedServer.ProtocolHelpers;

namespace BNLReloadedServer.BaseTypes;

public class AbilityApplicationUnitProjectile : AbilityApplication
{
    public override AbilityApplicationType Type => AbilityApplicationType.UnitProjectile;

    public Key UnitProjectileKey { get; set; }

    public float Speed { get; set; }

    public MultipleBullets? Bullets { get; set; }

    public override void Write(BinaryWriter writer)
    {
        new BitField(true, true, Bullets != null).Write(writer);
        Key.WriteRecord(writer, UnitProjectileKey);
        writer.Write(Speed);
        if (Bullets == null)
            return;
        MultipleBullets.WriteRecord(writer, Bullets);
    }

    public override void Read(BinaryReader reader)
    {
        var bitField = new BitField(3);
        bitField.Read(reader);
        if (bitField[0])
            UnitProjectileKey = Key.ReadRecord(reader);
        if (bitField[1])
            Speed = reader.ReadSingle();
        Bullets = bitField[2] ? MultipleBullets.ReadRecord(reader) : null;
    }

    public static void WriteRecord(BinaryWriter writer, AbilityApplicationUnitProjectile value)
    {
        value.Write(writer);
    }

    public static AbilityApplicationUnitProjectile ReadRecord(BinaryReader reader)
    {
        var applicationUnitProjectile = new AbilityApplicationUnitProjectile();
        applicationUnitProjectile.Read(reader);
        return applicationUnitProjectile;
    }
}
