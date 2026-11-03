#nullable enable
using System.Reflection;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using Terraria;
using Terraria.ModLoader;

namespace LastHopeAlt.Common;

// Keep compatibility glue separate from catalog and effect application. No Calamity source
// or assets are copied into this mod. Reflection tolerates field/property changes between releases.
[Autoload(false)]
public sealed class CalamityBridge : ModSystem
{
    private static Type? transformationPlayerType;
    private static FieldInfo? currentTransformation;
    private static MethodInfo? equipTransformation;

    public override void PostSetupContent()
    {
        if (!ModLoader.TryGetMod("CalamityMod", out Mod cal)) return;
        transformationPlayerType = cal.Code.GetType("CalamityMod.Items.BaseItems.TransformationPlayer");
        currentTransformation = transformationPlayerType?.GetField("currentTransformation", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        equipTransformation = transformationPlayerType?.GetMethod("EquipTransformation", BindingFlags.Public | BindingFlags.Static);
        Type? playerType = cal.Code.GetType("CalamityMod.CalPlayer.CalamityPlayer");
        MethodInfo? find = playerType?.GetMethod("FindAccessory", [typeof(int)]);
        if (find != null) Patch(find, PatchFindAccessory);
        Type? extensions = cal.Code.GetType("CalamityMod.CalamityKeybindsExtensions");
        MethodInfo? rent = cal.Code.GetType("CalamityMod.AccessoryKeybindManager")?.GetMethod("UpdateEquip", [typeof(Item), typeof(Player)]);
        if (rent != null) Patch(rent, il =>
        {
            var cursor = new ILCursor(il);
            ILLabel original = cursor.DefineLabel();
            cursor.Emit(OpCodes.Ldarg_1);
            cursor.Emit(OpCodes.Ldarg_2);
            cursor.EmitDelegate<Func<Item, Player, bool>>((item, player) => player.GetModPlayer<HopePlayer>().IsVirtual(item));
            cursor.Emit(OpCodes.Brfalse, original);
            cursor.Emit(OpCodes.Ret);
            cursor.MarkLabel(original);
        });
        PatchKey(extensions, "JustPressedKeybind", () => LastHope.AbilityKey.JustPressed);
        PatchKey(extensions, "JustReleasedKeybind", () => LastHope.AbilityKey.JustReleased);
        PatchKey(extensions, "CurrentlyPressingKeybind", () => LastHope.AbilityKey.Current);
        MethodInfo? dynamicKey = extensions?.GetMethod("GetDynamicModHotkey", [typeof(Item)]);
        if (dynamicKey != null) Patch(dynamicKey, il =>
        {
            var cursor = new ILCursor(il);
            ILLabel original = cursor.DefineLabel();
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<Item, bool>>(IsVirtual);
            cursor.Emit(OpCodes.Brfalse, original);
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<Item, ModKeybind?>>(item => IsSelected(item) ? LastHope.AbilityKey : null);
            cursor.Emit(OpCodes.Ret);
            cursor.MarkLabel(original);
        });
        if (extensions == null) Mod.Logger.Warn("Calamity uses legacy keybinds. Dedicated Calamity ability keys remain in effect.");
    }

    private void Patch(MethodInfo method, ILContext.Manipulator patch)
    {
        MonoModHooks.Modify(method, patch);
    }

    private static bool IsVirtual(Item item)
    {
        if (Main.dedServ || Main.LocalPlayer == null) return false;
        HopePlayer hope = Main.LocalPlayer.GetModPlayer<HopePlayer>();
        return hope.Equipped && hope.IsVirtual(item);
    }

    private static bool IsSelected(Item item)
        => Main.LocalPlayer.GetModPlayer<HopePlayer>().Selection(AbilityKind.ActiveAbility) == item.type;

    private void PatchKey(Type? extensions, string name, Func<bool> state)
    {
        MethodInfo? method = extensions?.GetMethod(name, [typeof(Item)]);
        if (method == null) return;
        Patch(method, il =>
        {
            var cursor = new ILCursor(il);
            ILLabel original = cursor.DefineLabel();
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<Item, bool>>(IsVirtual);
            cursor.Emit(OpCodes.Brfalse, original);
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<Item, bool>>(item => IsSelected(item) && state());
            cursor.Emit(OpCodes.Ret);
            cursor.MarkLabel(original);
        });
    }

    private static void PatchFindAccessory(ILContext il)
    {
        var cursor = new ILCursor(il);
        ILLabel original = cursor.DefineLabel();
        cursor.Emit(OpCodes.Ldarg_0);
        cursor.Emit(OpCodes.Ldarg_1);
        cursor.EmitDelegate<Func<ModPlayer, int, Item?>>((mp, type) =>
        {
            HopePlayer hope = mp.Player.GetModPlayer<HopePlayer>();
            return hope.Equipped && !hope.Options.Disabled.Contains(type) ? hope.VirtualItem(type) : null;
        });
        cursor.Emit(OpCodes.Dup);
        cursor.Emit(OpCodes.Brfalse, original);
        cursor.Emit(OpCodes.Ret);
        cursor.MarkLabel(original);
        cursor.Emit(OpCodes.Pop);
    }

    internal static void ApplyTransformation(Player player, Item item)
    {
        if (equipTransformation == null || currentTransformation == null || item.ModItem == null) return;
        Type? baseType = item.ModItem.GetType();
        while (baseType != null && baseType.Name != "TransformationAccessory") baseType = baseType.BaseType;
        if (baseType == null) return; // Vanilla's werewolf/merfolk buffs control their own appearance.
        var condition = item.ModItem.GetType().GetProperty("ShouldTransform")?.GetValue(item.ModItem) as Func<Player, bool>;
        if (condition != null && !condition(player)) return;
        foreach (ModPlayer mp in player.ModPlayers)
        {
            if (mp.GetType() != transformationPlayerType) continue;
            currentTransformation.SetValue(mp, item.ModItem);
            equipTransformation.Invoke(null, new object[] { item.ModItem, player, mp.Mod });
            return;
        }
    }

    // tModLoader automatically removes this assembly's hooks when the mod unloads.
    public override void Unload()
    {
        transformationPlayerType = null;
        currentTransformation = null;
        equipTransformation = null;
    }
}
