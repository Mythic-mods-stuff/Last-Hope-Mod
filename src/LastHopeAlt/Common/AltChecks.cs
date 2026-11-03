#nullable enable
using System.Reflection;
using System.Text.Json;
using LastHopeAlt.Content.Items;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace LastHopeAlt.Common;

public sealed class AltChecks : ModSystem
{
    public override void PostSetupRecipes()
    {
        string? report = Environment.GetEnvironmentVariable("LAST_HOPE_ALT_CHECK_REPORT");
        if (string.IsNullOrEmpty(report)) return;
        try
        {
            Check(Main.dedServ, "Isolated headless checks only");
            Main.maxTilesX = Main.maxTilesY = 180;
            Main.tile = (Tilemap)Activator.CreateInstance(typeof(Tilemap), BindingFlags.Instance | BindingFlags.NonPublic,
                null, new object[] { (ushort)180, (ushort)180 }, null)!;
            Main.worldSurface = 110; Main.rockLayer = 140;
            Main.chest = new Chest[Main.maxChests];
            for (int i = 0; i < Main.player.Length; i++) Main.player[i] = new Player { active = false };
            for (int i = 0; i < Main.item.Length; i++) Main.item[i] = new Item();
            for (int i = 0; i < Main.npc.Length; i++) Main.npc[i] = new NPC();
            for (int i = 0; i < Main.projectile.Length; i++) Main.projectile[i] = new Projectile();
            var player = new Player { whoAmI = 0, active = true, chest = -1, position = new Vector2(90 * 16, 90 * 16), statLifeMax = 500, statLifeMax2 = 500, statLife = 500 };
            Main.player[0] = player;
            Check(AccessoryCatalog.Entries.Count == 644, "Original 644-ingredient collection retained");
            Check(ModLoader.TryGetMod("AccessorySpawner", out var spawner), "Test helper is present");
            string reply = (string)spawner.Code.GetType("AccessorySpawner.SpawnService")!.GetMethod("Spawn", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { player })!;
            Check(reply.StartsWith("Spawned "), reply);
            Point bench = default;
            for (int x = 10; x < 170; x++)
                for (int y = 10; y < 170; y++)
                    if (ForgeService.BenchOrigin(x, y).X >= 0) bench = ForgeService.BenchOrigin(x, y);
            player.Center = new Vector2((bench.X + 1) * 16, (bench.Y - 1) * 16);
            Check(ForgeService.Inspect(player, bench).Complete, "Original chest recipe is complete");
            ForgeService.Craft(player, bench);
            Check(Main.item.Count(i => i.active && i.type == ModContent.ItemType<TheLastHope>()) == 1, "Alt relic crafted successfully");
            var results = new List<object>();
            foreach (bool day in new[] { true, false })
            {
                Main.dayTime = day;
                var p = new Player { whoAmI = 0, active = true, statLifeMax = 500, statLifeMax2 = 500, statLife = 500 };
                Main.player[0] = p;
                p.ConsumedLifeCrystals = 15;
                p.ConsumedLifeFruit = 20;
                p.ConsumedManaCrystals = 9;
                p.ResetEffects();
                p.statLife = p.statLifeMax2;
                HopePlayer hope = p.GetModPlayer<HopePlayer>();
                Bonus total = NumericStats.Total(new HopeOptions());
                var flags = typeof(Player).GetFields().Where(f => f.FieldType == typeof(bool)).ToDictionary(f => f, f => f.GetValue(p));
                object cal = Bonus.Calamity(p)!;
                var calFlags = cal.GetType().GetFields().Where(f => f.FieldType == typeof(bool)).ToDictionary(f => f, f => f.GetValue(cal));
                float startDamage = p.GetDamage(DamageClass.Generic).Additive;
                hope.Activate(false);
                Check(hope.Equipped, "Relic equipped");
                Check(p.wingTimeMax == total.Flight && total.Flight > 0, "Combined flight duration applied");
                Check(p.equippedWings.ModItem == null && p.equippedWings.type == ItemID.WingsSolar, "Ordinary vanilla wings only");
                Check(p.dashType == 0 && !p.wolfAcc && !p.accMerman, "No dash or transformations");
                Check(flags.All(pair => Equals(pair.Value, pair.Key.GetValue(p))), "No vanilla ability or screen-effect flags changed");
                Check(calFlags.All(pair => Equals(pair.Value, pair.Key.GetValue(cal))), "No Calamity ability or visual flags changed");
                Check(p.buffType.All(type => type == 0), "No buffs applied");
                Check(!Main.projectile.Any(projectile => projectile.active), "No projectiles spawned");
                Check(Math.Abs(p.GetDamage(DamageClass.Generic).Additive - startDamage - total.Classes.GetValueOrDefault(Bonus.ClassKey(DamageClass.Generic))!.Damage.Additive) < .0001, "Damage matches tooltip profile");
                double life = total.PlayerStats.GetValueOrDefault("statLifeMax2") + Math.Round(500 * total.HealthPercent);
                Check(p.statLifeMax2 == 500 + life, "Maximum life matches tooltip profile");
                Check((int)p.statDefense > 0 && (int)p.statDefense == total.Defense, "Defense matches tooltip profile on an initialized player");
                int previousLife = p.statLifeMax2;
                hope.Activate(false);
                Check(p.statLifeMax2 == previousLife, "Second relic cannot double the collection");
                List<string> tooltip = StatTooltip.Lines(total, 500, true, true);
                Check(tooltip.Any(line => line.Contains("seconds of ordinary wing flight")) && tooltip.Any(line => line.Contains("maximum life")), "Tooltip includes actual flight and health");
                var disabled = new HopeOptions();
                disabled.Disabled.Add(AccessoryCatalog.Entries.First().Type);
                using var bytes = new MemoryStream();
                using var writer = new BinaryWriter(bytes, System.Text.Encoding.UTF8, true);
                disabled.Write(writer); writer.Flush(); bytes.Position = 0;
                Check(HopeOptions.Read(new BinaryReader(bytes)).SameAs(disabled), "Multiplayer options roundtrip");
                results.Add(new { Day = day, Life = p.statLifeMax2, Defense = (int)p.statDefense, FlightSeconds = total.Flight / 60d, Tooltip = tooltip });
            }
            File.WriteAllText(report, JsonSerializer.Serialize(new { Passed = true, Ingredients = AccessoryCatalog.Entries.Count, Results = results,
                Checks = new[] { "same original recipe", "real generated-chest forge", "numeric stats match tooltip", "day/night profiles", "ordinary flight", "no ability or visual flags", "no buffs or projectiles", "second-relic guard", "option network serialization" } }, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine("LAST HOPE ALT CHECKS PASSED");
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            File.WriteAllText(report, JsonSerializer.Serialize(new { Passed = false, Error = ex.ToString() }, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine(ex); Environment.Exit(1);
        }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
