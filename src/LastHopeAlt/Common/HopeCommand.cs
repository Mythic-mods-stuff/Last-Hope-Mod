#nullable enable
using System.Text;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace LastHopeAlt.Common;

public sealed class HopeCommand : ModCommand
{
    public override string Command => "lasthope";
    public override CommandType Type => CommandType.Chat;
    public override string Usage => "/lasthope catalog | abilities";
    public override string Description => "Export the accessory checklist or show your selected relic abilities.";

    public override void Action(CommandCaller caller, string input, string[] args)
    {
        if (args.Length == 1 && args[0].Equals("catalog", StringComparison.OrdinalIgnoreCase))
        {
            string directory = Path.Combine(Main.SavePath, "LastHope");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "accessories.csv");
            var csv = new StringBuilder("Key,Name,Source\n");
            foreach (AccessoryEntry entry in AccessoryCatalog.Entries)
                csv.AppendLine($"\"{entry.Key}\",\"{entry.Name.Replace("\"", "\"\"")}\",{(entry.Calamity ? "Calamity" : "Terraria")}");
            File.WriteAllText(path, csv.ToString());
            caller.Reply($"Exported {AccessoryCatalog.Entries.Count} required accessories to {path}");
            return;
        }
        if (args.Length == 1 && args[0].Equals("abilities", StringComparison.OrdinalIgnoreCase))
        {
            Bonus bonus = NumericStats.Total(ModContent.GetInstance<HopeConfig>().Snapshot());
            caller.Reply($"The Last Hope Alt grants numerical stats and ordinary flight only ({bonus.Flight / 60f:0.##} seconds). Hover over the relic for its stat bonuses.");
            return;
        }
        caller.Reply(Usage);
    }
}
