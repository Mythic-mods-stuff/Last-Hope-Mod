#nullable enable
using System.Globalization;
using Terraria;
using Terraria.ModLoader;

namespace LastHopeAlt.Common;

internal static class StatTooltip
{
    private static string N(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    private static string Signed(double value) => (value >= 0 ? "+" : "") + N(value);
    private static string Percent(double value) => Signed(value * 100) + "%";
    private static string Modifier(ModifierBonus b)
    {
        var parts = new List<string>();
        if (Math.Abs(b.Additive) > .0001) parts.Add(Percent(b.Additive));
        if (Math.Abs(b.Multiplicative) > .0001) parts.Add(Percent(b.Multiplicative) + " multiplier");
        if (Math.Abs(b.Base) > .0001) parts.Add(Signed(b.Base) + " base");
        if (Math.Abs(b.Flat) > .0001) parts.Add(Signed(b.Flat) + " flat");
        return string.Join(", ", parts);
    }

    internal static List<string> Lines(Bonus b, int baseHealth, bool details, bool flight)
    {
        var lines = new List<string>();
        double health = b.PlayerStats.GetValueOrDefault("statLifeMax2") + Math.Round(baseHealth * b.HealthPercent);
        lines.Add($"{Signed(health)} maximum life ({Percent(b.HealthPercent)} base life), {Signed(b.PlayerStats.GetValueOrDefault("statManaMax2"))} maximum mana");
        string defense = $"{Signed(b.Defense)} defense";
        if (Math.Abs(b.DefensePercent) > .0001) defense += $", {Percent(b.DefensePercent)} defense bonus";
        if (Math.Abs(b.DefenseMultiplier) > .0001) defense += $", {Percent(b.DefenseMultiplier)} defense multiplier";
        lines.Add(defense);
        foreach (var (key, c) in b.Classes)
        {
            string name = key[(key.IndexOf('/') + 1)..];
            if (name == "Generic") name = "All";
            if (name.EndsWith("DamageClass")) name = name[..^11];
            var parts = new List<string>();
            if (c.Damage.Any) parts.Add(Modifier(c.Damage) + " damage");
            if (c.Crit != 0) parts.Add(Signed(c.Crit) + " percentage points crit");
            if (Math.Abs(c.Speed) > .0001) parts.Add(Percent(c.Speed) + " attack speed");
            if (c.Penetration != 0) parts.Add(Signed(c.Penetration) + " armor penetration");
            if (parts.Count > 0) lines.Add(name + ": " + string.Join("; ", parts));
            if (details && c.Knockback.Any) lines.Add(name + " knockback: " + Modifier(c.Knockback));
        }
        double dr = b.PlayerStats.GetValueOrDefault("endurance");
        if (dr != 0) lines.Add($"{Signed(dr * 100)} percentage points damage reduction (total capped at 100%)");
        lines.Add($"{Signed(b.PlayerStats.GetValueOrDefault("lifeRegen") / 2)} life/second regeneration bonus");
        lines.Add($"{Signed(b.PlayerStats.GetValueOrDefault("maxMinions"))} minion slots; {Signed(b.PlayerStats.GetValueOrDefault("maxTurrets"))} sentry slots");
        if (flight) lines.Add($"{N(b.Flight / 60d)} seconds of ordinary wing flight");
        if (!details) { lines.Add("Hold Shift for all numerical modifiers"); return lines; }
        if (b.Arrows.Any) lines.Add("Arrow damage: " + Modifier(b.Arrows));
        if (b.Bullets.Any) lines.Add("Bullet damage: " + Modifier(b.Bullets));
        if (b.Rockets.Any) lines.Add("Specialist damage: " + Modifier(b.Rockets));
        foreach (var (key, value) in b.PlayerStats)
        {
            if (key is "statLifeMax2" or "statManaMax2" or "lifeRegen" or "endurance" or "maxMinions" or "maxTurrets") continue;
            lines.Add(key switch
            {
                "manaCost" => "Mana cost modifier: " + Percent(value) + " (total cost cannot drop below zero)",
                "pickSpeed" => "Mining use-time modifier: " + Percent(value) + " (lower is faster; minimum 5%)",
                "moveSpeed" => "Movement speed: " + Percent(value),
                "tileSpeed" => "Tile placement speed: " + Percent(value),
                "wallSpeed" => "Wall placement speed: " + Percent(value),
                "whipRangeMultiplier" => "Whip range: " + Percent(value),
                "accRunSpeed" => "Running speed stat: " + Signed(value) + " pixels/tick",
                "jumpSpeedBoost" => "Jump speed: " + Signed(value) + " pixels/tick",
                "fishingSkill" => "Fishing power: " + Signed(value),
                "manaRegenBonus" => "Mana regeneration stat: " + Signed(value),
                "manaRegenDelayBonus" => "Mana regeneration delay stat: " + Signed(value),
                "aggro" => "Aggro: " + Signed(value),
                "blockRange" => "Block placement range: " + Signed(value) + " tiles",
                "minionKB" => "Minion knockback: " + Signed(value),
                "equipmentBasedLuckBonus" => "Equipment luck: " + Signed(value),
                _ => key + ": " + Signed(value)
            });
        }
        foreach (var (key, value) in b.CalamityStats)
        {
            string label = key switch
            {
                "rogueStealthMax" => "Maximum rogue stealth", "rogueVelocity" => "Rogue projectile speed",
                "stealthGenMoving" => "Moving stealth generation", "stealthGenStandstill" => "Stationary stealth generation",
                "ammoCost" => "Ammo cost", "critDamage" => "Critical damage multiplier",
                "contactDamageReduction" => "Contact damage reduction", "healingPotionMultiplier" => "Healing potion multiplier",
                "calamityBonusLuck" => "Calamity luck", _ => key.Replace("DebuffMultiplier", " debuff multiplier")
            };
            lines.Add(label + ": " + (key == "calamityBonusLuck" ? Signed(value) : Percent(value)));
        }
        foreach (var (key, value) in b.CalamityModifiers)
            if (value.Any) lines.Add(key.Replace("DebuffMultiplier", " debuff damage") + ": " + Modifier(value));
        return lines;
    }
}
