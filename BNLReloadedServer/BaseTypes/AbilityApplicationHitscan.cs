using BNLReloadedServer.ProtocolHelpers;

namespace BNLReloadedServer.BaseTypes;

public class AbilityApplicationHitscan : AbilityApplication
{
  public override AbilityApplicationType Type => AbilityApplicationType.Hitscan;

    public float Range { get; set; }

    public bool HitOnOutOfRange { get; set; }

    public bool IncludeTargetUnit { get; set; } = true;

    public MultipleBullets? Bullets { get; set; }

    public override void Write(BinaryWriter writer)
    {
      new BitField(true, true, true, Bullets != null).Write(writer);
      writer.Write(Range);
      writer.Write(HitOnOutOfRange);
      writer.Write(IncludeTargetUnit);
      if (Bullets == null)
        return;
      MultipleBullets.WriteRecord(writer, Bullets);
    }

    public override void Read(BinaryReader reader)
    {
      var bitField = new BitField(4);
      bitField.Read(reader);
      if (bitField[0])
        Range = reader.ReadSingle();
      if (bitField[1])
        HitOnOutOfRange = reader.ReadBoolean();
      if (bitField[2])
        IncludeTargetUnit = reader.ReadBoolean();
      if (bitField[3])
        Bullets = MultipleBullets.ReadRecord(reader);
      else
        Bullets = null;
    }

    public static void WriteRecord(BinaryWriter writer, AbilityApplicationHitscan value)
    {
      value.Write(writer);
    }

    public static AbilityApplicationHitscan ReadRecord(BinaryReader reader)
    {
      var applicationHitscan = new AbilityApplicationHitscan();
      applicationHitscan.Read(reader);
      return applicationHitscan;
    }
}