using Polykov.Inventory;
using Polykov.UI.Framework;

namespace Polykov.Lobby
{
    public enum LobbyTab : byte { MainMenu, Hideout, Character, Traders, Flea, Armorer }

    /// <summary>
    /// The whole lobby UI (engine-free): top status bar, bottom navigation tabs like Escape from Tarkov, and the
    /// screens behind them. The host draws the 3D stage for <see cref="View"/> and acts on <see cref="Requests"/>.
    /// </summary>
    public sealed class LobbyScreens
    {
        public const float TopBarHeight = 40f;
        public const float BottomBarHeight = 50f;

        private readonly LobbyContext _ctx;
        public readonly LobbyRequests Requests = new LobbyRequests();
        public readonly InventoryView Inventory;
        public readonly ArmorerView Armorer;
        public LobbyTab Tab = LobbyTab.MainMenu;
        private float _fade;

        public LobbyScreens(LobbyContext ctx)
        {
            _ctx = ctx;
            Inventory = new InventoryView(ctx, Requests);
            Armorer = new ArmorerView(ctx, Requests);
            Inventory.OpenArmorer = OpenArmorer;
            Armorer.Back = () => Tab = LobbyTab.Character;
        }

        public LobbyView View => Tab == LobbyTab.Armorer ? LobbyView.Armorer : Tab == LobbyTab.Character ? LobbyView.Character : LobbyView.MainMenu;

        /// <summary>True while entering a raid (the host shows the fade and loads the scene).</summary>
        public bool Loading { get; private set; }

        public void OpenArmorer(Item weapon)
        {
            Armorer.Weapon = weapon;
            Tab = LobbyTab.Armorer;
        }

        public void Frame(Ui ui)
        {
            var content = new UiRect(12f, TopBarHeight + 12f, ui.Width - 24f, ui.Height - TopBarHeight - BottomBarHeight - 24f);
            switch (Tab)
            {
                case LobbyTab.Character: Inventory.Frame(ui, content); break;
                case LobbyTab.Armorer:
                    if (Armorer.Weapon == null) Armorer.Weapon = _ctx.Profile.Equipped(EquipSlot.Holster);
                    Armorer.Frame(ui, content);
                    break;
                default: MainMenu(ui); break;
            }
            TopBar(ui);
            BottomBar(ui);
            if (Loading)
            {
                _fade = System.Math.Min(1f, _fade + ui.DeltaTime * 3f);
                ui.Fill(new UiRect(0, 0, ui.Width, ui.Height), new UiColor(0, 0, 0, _fade));
                ui.SpacedText(new UiRect(0, ui.Height * 0.5f - 20f, ui.Width, 40f), "CARGANDO", UiTheme.SizeTitle, UiFont.Bold, 6f,
                    UiTheme.Text.WithAlpha(_fade));
                if (_fade >= 1f) Requests.EnterGame = true;
            }
        }

        private void StartRaid()
        {
            if (Loading) return;
            Loading = true;
            _fade = 0f;
        }

        // ------------------------------------------------------------------ bars

        private void TopBar(Ui ui)
        {
            var bar = new UiRect(0, 0, ui.Width, TopBarHeight);
            ui.Fill(bar, UiTheme.Bar);
            ui.Fill(new UiRect(0, bar.YMax - 1f, ui.Width, 1f), UiTheme.Border);
            ui.SpacedText(new UiRect(20f, 0, 400f, TopBarHeight), "ESCAPE FROM POLYKOV", UiTheme.SizeBody, UiFont.Bold, 2f, UiTheme.Text, UiAlign.Left);

            Profile p = _ctx.Profile;
            float x = ui.Width - 20f;
            x = Chip(ui, x, "₽ " + Format.Thousands(p.Money), UiTheme.Text);
            x = Chip(ui, x, Format.Kg(p.CarriedWeight), UiTheme.TextDim);
            x = Chip(ui, x, p.Nickname.ToUpperInvariant(), UiTheme.TextBright);
            // Level badge.
            var badge = new UiRect(x - 34f, 7f, 26f, 26f);
            ui.Fill(badge, UiColor.Hex(0x2A2C2D));
            ui.Frame(badge, UiTheme.BorderLight);
            ui.Label(badge, p.Level.ToString(), UiTheme.SizeBody, UiFont.Bold, UiAlign.Center, UiTheme.TextBright);
        }

        private static float Chip(Ui ui, float right, string text, UiColor color)
        {
            float w = ui.Draw.TextWidth(text, UiFont.Bold, UiTheme.SizeBody);
            var r = new UiRect(right - w, 0f, w, TopBarHeight);
            ui.Label(r, text, UiTheme.SizeBody, UiFont.Bold, UiAlign.Right, color);
            ui.Fill(new UiRect(r.X - 14f, 12f, 1f, TopBarHeight - 24f), UiTheme.Border);
            return r.X - 28f;
        }

        private static readonly (LobbyTab tab, string text, bool enabled)[] Tabs =
        {
            (LobbyTab.MainMenu, "MENÚ PRINCIPAL", true), (LobbyTab.Hideout, "ESCONDITE", false), (LobbyTab.Character, "PERSONAJE", true),
            (LobbyTab.Traders, "COMERCIANTES", false), (LobbyTab.Flea, "MERCADILLO", false), (LobbyTab.Armorer, "ARMERO", true),
        };

        private void BottomBar(Ui ui)
        {
            var bar = new UiRect(0, ui.Height - BottomBarHeight, ui.Width, BottomBarHeight);
            ui.Fill(bar, UiTheme.Bar);
            ui.Fill(new UiRect(0, bar.Y, ui.Width, 1f), UiTheme.Border);
            float tabW = 170f, total = tabW * Tabs.Length;
            float x = (ui.Width - total) * 0.5f;
            foreach (var (tab, text, enabled) in Tabs)
            {
                var r = new UiRect(x, bar.Y + 1f, tabW, BottomBarHeight - 1f);
                if (ui.Tab(r, text, Tab == tab, enabled && !Loading))
                {
                    if (tab == LobbyTab.Armorer) OpenArmorer(Armorer.Weapon ?? _ctx.Profile.Equipped(EquipSlot.Holster));
                    else Tab = tab;
                }
                x += tabW;
            }
            ui.Label(new UiRect(20f, bar.Y, 300f, BottomBarHeight), _ctx.Version, UiTheme.SizeSmall, UiFont.Regular, UiAlign.Left, UiTheme.TextDisabled);
            ui.Label(new UiRect(ui.Width - 220f, bar.Y, 200f, BottomBarHeight), _ctx.Clock, UiTheme.SizeLabel, UiFont.Bold, UiAlign.Right, UiTheme.Text);
        }

        // ------------------------------------------------------------------ main menu

        /// <summary>Screen rect where the 3D character stands on the main menu (right side).</summary>
        public UiRect MainMenuCharacterArea(Ui ui) => new UiRect(ui.Width * 0.58f, TopBarHeight + 60f, ui.Width * 0.3f, ui.Height - TopBarHeight - BottomBarHeight - 160f);

        private void MainMenu(Ui ui)
        {
            float cx = ui.Width * 0.32f;
            float y = ui.Height * 0.2f;
            ui.SpacedText(new UiRect(cx - 400f, y, 800f, 46f), "ESCAPE FROM", 34, UiFont.Bold, 10f, UiTheme.Text);
            ui.SpacedText(new UiRect(cx - 400f, y + 40f, 800f, 150f), "POLYKOV", 132, UiFont.Bold, 14f, UiTheme.TextBright);
            ui.Fill(new UiRect(cx - 250f, y + 196f, 500f, 1f), UiTheme.BorderLight);
            ui.SpacedText(new UiRect(cx - 400f, y + 204f, 800f, 24f), "EXTRACTION SHOOTER  ·  FASE 1", UiTheme.SizeSmall, UiFont.Regular, 3f, UiTheme.TextDim);

            float bw = 460f, bx = cx - bw * 0.5f;
            y += 280f;
            if (ui.Button(new UiRect(bx, y, bw, 74f), "ESCAPE FROM POLYKOV", ButtonStyle.Primary, null, 26)) StartRaid();
            ui.Label(new UiRect(bx, y + 78f, bw, 20f), "Arena de pruebas  ·  día  ·  sin límite de tiempo", UiTheme.SizeSmall, UiFont.Regular, UiAlign.Center, UiTheme.TextDim);
            y += 120f;
            if (ui.Button(new UiRect(bx, y, bw, 54f), "PERSONAJE", ButtonStyle.Normal, null, UiTheme.SizeLabel)) Tab = LobbyTab.Character;
            y += 64f;
            if (ui.Button(new UiRect(bx, y, bw, 54f), "ARMERO", ButtonStyle.Normal, null, UiTheme.SizeLabel))
                OpenArmorer(_ctx.Profile.Equipped(EquipSlot.Holster));
            y += 64f;
            ui.Button(new UiRect(bx, y, bw, 54f), "ESCONDITE", ButtonStyle.Locked, null, UiTheme.SizeLabel);

            // Loadout summary under the character.
            Item pistol = _ctx.Profile.Equipped(EquipSlot.Holster);
            UiRect area = MainMenuCharacterArea(ui);
            var info = new UiRect(area.X + area.W * 0.5f - 200f, area.YMax - 40f, 400f, 36f);
            ui.Fill(info, UiTheme.Panel);
            ui.Frame(info, UiTheme.Border);
            string loadout = (pistol != null ? pistol.Def.ShortName : "Sin arma") + "  ·  " + Format.Kg(_ctx.Profile.CarriedWeight);
            ui.Label(info, _ctx.Profile.Nickname.ToUpperInvariant() + "  ·  " + loadout, UiTheme.SizeBody, UiFont.Bold, UiAlign.Center, UiTheme.Text);
        }
    }
}
