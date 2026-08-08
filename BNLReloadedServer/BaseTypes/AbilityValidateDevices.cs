using BNLReloadedServer.ProtocolHelpers;

namespace BNLReloadedServer.BaseTypes;

public class AbilityValidateDevices : AbilityValidate
{
    public override AbilityValidateType Type => AbilityValidateType.Devices;

    public EffectTargeting? DeviceTargeting { get; set; }

    public bool OwnedOnly { get; set; }

    public int MinCount { get; set; }

    public float? Range { get; set; }

    public override void Write(BinaryWriter writer)
    {
      new BitField(DeviceTargeting != null, true, true, Range.HasValue).Write(writer);
      if (DeviceTargeting != null)
        EffectTargeting.WriteRecord(writer, DeviceTargeting);
      writer.Write(OwnedOnly);
      writer.Write(MinCount);
      if (!Range.HasValue)
        return;
      writer.Write(Range.Value);
    }

    public override void Read(BinaryReader reader)
    {
      var bitField = new BitField(4);
      bitField.Read(reader);
      DeviceTargeting = bitField[0] ? EffectTargeting.ReadRecord(reader) : null;
      if (bitField[1])
        OwnedOnly = reader.ReadBoolean();
      if (bitField[2])
        MinCount = reader.ReadInt32();
      Range = bitField[3] ? reader.ReadSingle() : null;
    }

    public static void WriteRecord(BinaryWriter writer, AbilityValidateDevices value)
    {
      value.Write(writer);
    }

    public static AbilityValidateDevices ReadRecord(BinaryReader reader)
    {
      var abilityValidateDevices = new AbilityValidateDevices();
      abilityValidateDevices.Read(reader);
      return abilityValidateDevices;
    }
}