#nullable enable
using LastHopeAlt.UI;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;

namespace LastHopeAlt.Common;

public sealed class HopePlayer : ModPlayer
{
    public bool Equipped;
    public HopeOptions Options = new();
    private HopeOptions? lastSent;
    private readonly Item ordinaryWings = new(ItemID.WingsSolar);
    private bool hideWings;
    private int flightTime;
    internal int FlightTimeTotal => flightTime;
    // Kept for the copied compatibility source; its hooks are disabled in this variant.
    internal bool Applying => false;
    public Item? VirtualItem(int type) => null;
    public bool IsVirtual(Item item) => false;
    public int Selection(AbilityKind kind) => 0;

    public override void ResetEffects() => Equipped = false;

    public void Activate(bool hideVisual)
    {
        if (Equipped) return;
        Equipped = true;
        hideWings = hideVisual;
        if (Player.whoAmI == Main.myPlayer) Options = ModContent.GetInstance<HopeConfig>().Snapshot();
        Bonus current = NumericStats.Total(Options);
        current.Apply(Player);
        flightTime = Options.Modes[(int)AbilityKind.Wings] == SelectionMode.Disabled ? 0 : current.Flight;
        ApplyFlight();
    }

    private void ApplyFlight()
    {
        if (flightTime <= 0) return;
        // A vanilla wing item provides ordinary flight without Calamity wing abilities.
        Player.wingsLogic = ordinaryWings.wingSlot;
        Player.equippedWings = ordinaryWings;
        if (!hideWings) Player.wings = ordinaryWings.wingSlot;
        Player.wingTimeMax = Math.Max(Player.wingTimeMax, flightTime);
    }

    public override void UpdateEquips() { if (Equipped) ApplyFlight(); }
    public override void PostUpdateEquips() { if (Equipped) ApplyFlight(); }

    public override void ProcessTriggers(TriggersSet triggersSet)
    {
        if (!LastHope.ForgeKey.JustPressed) return;
        Point center = Player.Center.ToTileCoordinates();
        for (int x = center.X - 5; x <= center.X + 5; x++)
            for (int y = center.Y - 5; y <= center.Y + 5; y++)
            {
                Point bench = ForgeService.BenchOrigin(x, y);
                if (bench.X >= 0 && ForgeService.CanUse(Player, bench)) { ForgeUI.Open(bench); return; }
            }
        Main.NewText("Stand near a workbench to open the Last Hope Alt forge.");
    }

    public override void PostUpdate()
    {
        if (Player.whoAmI != Main.myPlayer || Main.GameUpdateCount % 30 != 0) return;
        HopeOptions options = ModContent.GetInstance<HopeConfig>().Snapshot();
        if (lastSent != null && options.SameAs(lastSent)) return;
        Options = options;
        lastSent = options;
        if (Main.netMode == NetmodeID.MultiplayerClient) SyncPlayer(-1, -1, false);
    }

    public override void SyncPlayer(int toWho, int fromWho, bool newPlayer)
    {
        var packet = Mod.GetPacket();
        packet.Write((byte)HopeMessage.Options);
        packet.Write((byte)Player.whoAmI);
        Options.Write(packet);
        packet.Send(toWho, fromWho);
    }
}
