#nullable enable
using LastHopeAlt.Common;
using LastHopeAlt.UI;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace LastHopeAlt;

public enum HopeMessage : byte { Inspect, Forge, Status, Options }

public sealed class LastHope : Mod
{
    internal static LastHope Instance = null!;
    internal static ModKeybind AbilityKey = null!;
    internal static ModKeybind ForgeKey = null!;
    private readonly ulong[] lastRequests = new ulong[Main.maxPlayers];
    private readonly bool[] requestedBefore = new bool[Main.maxPlayers];
    private readonly ulong[] lastForges = new ulong[Main.maxPlayers];
    private readonly bool[] forgedBefore = new bool[Main.maxPlayers];

    public override void Load()
    {
        Instance = this;
        ForgeKey = KeybindLoader.RegisterKeybind(this, "OpenForge", "P");
    }

    public override void HandlePacket(BinaryReader reader, int whoAmI)
    {
        var kind = (HopeMessage)reader.ReadByte();
        if (kind is HopeMessage.Inspect or HopeMessage.Forge)
        {
            if (Main.netMode != NetmodeID.Server || whoAmI < 0 || whoAmI >= Main.maxPlayers) return;
            Point bench = new(reader.ReadInt32(), reader.ReadInt32());
            ulong now = Main.GameUpdateCount;
            if (kind == HopeMessage.Forge)
            {
                if (forgedBefore[whoAmI] && now - lastForges[whoAmI] < 30) return;
                forgedBefore[whoAmI] = true;
                lastForges[whoAmI] = now;
            }
            else
            {
                if (requestedBefore[whoAmI] && now - lastRequests[whoAmI] < 15) return;
                requestedBefore[whoAmI] = true;
                lastRequests[whoAmI] = now;
            }
            Player player = Main.player[whoAmI];
            ForgeStatus status = kind == HopeMessage.Forge ? ForgeService.Craft(player, bench) : ForgeService.Inspect(player, bench);
            SendStatus(whoAmI, bench, status);
        }
        else if (kind == HopeMessage.Status)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient) return;
            Point bench = new(reader.ReadInt32(), reader.ReadInt32());
            bool valid = reader.ReadBoolean();
            string message = reader.ReadString();
            int count = reader.ReadUInt16();
            if (count > AccessoryCatalog.Entries.Count) return;
            var present = new HashSet<int>();
            for (int i = 0; i < count; i++)
            {
                int type = reader.ReadInt32();
                if (AccessoryCatalog.Contains(type)) present.Add(type);
            }
            ForgeUI.Receive(bench, new(valid, present, new(), message));
        }
        else if (kind == HopeMessage.Options)
        {
            int index = reader.ReadByte();
            if (Main.netMode == NetmodeID.Server) index = whoAmI;
            if (index < 0 || index >= Main.maxPlayers) return;
            try
            {
                Main.player[index].GetModPlayer<HopePlayer>().Options = HopeOptions.Read(reader);
                if (Main.netMode == NetmodeID.Server) Main.player[index].GetModPlayer<HopePlayer>().SyncPlayer(-1, whoAmI, false);
            }
            catch (InvalidDataException ex) { Logger.Warn(ex.Message); }
        }
    }

    internal static void Request(Point bench, bool craft)
    {
        if (Main.netMode == NetmodeID.SinglePlayer)
        {
            ForgeUI.Receive(bench, craft ? ForgeService.Craft(Main.LocalPlayer, bench) : ForgeService.Inspect(Main.LocalPlayer, bench));
            return;
        }
        var packet = Instance.GetPacket();
        packet.Write((byte)(craft ? HopeMessage.Forge : HopeMessage.Inspect));
        packet.Write(bench.X);
        packet.Write(bench.Y);
        packet.Send();
    }

    private void SendStatus(int to, Point bench, ForgeStatus status)
    {
        var packet = GetPacket();
        packet.Write((byte)HopeMessage.Status);
        packet.Write(bench.X);
        packet.Write(bench.Y);
        packet.Write(status.Valid);
        packet.Write(status.Message);
        packet.Write((ushort)status.Present.Count);
        foreach (int type in status.Present.Order()) packet.Write(type);
        packet.Send(to);
    }

    public override void Unload()
    {
        Instance = null!;
        AbilityKey = null!;
        ForgeKey = null!;
    }
}
