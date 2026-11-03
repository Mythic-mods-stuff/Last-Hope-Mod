#nullable enable
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Terraria;
using Terraria.ModLoader;

namespace LastHopeAlt.Common;

public sealed class ModifierBonus
{
    public float Additive { get; set; }
    public float Multiplicative { get; set; }
    public float Flat { get; set; }
    public float Base { get; set; }
    public static ModifierBonus Difference(StatModifier a, StatModifier b) => new()
    { Additive = a.Additive - b.Additive, Multiplicative = a.Multiplicative - b.Multiplicative, Flat = a.Flat - b.Flat, Base = a.Base - b.Base };
    public void Add(ModifierBonus b) { Additive += b.Additive; Multiplicative += b.Multiplicative; Flat += b.Flat; Base += b.Base; }
    public StatModifier Apply(StatModifier value) => new(value.Additive + Additive, value.Multiplicative + Multiplicative, value.Flat + Flat, value.Base + Base);
    public bool Any => Math.Abs(Additive) + Math.Abs(Multiplicative) + Math.Abs(Flat) + Math.Abs(Base) > .0001;
}

public sealed class ClassBonus
{
    public ModifierBonus Damage { get; set; } = new();
    public ModifierBonus Knockback { get; set; } = new();
    public float Crit { get; set; }
    public float Speed { get; set; }
    public float Penetration { get; set; }
    public void Add(ClassBonus b) { Damage.Add(b.Damage); Knockback.Add(b.Knockback); Crit += b.Crit; Speed += b.Speed; Penetration += b.Penetration; }
}

public sealed class Bonus
{
    // Ability flags, visuals, buffs and cooldowns cannot enter this numerical allowlist.
    internal static readonly string[] PlayerFields = ["statLifeMax2", "statManaMax2", "lifeRegen", "manaRegenBonus", "manaCost", "manaRegenDelayBonus",
        "endurance", "moveSpeed", "jumpSpeedBoost", "accRunSpeed", "pickSpeed", "tileSpeed", "wallSpeed", "maxMinions", "maxTurrets",
        "aggro", "fishingSkill", "blockRange", "whipRangeMultiplier", "minionKB", "equipmentBasedLuckBonus"];
    internal static readonly string[] CalamityFields = ["rogueStealthMax", "rogueVelocity", "stealthGenMoving", "stealthGenStandstill", "ammoCost", "critDamage",
        "contactDamageReduction", "healingPotionMultiplier", "calamityBonusLuck", "HeatDebuffMultiplier", "ColdDebuffMultiplier", "SicknessDebuffMultiplier", "WaterDebuffMultiplier", "ElectricDebuffMultiplier"];
    public Dictionary<string, double> PlayerStats { get; set; } = new();
    public Dictionary<string, double> CalamityStats { get; set; } = new();
    public Dictionary<string, ModifierBonus> CalamityModifiers { get; set; } = new();
    public Dictionary<string, ClassBonus> Classes { get; set; } = new();
    public ModifierBonus Arrows { get; set; } = new();
    public ModifierBonus Bullets { get; set; } = new();
    public ModifierBonus Rockets { get; set; } = new();
    public int Defense { get; set; }
    public float DefensePercent { get; set; }
    public float DefenseMultiplier { get; set; }
    public double HealthPercent { get; set; }
    public int Flight { get; set; }

    internal static object? Calamity(Player p)
    {
        foreach (ModPlayer mp in p.ModPlayers)
            if (mp.GetType().FullName == "CalamityMod.CalPlayer.CalamityPlayer") return mp;
        return null;
    }
    internal static string ClassKey(DamageClass dc) => (dc.Mod?.Name ?? "Terraria") + "/" + dc.Name;
    internal static double Number(object? owner, string name)
        => owner?.GetType().GetField(name)?.GetValue(owner) is int or float or double
            ? Convert.ToDouble(owner.GetType().GetField(name)!.GetValue(owner), CultureInfo.InvariantCulture) : 0;
    private static void AddFields(Dictionary<string, double> target, Dictionary<string, double> source)
    { foreach (var (key, value) in source) target[key] = target.GetValueOrDefault(key) + value; }
    public void Add(Bonus b)
    {
        AddFields(PlayerStats, b.PlayerStats); AddFields(CalamityStats, b.CalamityStats);
        foreach (var (key, value) in b.CalamityModifiers) { if (!CalamityModifiers.TryGetValue(key, out var m)) CalamityModifiers[key] = m = new(); m.Add(value); }
        foreach (var (key, value) in b.Classes) { if (!Classes.TryGetValue(key, out var c)) Classes[key] = c = new(); c.Add(value); }
        Arrows.Add(b.Arrows); Bullets.Add(b.Bullets); Rockets.Add(b.Rockets);
        Defense += b.Defense; DefensePercent += b.DefensePercent; DefenseMultiplier += b.DefenseMultiplier;
        HealthPercent += b.HealthPercent; Flight += b.Flight;
    }

    public void Apply(Player player)
    {
        ApplyFields(player, PlayerStats, PlayerFields);
        object? cal = Calamity(player);
        if (cal != null) ApplyFields(cal, CalamityStats, CalamityFields);
        if (cal != null)
            foreach (string name in CalamityFields)
                if (CalamityModifiers.TryGetValue(name, out var m) && cal.GetType().GetField(name) is FieldInfo field && field.GetValue(cal) is StatModifier old)
                    field.SetValue(cal, m.Apply(old));
        player.statLifeMax2 += (int)Math.Round(player.statLifeMax * HealthPercent);
        player.statDefense += Defense;
        player.statDefense.AdditiveBonus += DefensePercent;
        player.statDefense.FinalMultiplier *= Math.Max(0, 1 + DefenseMultiplier);
        for (int i = 0; i < DamageClassLoader.DamageClassCount; i++)
        {
            DamageClass dc = DamageClassLoader.GetDamageClass(i);
            if (!Classes.TryGetValue(ClassKey(dc), out var bonus)) continue;
            player.GetDamage(dc) = bonus.Damage.Apply(player.GetDamage(dc));
            player.GetKnockback(dc) = bonus.Knockback.Apply(player.GetKnockback(dc));
            player.GetCritChance(dc) += bonus.Crit;
            player.GetAttackSpeed(dc) += bonus.Speed;
            player.GetArmorPenetration(dc) += bonus.Penetration;
        }
        player.arrowDamage = Arrows.Apply(player.arrowDamage);
        player.bulletDamage = Bullets.Apply(player.bulletDamage);
        player.specialistDamage = Rockets.Apply(player.specialistDamage);
        player.manaCost = Math.Max(0, player.manaCost);
        player.pickSpeed = Math.Max(.05f, player.pickSpeed);
        player.endurance = Math.Clamp(player.endurance, 0, 1);
    }

    private static void ApplyFields(object owner, Dictionary<string, double> values, string[] allowed)
    {
        foreach (string key in allowed)
        {
            if (!values.TryGetValue(key, out double delta)) continue;
            FieldInfo? field = owner.GetType().GetField(key);
            if (field == null || !double.IsFinite(delta)) continue;
            if (field.FieldType != typeof(int) && field.FieldType != typeof(float) && field.FieldType != typeof(double)) continue;
            field.SetValue(owner, Convert.ChangeType(Number(owner, key) + delta, field.FieldType, CultureInfo.InvariantCulture));
        }
    }
}

public sealed class AccessoryNumbers
{
    public string Key { get; set; } = "";
    public Bonus Day { get; set; } = new();
    public Bonus Night { get; set; } = new();
}

public sealed class NumericStats : ModSystem
{
    private static Dictionary<string, AccessoryNumbers> data = new();
    private static Bonus? cachedTotal;
    private static bool cachedDay;
    private static HashSet<int> cachedDisabled = new();
    public override void Load()
    {
        if (Mod.FileExists("Data/accessory-stats.json"))
            data = (JsonSerializer.Deserialize<AccessoryNumbers[]>(Mod.GetFileBytes("Data/accessory-stats.json")) ?? []).ToDictionary(x => x.Key);
    }
    public override void PostSetupRecipes()
    {
        if (Environment.GetEnvironmentVariable("LAST_HOPE_ALT_EXPORT") != null) return;
        string[] missing = AccessoryCatalog.Entries.Where(e => !data.ContainsKey(e.Key)).Select(e => e.Key).ToArray();
        if (missing.Length > 0) throw new InvalidOperationException($"The Last Hope Alt has no numerical profile for {missing.Length} accessories. Regenerate the profile for this Calamity version.");
    }
    public static Bonus Total(HopeOptions options)
    {
        if (cachedTotal != null && cachedDay == Main.dayTime && cachedDisabled.SetEquals(options.Disabled)) return cachedTotal;
        var total = new Bonus();
        foreach (AccessoryEntry entry in AccessoryCatalog.Entries)
            if (!options.Disabled.Contains(entry.Type) && data.TryGetValue(entry.Key, out var item)) total.Add(Main.dayTime ? item.Day : item.Night);
        cachedDay = Main.dayTime;
        cachedDisabled = new HashSet<int>(options.Disabled);
        return cachedTotal = total;
    }
    public override void Unload() { data.Clear(); cachedTotal = null; cachedDisabled.Clear(); }
}
