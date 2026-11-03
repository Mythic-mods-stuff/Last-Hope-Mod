#nullable enable
using LastHopeAlt.Common;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework.Input;

namespace LastHopeAlt.Content.Items;

public sealed class TheLastHope : ModItem
{
    // Reuse a game asset until a dedicated relic sprite is made.
    public override string Texture => "Terraria/Images/Item_" + ItemID.CelestialShell;

    public override void SetDefaults()
    {
        Item.width = 32;
        Item.height = 32;
        Item.accessory = true;
        Item.rare = ItemRarityID.Red;
        Item.value = 0;
    }

    public override void UpdateAccessory(Player player, bool hideVisual)
        => player.GetModPlayer<HopePlayer>().Activate(hideVisual);

    public override void ModifyTooltips(List<TooltipLine> tooltips)
    {
        HopeOptions options = ModContent.GetInstance<HopeConfig>().Snapshot();
        Bonus bonus = NumericStats.Total(options);
        bool details = Main.keyState.IsKeyDown(Keys.LeftShift) || Main.keyState.IsKeyDown(Keys.RightShift);
        int index = 0;
        foreach (string text in StatTooltip.Lines(bonus, Main.LocalPlayer.statLifeMax, details,
            options.Modes[(int)AbilityKind.Wings] != SelectionMode.Disabled))
            tooltips.Add(new TooltipLine(Mod, "AltStat" + index++, text));
        tooltips.Add(new TooltipLine(Mod, "AltScope", "Stats and ordinary flight only. No accessory abilities or visual effects."));
        tooltips.Add(new TooltipLine(Mod, "AltProfile", "Passive profile: full health and stationary; day/night bonuses adjust automatically."));
    }

    public override Color? GetAlpha(Color lightColor) => new Color(255, 214, 130, 255);
}
