#nullable enable
using LastHopeAlt.UI;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace LastHopeAlt.Common;

public sealed class WorkbenchInteraction : GlobalTile
{
    public override void RightClick(int i, int j, int type)
    {
        if (type != TileID.WorkBenches) return;
        ForgeUI.Open(ForgeService.BenchOrigin(i, j));
    }
}
