#nullable enable
using System.Reflection;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.Creative;
using Terraria.ID;
using Terraria.ModLoader;

namespace LastHopeAlt.Common;

public sealed record AccessoryEntry(int Type, string Key, string Name, bool Calamity, int WingTime,
    bool Dash, bool Transformation, bool ActiveAbility, bool Music, long Priority)
{
    public bool Supports(AbilityKind kind) => kind switch
    {
        AbilityKind.Wings => WingTime > 0,
        AbilityKind.Dash => Dash,
        AbilityKind.Transformation => Transformation,
        AbilityKind.ActiveAbility => ActiveAbility,
        AbilityKind.Music => Music,
        _ => false
    };
}

public sealed class AccessoryCatalog : ModSystem
{
    public static IReadOnlyList<AccessoryEntry> Entries { get; private set; } = Array.Empty<AccessoryEntry>();
    private static HashSet<int> eligible = new();
    public static bool Contains(int type) => eligible.Contains(type);

    private static readonly HashSet<string> DeveloperItems = new(StringComparer.Ordinal)
    {
        "CenxsWings", "CrownosWings", "DTownsWings", "FoodBarbarianWings", "GroxTheGreatWings",
        "JimsWings", "LeinforsWings", "LokisWings", "RedsWings", "RedsWingsOld", "SkiphsWings",
        "WillsWings", "ArkhalisWings", "Yoraiz0rWings", "LazuresBarrierPlatform", "GhostarsWings", "SafemanWings",
        "Yoraiz0rDarkness", "LeinforsAccessory"
    };

    // Explicit progression order for dashes; rarity alone does not describe dash strength.
    private static readonly Dictionary<string, int> DashRanks = new()
    {
        ["ShieldOfCthulhu"] = 10, ["ShieldofCthulhu"] = 10, ["Tabi"] = 20, ["MasterNinjaGear"] = 25,
        ["CounterScarf"] = 30, ["EvasionScarf"] = 35, ["OrnateShield"] = 40, ["DeepDiver"] = 45,
        ["AsgardsValor"] = 50, ["StatisNinjaBelt"] = 60, ["ElysianAegis"] = 70,
        ["StatisVoidSash"] = 80, ["AsgardianAegis"] = 100, ["V8Engine"] = 55, ["V8000Engine"] = 90
    };

    public override void PostAddRecipes()
    {
        var result = new List<AccessoryEntry>();
        ModLoader.TryGetMod("CalamityMod", out Mod? calamity);
        var keybindSet = calamity?.Code.GetType("CalamityMod.Systems.Collections.CalamityItemSets")?
            .GetField("HasAccessoryKeybind", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as bool[];
        foreach ((int type, Item sample) in ContentSamples.ItemsByType.OrderBy(x => x.Key))
        {
            if (type <= 0 || !sample.accessory || sample.IsAir) continue;
            bool cal = sample.ModItem?.Mod.Name == "CalamityMod";
            if (sample.ModItem != null && !cal) continue;
            string name = sample.ModItem?.Name ?? ItemID.Search.GetName(type);
            if (ItemID.Sets.Deprecated[type] || ItemID.Sets.ItemsThatShouldNotBeInInventory[type] || DeveloperItems.Contains(name)) continue;
            // Both regular pets and light pets are excluded, including any accessory which grants one.
            if (sample.buffType > 0 && (Main.vanityPet[sample.buffType] || Main.lightPet[sample.buffType])) continue;
            if (!CreativeItemSacrificesCatalog.Instance.SacrificeCountNeededByItemId.TryGetValue(type, out int research) || research <= 0) continue;
            if (cal && IsDeveloper(sample)) continue;
            bool transform = IsTransformation(sample.ModItem?.GetType()) || name is "MoonCharm" or "NeptunesShell" or "MoonShell" or "CelestialShell";
            bool dash = DashRanks.ContainsKey(name);
            bool active = keybindSet != null && type < keybindSet.Length && keybindSet[type];
            // Compatibility with versions preceding Calamity's dynamic accessory keybind system.
            active |= name is "AscendantInsignia" or "SpectralVeil" or "TheTransformer" or "InterstellarStompers" or "BlunderBooster";
            bool music = name.StartsWith("MusicBox", StringComparison.Ordinal) || name.EndsWith("MusicBox", StringComparison.Ordinal);
            int time = sample.wingSlot > 0 ? ArmorIDs.Wing.Sets.Stats[sample.wingSlot].FlyTime : 0;
            long priority = Math.Max(0, sample.rare) * 1_000_000L + Math.Clamp(sample.value, 0, 999_999);
            if (dash) priority = DashRanks[name] * 1_000_000L;
            result.Add(new(type, (cal ? "CalamityMod/" : "Terraria/") + name, sample.Name, cal,
                time, dash, transform, active, music, priority));
        }
        Entries = result;
        eligible = result.Select(x => x.Type).ToHashSet();
        Mod.Logger.Info($"Accessory catalog: {result.Count} eligible ({result.Count(x => !x.Calamity)} vanilla / {result.Count(x => x.Calamity)} Calamity)");
    }

    private static bool IsDeveloper(Item item)
    {
        foreach (GlobalItem global in item.Globals)
        {
            if (global.Mod.Name != "CalamityMod") continue;
            if (global.GetType().GetField("devItem")?.GetValue(global) is true
                || global.GetType().GetProperty("devItem")?.GetValue(global) is true) return true;
        }
        return false;
    }

    private static bool IsTransformation(Type? type)
    {
        while (type != null)
        {
            if (type.Name == "TransformationAccessory") return true;
            type = type.BaseType;
        }
        return false;
    }

    public static int Select(AbilityKind kind, HopeOptions options)
    {
        int slot = (int)kind;
        if (options.Modes[slot] == SelectionMode.Disabled) return 0;
        var candidates = Entries.Where(x => x.Supports(kind) && !options.Disabled.Contains(x.Type));
        if (options.Modes[slot] == SelectionMode.Manual && candidates.Any(x => x.Type == options.Items[slot]))
            return options.Items[slot];
        return candidates.OrderByDescending(x => kind == AbilityKind.Wings ? x.WingTime * 1_000_000_000L + x.Priority : x.Priority)
            .ThenBy(x => x.Key, StringComparer.Ordinal).Select(x => x.Type).FirstOrDefault();
    }

    public override void Unload()
    {
        Entries = Array.Empty<AccessoryEntry>();
        eligible.Clear();
    }
}
