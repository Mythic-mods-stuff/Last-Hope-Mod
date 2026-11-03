#nullable enable
using LastHopeAlt.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.ModLoader;
using Terraria.UI;

namespace LastHopeAlt.UI;

public sealed class ForgeUI : ModSystem
{
    private static UserInterface? userInterface;
    private static ForgeState? state;
    private static Point bench;
    private static bool visible;
    private static GameTime lastTime = new();
    private static ulong refreshed;

    public override void Load()
    {
        if (Main.dedServ) return;
        userInterface = new UserInterface();
        state = new ForgeState();
        state.Activate();
    }

    public static void Open(Point origin)
    {
        if (Main.dedServ || origin.X < 0 || state == null) return;
        bench = origin;
        visible = true;
        Main.playerInventory = true;
        userInterface!.SetState(state);
        Refresh();
    }

    private static void Refresh()
    {
        refreshed = Main.GameUpdateCount;
        LastHope.Request(bench, false);
    }

    public static void Receive(Point origin, ForgeStatus status)
    {
        if (visible && origin == bench) state?.Show(status);
    }

    public static void Close()
    {
        visible = false;
        userInterface?.SetState(null);
    }

    public override void UpdateUI(GameTime gameTime)
    {
        lastTime = gameTime;
        if (!visible) return;
        if (!Main.playerInventory || !ForgeService.CanUse(Main.LocalPlayer, bench)) { Close(); return; }
        userInterface?.Update(gameTime);
        if (Main.GameUpdateCount - refreshed >= 120) Refresh();
    }

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        if (!visible) return;
        int index = layers.FindIndex(x => x.Name == "Vanilla: Mouse Text");
        if (index < 0) index = layers.Count;
        layers.Insert(index, new LegacyGameInterfaceLayer("LastHope: Forge", () =>
        {
            userInterface?.Draw(Main.spriteBatch, lastTime);
            return true;
        }, InterfaceScaleType.UI));
    }

    public override void Unload()
    {
        userInterface = null;
        state = null;
        visible = false;
    }

    private sealed class ForgeState : UIState
    {
        private readonly UIText progress = new("Inspecting the collection...");
        private readonly UIText message = new("Close nearby chests before forging.", .8f);
        private readonly UIList list = new();
        private readonly UITextPanel<string> forge = new("Forge The Last Hope Alt");
        private ForgeStatus? status;
        private bool missingOnly;

        public override void OnInitialize()
        {
            var panel = new UIPanel();
            panel.Width.Set(720, 0);
            panel.Height.Set(530, 0);
            panel.HAlign = panel.VAlign = .5f;
            panel.BackgroundColor = new Color(24, 21, 42, 245);
            panel.BorderColor = new Color(220, 177, 93);
            panel.OnMouseOver += (_, _) => Main.LocalPlayer.mouseInterface = true;
            Append(panel);
            var title = new UIText("THE LAST HOPE", 1.2f, true);
            title.Left.Set(8, 0); title.Top.Set(2, 0); panel.Append(title);
            var close = Button("Close", 606, 0, 80, 30);
            close.OnLeftClick += (_, _) => Close(); panel.Append(close);
            progress.Left.Set(8, 0); progress.Top.Set(48, 0); panel.Append(progress);
            message.Left.Set(8, 0); message.Top.Set(77, 0); panel.Append(message);
            var hint = new UIText("One of each accessory • vanilla + Calamity • 10-tile chest radius", .8f);
            hint.Left.Set(8, 0); hint.Top.Set(99, 0); panel.Append(hint);
            list.Left.Set(8, 0); list.Top.Set(130, 0);
            list.Width.Set(-40, 1); list.Height.Set(305, 0); list.ListPadding = 3;
            panel.Append(list);
            var scrollbar = new UIScrollbar();
            scrollbar.Left.Set(-22, 1); scrollbar.Top.Set(130, 0);
            scrollbar.Height.Set(305, 0); scrollbar.SetView(100, 1000);
            panel.Append(scrollbar); list.SetScrollbar(scrollbar);
            forge.Left.Set(8, 0); forge.Top.Set(453, 0); forge.Width.Set(250, 0); forge.Height.Set(36, 0);
            forge.OnLeftClick += (_, _) =>
            {
                if (status?.Complete != true) return;
                forge.SetText("Forging...");
                LastHope.Request(bench, true);
                refreshed = Main.GameUpdateCount;
            };
            panel.Append(forge);
            var refresh = Button("Refresh", 272, 453, 120, 36);
            refresh.OnLeftClick += (_, _) => Refresh(); panel.Append(refresh);
            var filter = Button("Show missing", 406, 453, 190, 36);
            filter.OnLeftClick += (_, _) =>
            {
                missingOnly = !missingOnly;
                filter.SetText(missingOnly ? "Show all" : "Show missing");
                if (status != null) Show(status, true);
            };
            panel.Append(filter);
        }

        private static UITextPanel<string> Button(string text, int x, int y, int width, int height)
        {
            var button = new UITextPanel<string>(text, .8f);
            button.Left.Set(x, 0); button.Top.Set(y, 0);
            button.Width.Set(width, 0); button.Height.Set(height, 0);
            return button;
        }

        public void Show(ForgeStatus next, bool forceList = false)
        {
            bool sameCollection = status != null && status.Present.SetEquals(next.Present);
            status = next;
            progress.SetText($"Collection: {next.Present.Count} / {AccessoryCatalog.Entries.Count} accessories");
            message.SetText(next.Message);
            progress.TextColor = next.Complete ? Color.LightGreen : Color.Gold;
            forge.SetText(next.Complete ? "Forge The Last Hope Alt" : "Collection incomplete");
            forge.BackgroundColor = next.Complete ? new Color(85, 69, 28) : new Color(45, 43, 52);
            if (sameCollection && !forceList) return;
            list.Clear();
            foreach (AccessoryEntry entry in AccessoryCatalog.Entries.OrderBy(x => x.Calamity).ThenBy(x => x.Name))
            {
                bool present = next.Present.Contains(entry.Type);
                if (missingOnly && present) continue;
                list.Add(new AccessoryRow(entry, present));
            }
            Recalculate();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            foreach (UIElement child in Elements)
                if (child.ContainsPoint(Main.MouseScreen)) Main.LocalPlayer.mouseInterface = true;
        }
    }

    private sealed class AccessoryRow : UIElement
    {
        private readonly Item item;
        private readonly string text;
        private readonly bool present;
        public AccessoryRow(AccessoryEntry entry, bool present)
        {
            item = new Item(entry.Type);
            this.present = present;
            text = $"{(present ? "Collected" : "Missing")}  •  {entry.Name}  [{(entry.Calamity ? "Calamity" : "Terraria")}]";
            Width.Set(0, 1); Height.Set(34, 0);
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            CalculatedStyle dim = GetDimensions();
            spriteBatch.Draw(TextureAssets.MagicPixel.Value, dim.ToRectangle(), new Color(255, 255, 255, IsMouseHovering ? 22 : 7));
            Main.instance.LoadItem(item.type);
            Texture2D texture = TextureAssets.Item[item.type].Value;
            Rectangle frame = Main.itemAnimations[item.type]?.GetFrame(texture) ?? texture.Bounds;
            float scale = Math.Min(26f / frame.Width, 26f / frame.Height);
            spriteBatch.Draw(texture, dim.Position() + new Vector2(17, 17), frame, Color.White, 0, frame.Size() / 2, scale, SpriteEffects.None, 0);
            Utils.DrawBorderString(spriteBatch, text, dim.Position() + new Vector2(38, 9), present ? Color.LightGreen : Color.Silver, .72f);
            if (IsMouseHovering)
            {
                Main.LocalPlayer.mouseInterface = true;
                Main.HoverItem = item.Clone();
                Main.hoverItemName = item.Name;
            }
        }
    }
}
