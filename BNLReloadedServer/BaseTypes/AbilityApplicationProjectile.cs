using BNLReloadedServer.ProtocolHelpers;

namespace BNLReloadedServer.BaseTypes;

public class AbilityApplicationProjectile : AbilityApplication
{
    public override AbilityApplicationType Type => AbilityApplicationType.Projectile;

    public Key ProjectileKey { get; set; }

    public float Speed { get; set; }

    public MultipleBullets? Bullets { get; set; }

    public override void Write(BinaryWriter writer)
    {
        new BitField(true, true, Bullets != null).Write(writer);
        Key.WriteRecord(writer, ProjectileKey);
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
            ProjectileKey = Key.ReadRecord(reader);
        if (bitField[1])
            Speed = reader.ReadSingle();
        Bullets = bitField[2] ? MultipleBullets.ReadRecord(reader) : null;
    }

    public static void WriteRecord(BinaryWriter writer, AbilityApplicationProjectile value)
    {
        value.Write(writer);
    }

    public static AbilityApplicationProjectile ReadRecord(BinaryReader reader)
    {
        var applicationProjectile = new AbilityApplicationProjectile();
        applicationProjectile.Read(reader);
        return applicationProjectile;
    }
}
