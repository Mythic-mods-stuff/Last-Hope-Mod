#nullable enable
using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace LastHopeAlt.Common;

public enum SelectionMode : byte { Automatic, Manual, Disabled }
public enum AbilityKind : byte { Wings, Dash, Transformation, ActiveAbility, Music }

public sealed class AbilityChoice
{
    [DefaultValue(SelectionMode.Automatic)]
    public SelectionMode Mode;
    public ItemDefinition Accessory = new();
    public override string ToString() => Mode == SelectionMode.Manual ? Accessory.ToString() : Mode.ToString();
}

public sealed class HopeConfig : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ClientSide;
    [DefaultValue(true)]
    public bool EnableFlight = true;
    public List<ItemDefinition> DisabledEffects = new();

    public HopeOptions Snapshot()
    {
        var options = new HopeOptions();
        options.Modes[(int)AbilityKind.Wings] = EnableFlight ? SelectionMode.Automatic : SelectionMode.Disabled;
        for (int i = 1; i < options.Modes.Length; i++) options.Modes[i] = SelectionMode.Disabled;
        options.Disabled.UnionWith(DisabledEffects.Select(x => x.Type).Where(AccessoryCatalog.Contains));
        return options;
    }
}

public sealed class HopeOptions
{
    public SelectionMode[] Modes = new SelectionMode[5];
    public int[] Items = new int[5];
    public HashSet<int> Disabled = new();

    public bool SameAs(HopeOptions other) => Modes.SequenceEqual(other.Modes)
        && Items.SequenceEqual(other.Items) && Disabled.SetEquals(other.Disabled);

    public void Write(BinaryWriter writer)
    {
        for (int i = 0; i < 5; i++)
        {
            writer.Write((byte)Modes[i]);
            writer.Write(Items[i]);
        }
        writer.Write((ushort)Disabled.Count);
        foreach (int type in Disabled.Order()) writer.Write(type);
    }

    public static HopeOptions Read(BinaryReader reader)
    {
        var options = new HopeOptions();
        for (int i = 0; i < 5; i++)
        {
            byte mode = reader.ReadByte();
            options.Modes[i] = mode <= 2 ? (SelectionMode)mode : SelectionMode.Automatic;
            int type = reader.ReadInt32();
            options.Items[i] = AccessoryCatalog.Contains(type) ? type : 0;
        }
        int count = reader.ReadUInt16();
        if (count > AccessoryCatalog.Entries.Count) throw new InvalidDataException("Invalid effect selection count");
        for (int i = 0; i < count; i++)
        {
            int type = reader.ReadInt32();
            if (AccessoryCatalog.Contains(type)) options.Disabled.Add(type);
        }
        return options;
    }
}
