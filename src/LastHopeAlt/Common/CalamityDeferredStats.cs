#nullable enable
using System.Reflection;
using Terraria;
using Terraria.ModLoader;

namespace LastHopeAlt.Common;

// Calamity normally collapses several ingredient accessories into shared bools and
// deliberately grants only the upgraded accessory's minion slots. Preserve each source's
// numerical contribution while leaving the shared proc/ability flags in Calamity's hands.
internal sealed class CalamityDeferredStats
{
    private static readonly string[] MinionFlags = ["nucleogenesis", "shadowMinions", "statisMinions", "starTaintedGenerator", "starbusterCore", "voltaicJelly", "nuclearFuelRod"];
    private readonly object owner;
    private readonly FieldInfo?[] minions;
    private readonly bool[] union;
    private readonly FieldInfo? treads;
    private readonly FieldInfo? extraFlight;
    private readonly double treadsBoost;
    private readonly FieldInfo? fleshKnuckles;
    private int fleshKnucklesTotal;
    private bool fleshKnucklesUnion;
    private int minionTotal;
    private int treadsTotal;
    private bool treadsUnion;

    public CalamityDeferredStats(object owner)
    {
        this.owner = owner;
        Type type = owner.GetType();
        minions = MinionFlags.Select(type.GetField).ToArray();
        union = new bool[minions.Length];
        treads = type.GetField("angelTreads");
        extraFlight = type.GetField("externalFlightTimeMultBoost");
        fleshKnuckles = type.GetField("fleshKnuckles");
        Type? item = ModLoader.GetMod("CalamityMod").Code.GetType("CalamityMod.Items.Accessories.AngelTreads");
        treadsBoost = Convert.ToDouble(item?.GetField("FlightTimeBoost", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) ?? .1f);
    }

    private bool Read(FieldInfo? field) => field?.GetValue(owner) is true;
    private static int Slots(bool[] flags) => flags[0] ? 4 : (flags[1] ? 1 : 0) + (flags[2] ? 2 : 0)
        + (flags[3] ? 2 : (flags[4] ? 1 : 0) + (flags[5] ? 1 : 0) + (flags[6] ? 1 : 0));

    public void Begin()
    {
        for (int i = 0; i < minions.Length; i++) union[i] = Read(minions[i]);
        minionTotal = Slots(union);
        treadsUnion = Read(treads);
        treadsTotal = treadsUnion ? 1 : 0;
        fleshKnucklesUnion = Read(fleshKnuckles);
        fleshKnucklesTotal = fleshKnucklesUnion ? 1 : 0;
    }

    public void BeforeSource()
    {
        foreach (FieldInfo? field in minions) field?.SetValue(owner, false);
        treads?.SetValue(owner, false);
        fleshKnuckles?.SetValue(owner, false);
    }

    public void AfterSource()
    {
        bool[] flags = new bool[minions.Length];
        for (int i = 0; i < flags.Length; i++) { flags[i] = Read(minions[i]); union[i] |= flags[i]; }
        minionTotal += Slots(flags);
        if (Read(treads)) { treadsTotal++; treadsUnion = true; }
        if (Read(fleshKnuckles)) { fleshKnucklesTotal++; fleshKnucklesUnion = true; }
    }

    public void Finish(Player player)
    {
        for (int i = 0; i < minions.Length; i++) minions[i]?.SetValue(owner, union[i]);
        treads?.SetValue(owner, treadsUnion);
        fleshKnuckles?.SetValue(owner, fleshKnucklesUnion);
        // Calamity grants 25 maximum life on the next ResetEffects for this shared flag.
        // Add the extra component/upgrade copies now; leave its original contribution intact.
        player.statLifeMax2 += Math.Max(0, fleshKnucklesTotal - (fleshKnucklesUnion ? 1 : 0)) * 25;
        player.maxMinions += minionTotal - Slots(union);
        if (extraFlight != null && treadsUnion && treadsTotal > 1)
        {
            double value = Convert.ToDouble(extraFlight.GetValue(owner)) + (treadsTotal - 1) * treadsBoost;
            extraFlight.SetValue(owner, Convert.ChangeType(value, extraFlight.FieldType));
        }
    }
}
