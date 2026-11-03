#nullable enable
using System.Reflection;
using System.Text.Json;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;

namespace LastHopeAlt.Common;

// Offline build tool only. Never invokes source accessory hooks during gameplay.
public sealed class ProfileExporter : ModSystem
{
    public override void PostSetupRecipes()
    {
        string? target = Environment.GetEnvironmentVariable("LAST_HOPE_ALT_EXPORT");
        if (string.IsNullOrEmpty(target)) return;
        string current = "initialization";
        try
        {
            if (!Main.dedServ) throw new InvalidOperationException("Export requires an isolated headless process");
            Main.maxTilesX = Main.maxTilesY = 180;
            Main.tile = (Tilemap)Activator.CreateInstance(typeof(Tilemap), BindingFlags.Instance | BindingFlags.NonPublic,
                null, new object[] { (ushort)180, (ushort)180 }, null)!;
            Main.worldSurface = 110; Main.rockLayer = 140;
            for (int i = 0; i < Main.player.Length; i++) Main.player[i] = new Player { active = false };
            for (int i = 0; i < Main.projectile.Length; i++) Main.projectile[i] = new Projectile();
            for (int i = 0; i < Main.npc.Length; i++) Main.npc[i] = new NPC();
            for (int i = 0; i < Main.item.Length; i++) Main.item[i] = new Item();
            var items = new List<AccessoryNumbers>();
            foreach (AccessoryEntry entry in AccessoryCatalog.Entries)
            {
                current = entry.Key;
                var record = new AccessoryNumbers { Key = entry.Key };
                foreach (bool day in new[] { true, false })
                {
                    Main.dayTime = day;
                    Player empty = Sample(0, 400);
                    Player source = Sample(entry.Type, 400);
                    Player emptyLarge = Sample(0, 500);
                    Player sourceLarge = Sample(entry.Type, 500);
                    Bonus bonus = Difference(source, empty);
                    double small = source.statLifeMax2 - empty.statLifeMax2;
                    double large = sourceLarge.statLifeMax2 - emptyLarge.statLifeMax2;
                    bonus.HealthPercent = (large - small) / 100;
                    bonus.PlayerStats["statLifeMax2"] = small - 400 * bonus.HealthPercent;
                    // Wing duration is measured independently; it never enables a source wing's special behavior.
                    bonus.Flight = Math.Max(entry.WingTime, Math.Max(0, source.wingTimeMax - empty.wingTimeMax));
                    if (day) record.Day = bonus; else record.Night = bonus;
                }
                items.Add(record);
                if (items.Count % 100 == 0) Console.WriteLine($"ALT PROFILE: {items.Count}/{AccessoryCatalog.Entries.Count}");
            }
            File.WriteAllText(target, JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"ALT PROFILE EXPORTED: {items.Count} accessories");
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ALT PROFILE FAILED for {current}: {ex}");
            Environment.Exit(1);
        }
    }

    private static Player Sample(int type, int health)
    {
        var p = new Player { whoAmI = 0, active = true, position = new Vector2(90 * 16, 90 * 16),
            statLifeMax = health, statLifeMax2 = health, statLife = health, statManaMax = 200, statManaMax2 = 200, statMana = 200, chest = -1 };
        Main.player[0] = p;
        // ResetEffects derives permanent maxima from consumed upgrades, not assigned fields.
        p.ConsumedLifeCrystals = 15;
        p.ConsumedLifeFruit = health == 500 ? 20 : 0;
        p.ConsumedManaCrystals = 9;
        if (type > 0) { p.armor[3] = new Item(type); p.armor[3].expertOnly = p.armor[3].masterOnly = false; }
        // Two passes include maximum-life bonuses which Calamity resolves on the next reset.
        for (int tick = 0; tick < 2; tick++)
        {
            p.ResetEffects();
            if (p.statLifeMax != health) throw new InvalidOperationException($"Sample base life is {p.statLifeMax}, expected {health}");
            p.statLife = p.statLifeMax2;
            p.UpdateEquips(0);
            PlayerLoader.PostUpdateEquips(p);
            p.statLife = p.statLifeMax2;
            PlayerLoader.PostUpdateMiscEffects(p);
        }
        return p;
    }

    internal static Bonus Difference(Player value, Player baseline)
    {
        var b = new Bonus();
        foreach (string name in Bonus.PlayerFields)
        {
            double delta = Bonus.Number(value, name) - Bonus.Number(baseline, name);
            if (Math.Abs(delta) > .00001) b.PlayerStats[name] = delta;
        }
        object? vc = Bonus.Calamity(value), bc = Bonus.Calamity(baseline);
        foreach (string name in Bonus.CalamityFields)
        {
            if (vc?.GetType().GetField(name)?.GetValue(vc) is StatModifier vm && bc?.GetType().GetField(name)?.GetValue(bc) is StatModifier bm)
            {
                ModifierBonus modifier = ModifierBonus.Difference(vm, bm);
                if (modifier.Any) b.CalamityModifiers[name] = modifier;
                continue;
            }
            double delta = Bonus.Number(vc, name) - Bonus.Number(bc, name);
            if (Math.Abs(delta) > .00001) b.CalamityStats[name] = delta;
        }
        b.Defense = value.statDefense.Positive - value.statDefense.Negative - baseline.statDefense.Positive + baseline.statDefense.Negative;
        b.DefensePercent = value.statDefense.AdditiveBonus.Value - baseline.statDefense.AdditiveBonus.Value;
        b.DefenseMultiplier = value.statDefense.FinalMultiplier.Value - baseline.statDefense.FinalMultiplier.Value;
        b.Arrows = ModifierBonus.Difference(value.arrowDamage, baseline.arrowDamage);
        b.Bullets = ModifierBonus.Difference(value.bulletDamage, baseline.bulletDamage);
        b.Rockets = ModifierBonus.Difference(value.specialistDamage, baseline.specialistDamage);
        for (int i = 0; i < DamageClassLoader.DamageClassCount; i++)
        {
            DamageClass dc = DamageClassLoader.GetDamageClass(i);
            var c = new ClassBonus { Damage = ModifierBonus.Difference(value.GetDamage(dc), baseline.GetDamage(dc)),
                Knockback = ModifierBonus.Difference(value.GetKnockback(dc), baseline.GetKnockback(dc)),
                Crit = value.GetCritChance(dc) - baseline.GetCritChance(dc), Speed = value.GetAttackSpeed(dc) - baseline.GetAttackSpeed(dc),
                Penetration = value.GetArmorPenetration(dc) - baseline.GetArmorPenetration(dc) };
            if (c.Damage.Any || c.Knockback.Any || c.Crit != 0 || c.Speed != 0 || c.Penetration != 0) b.Classes[Bonus.ClassKey(dc)] = c;
        }
        return b;
    }
}
