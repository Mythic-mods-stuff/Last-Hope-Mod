#nullable enable
using System.Linq.Expressions;
using System.Reflection;
using Terraria;
using Terraria.ModLoader;

namespace LastHopeAlt.Common;

// Evaluate each source's numerical changes against the same starting equipment stats.
// This makes flat and multiplicative accessory bonuses add rather than compound.
// Stateful fields (cooldowns, current health, projectile counts, etc.) are never reset here.
internal sealed class StatAccumulator
{
    private sealed class Scalar
    {
        public readonly Func<object, double> Read;
        public readonly Action<object, double> Write;
        public readonly object Owner;
        public double Baseline, Total;

        public Scalar(object owner, FieldInfo field)
        {
            Owner = owner;
            var instance = Expression.Parameter(typeof(object));
            var value = Expression.Parameter(typeof(double));
            var member = Expression.Field(Expression.Convert(instance, field.DeclaringType!), field);
            Read = Expression.Lambda<Func<object, double>>(Expression.Convert(member, typeof(double)), instance).Compile();
            Write = Expression.Lambda<Action<object, double>>(Expression.Assign(member, Expression.Convert(value, field.FieldType)), instance, value).Compile();
        }
    }

    private static readonly string[] PlayerFields = [
        "statLifeMax2", "statManaMax2", "lifeRegen", "manaRegenBonus", "manaCost", "manaRegenDelayBonus",
        "endurance", "moveSpeed", "jumpSpeedBoost", "accRunSpeed", "pickSpeed", "tileSpeed", "wallSpeed",
        "maxMinions", "maxTurrets", "aggro", "fishingSkill", "blockRange", "lavaMax", "rocketTimeMax",
        "whipRangeMultiplier", "arrowDamage", "bulletDamage", "specialistDamage", "minionKB", "equipmentBasedLuckBonus"
    ];
    private static readonly string[] CalamityFields = [
        "rogueStealthMax", "rogueVelocity", "stealthGenMoving", "stealthGenStandstill", "ammoCost", "critDamage",
        "contactDamageReduction", "healingPotionMultiplier", "moveSpeedBonus", "calamityBonusLuck", "RageDamageBoost",
        "HeatDebuffMultiplier", "ColdDebuffMultiplier", "SicknessDebuffMultiplier", "WaterDebuffMultiplier", "ElectricDebuffMultiplier"
    ];
    private readonly Player player;
    private readonly List<Scalar> scalars = new();
    private readonly StatModifier[] damageBase, damageTotal, knockbackBase, knockbackTotal;
    private readonly float[] critBase, critTotal, attackBase, attackTotal, penetrationBase, penetrationTotal;
    private readonly StatModifier[] ammoBase = new StatModifier[3], ammoTotal = new StatModifier[3];
    private Player.DefenseStat defenseBase;
    private int defensePositive, defenseNegative;
    private float defenseAdditive, defenseMultiplier;

    public StatAccumulator(Player player)
    {
        this.player = player;
        AddScalars(player, PlayerFields);
        foreach (ModPlayer mp in player.ModPlayers)
            if (mp.GetType().FullName == "CalamityMod.CalPlayer.CalamityPlayer") AddScalars(mp, CalamityFields);
        int count = DamageClassLoader.DamageClassCount;
        damageBase = new StatModifier[count]; damageTotal = new StatModifier[count];
        knockbackBase = new StatModifier[count]; knockbackTotal = new StatModifier[count];
        critBase = new float[count]; critTotal = new float[count];
        attackBase = new float[count]; attackTotal = new float[count];
        penetrationBase = new float[count]; penetrationTotal = new float[count];
    }

    private void AddScalars(object owner, string[] names)
    {
        foreach (string name in names)
        {
            FieldInfo? field = owner.GetType().GetField(name);
            if (field != null && (field.FieldType == typeof(float) || field.FieldType == typeof(int) || field.FieldType == typeof(double)))
                scalars.Add(new Scalar(owner, field));
        }
    }

    public void Begin()
    {
        foreach (Scalar s in scalars) { s.Baseline = s.Read(s.Owner); s.Total = 0; }
        defenseBase = player.statDefense;
        defensePositive = defenseNegative = 0;
        defenseAdditive = defenseMultiplier = 0;
        ammoBase[0] = player.arrowDamage; ammoBase[1] = player.bulletDamage; ammoBase[2] = player.specialistDamage;
        Array.Fill(ammoTotal, new StatModifier(0, 0, 0, 0));
        for (int i = 0; i < damageBase.Length; i++)
        {
            DamageClass dc = DamageClassLoader.GetDamageClass(i);
            damageBase[i] = player.GetDamage(dc); knockbackBase[i] = player.GetKnockback(dc);
            damageTotal[i] = knockbackTotal[i] = new StatModifier(0, 0, 0, 0);
            critBase[i] = player.GetCritChance(dc); critTotal[i] = 0;
            attackBase[i] = player.GetAttackSpeed(dc); attackTotal[i] = 0;
            penetrationBase[i] = player.GetArmorPenetration(dc); penetrationTotal[i] = 0;
        }
    }

    public void BeforeSource()
    {
        foreach (Scalar s in scalars) s.Write(s.Owner, s.Baseline);
        player.statDefense = defenseBase;
        player.arrowDamage = ammoBase[0]; player.bulletDamage = ammoBase[1]; player.specialistDamage = ammoBase[2];
        for (int i = 0; i < damageBase.Length; i++)
        {
            DamageClass dc = DamageClassLoader.GetDamageClass(i);
            player.GetDamage(dc) = damageBase[i]; player.GetKnockback(dc) = knockbackBase[i];
            player.GetCritChance(dc) = critBase[i]; player.GetAttackSpeed(dc) = attackBase[i];
            player.GetArmorPenetration(dc) = penetrationBase[i];
        }
    }

    public void AfterSource()
    {
        foreach (Scalar s in scalars) s.Total += s.Read(s.Owner) - s.Baseline;
        defensePositive += player.statDefense.Positive - defenseBase.Positive;
        defenseNegative += player.statDefense.Negative - defenseBase.Negative;
        defenseAdditive += player.statDefense.AdditiveBonus.Value - defenseBase.AdditiveBonus.Value;
        defenseMultiplier += player.statDefense.FinalMultiplier.Value - defenseBase.FinalMultiplier.Value;
        ammoTotal[0] = AddDelta(ammoTotal[0], player.arrowDamage, ammoBase[0]);
        ammoTotal[1] = AddDelta(ammoTotal[1], player.bulletDamage, ammoBase[1]);
        ammoTotal[2] = AddDelta(ammoTotal[2], player.specialistDamage, ammoBase[2]);
        for (int i = 0; i < damageBase.Length; i++)
        {
            DamageClass dc = DamageClassLoader.GetDamageClass(i);
            damageTotal[i] = AddDelta(damageTotal[i], player.GetDamage(dc), damageBase[i]);
            knockbackTotal[i] = AddDelta(knockbackTotal[i], player.GetKnockback(dc), knockbackBase[i]);
            critTotal[i] += player.GetCritChance(dc) - critBase[i];
            attackTotal[i] += player.GetAttackSpeed(dc) - attackBase[i];
            penetrationTotal[i] += player.GetArmorPenetration(dc) - penetrationBase[i];
        }
    }

    private static StatModifier AddDelta(StatModifier total, StatModifier value, StatModifier baseline)
        => new(total.Additive + value.Additive - baseline.Additive,
            total.Multiplicative + value.Multiplicative - baseline.Multiplicative,
            total.Flat + value.Flat - baseline.Flat, total.Base + value.Base - baseline.Base);

    private static StatModifier Combine(StatModifier baseline, StatModifier delta)
        => new(baseline.Additive + delta.Additive, baseline.Multiplicative + delta.Multiplicative,
            baseline.Flat + delta.Flat, baseline.Base + delta.Base);

    public void Finish()
    {
        foreach (Scalar s in scalars) s.Write(s.Owner, s.Baseline + s.Total);
        var defense = defenseBase + defensePositive - defenseNegative;
        defense.AdditiveBonus += defenseAdditive;
        float baselineMult = defenseBase.FinalMultiplier.Value;
        if (baselineMult != 0) defense.FinalMultiplier *= (baselineMult + defenseMultiplier) / baselineMult;
        player.statDefense = defense;
        player.arrowDamage = Combine(ammoBase[0], ammoTotal[0]);
        player.bulletDamage = Combine(ammoBase[1], ammoTotal[1]);
        player.specialistDamage = Combine(ammoBase[2], ammoTotal[2]);
        for (int i = 0; i < damageBase.Length; i++)
        {
            DamageClass dc = DamageClassLoader.GetDamageClass(i);
            player.GetDamage(dc) = Combine(damageBase[i], damageTotal[i]);
            player.GetKnockback(dc) = Combine(knockbackBase[i], knockbackTotal[i]);
            player.GetCritChance(dc) = critBase[i] + critTotal[i];
            player.GetAttackSpeed(dc) = attackBase[i] + attackTotal[i];
            player.GetArmorPenetration(dc) = penetrationBase[i] + penetrationTotal[i];
        }
    }
}
