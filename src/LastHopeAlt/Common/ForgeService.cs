#nullable enable
using LastHopeAlt.Content.Items;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace LastHopeAlt.Common;

public readonly record struct IngredientSlot(int Chest, int Slot, int Type);
public sealed record ForgeStatus(bool Valid, HashSet<int> Present, List<IngredientSlot> Plan, string Message)
{
    public bool Complete => Valid && Present.Count == AccessoryCatalog.Entries.Count && Present.Count > 0;
}

public static class ForgeService
{
    public const int ChestRadius = 10;
    public const int PlayerReach = 6;

    public static Point BenchOrigin(int x, int y)
    {
        if (!WorldGen.InWorld(x, y, 2)) return new Point(-1, -1);
        Tile tile = Main.tile[x, y];
        return tile.HasTile && tile.TileType == TileID.WorkBenches
            ? new Point(x - tile.TileFrameX / 18 % 2, y) : new Point(-1, -1);
    }

    public static bool InChestRange(Point bench, Point chest)
    {
        // Nearest tile-to-tile distance between a 2x1 workbench and a 2x2 chest.
        int dx = Math.Max(0, Math.Max(bench.X - (chest.X + 1), chest.X - (bench.X + 1)));
        int dy = Math.Max(0, Math.Max(bench.Y - (chest.Y + 1), chest.Y - bench.Y));
        return dx * dx + dy * dy <= ChestRadius * ChestRadius;
    }

    public static bool CanUse(Player player, Point bench)
    {
        if (!player.active || player.dead || bench.X < 0 || BenchOrigin(bench.X, bench.Y) != bench) return false;
        return Vector2.DistanceSquared(player.Center, new Vector2((bench.X + 1) * 16, (bench.Y + .5f) * 16))
            <= PlayerReach * PlayerReach * 256;
    }

    public static ForgeStatus Inspect(Player player, Point bench)
    {
        if (!CanUse(player, bench)) return new(false, new(), new(), "Stand near a workbench to reforge the relic.");
        var present = new HashSet<int>();
        var plan = new List<IngredientSlot>();
        bool openChest = false;
        for (int c = 0; c < Main.maxChests; c++)
        {
            Chest? chest = Main.chest[c];
            if (chest == null || !InChestRange(bench, new Point(chest.x, chest.y)) || Chest.IsLocked(chest.x, chest.y)) continue;
            // Never race a player's chest inventory edits, including the crafter's own open chest.
            if (Main.player.Any(p => p != null && p.active && p.chest == c)) { openChest = true; continue; }
            for (int s = 0; s < Chest.maxItems; s++)
            {
                Item item = chest.item[s];
                if (item != null && !item.IsAir && item.stack > 0 && AccessoryCatalog.Contains(item.type) && present.Add(item.type))
                    plan.Add(new(c, s, item.type));
            }
        }
        string message = openChest ? "Close nearby chests so the forge can inspect them." : "Unlocked, closed chests within 10 tiles of this workbench.";
        return new(true, present, plan, message);
    }

    // Called only on the game thread, on the server or in singleplayer. A client never consumes ingredients.
    public static ForgeStatus Craft(Player player, Point bench)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient) throw new InvalidOperationException("Server-only forge");
        ForgeStatus status = Inspect(player, bench);
        if (!status.Complete) return status with { Message = "The collection is incomplete. No items were consumed." };
        if (!Main.item.Take(Main.maxItems).Any(i => !i.active))
            return status with { Message = "No room for a world item. Pick up nearby drops first." };

        // Revalidate the entire transaction before changing any stack.
        foreach (IngredientSlot slot in status.Plan)
        {
            Item item = Main.chest[slot.Chest].item[slot.Slot];
            if (item.type != slot.Type || item.stack < 1)
                return status with { Message = "The collection changed. Refresh and try again." };
        }
        Item[] originals = status.Plan.Select(s => Main.chest[s.Chest].item[s.Slot].Clone()).ToArray();
        int drop = -1;
        try
        {
            foreach (IngredientSlot slot in status.Plan)
            {
                Item item = Main.chest[slot.Chest].item[slot.Slot];
                if (--item.stack == 0) item.TurnToAir();
            }
            drop = player.QuickSpawnItem(player.GetSource_Misc("LastHopeForge"), ModContent.ItemType<TheLastHope>());
            if (drop < 0 || drop >= Main.maxItems || !Main.item[drop].active || Main.item[drop].type != ModContent.ItemType<TheLastHope>())
                throw new InvalidOperationException("The forge output could not be created");
            Main.item[drop].playerIndexTheItemIsReservedFor = player.whoAmI;
            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.SyncItem, -1, -1, null, drop);
        }
        catch (Exception ex)
        {
            for (int i = 0; i < status.Plan.Count; i++)
            {
                IngredientSlot slot = status.Plan[i];
                Main.chest[slot.Chest].item[slot.Slot] = originals[i];
            }
            if (drop >= 0 && drop < Main.maxItems)
            {
                Main.item[drop].TurnToAir();
                if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncItem, -1, -1, null, drop);
            }
            LastHope.Instance.Logger.Error("Forge failed; ingredients restored", ex);
            return status with { Message = "Forging failed. All ingredients were restored; see the log." };
        }
        if (Main.netMode == NetmodeID.Server)
            foreach (IngredientSlot slot in status.Plan)
                NetMessage.SendData(MessageID.SyncChestItem, -1, -1, null, slot.Chest, slot.Slot);
        return Inspect(player, bench) with { Message = "The Last Hope Alt has been reforged. Your relic is at your feet." };
    }
}
