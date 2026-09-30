using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Polykov.Inventory;
using Polykov.Lobby;
using Polykov.UI.Framework;
using Polykov.Weapons;
using SkiaSharp;

namespace Polykov.Tools.UiPreview
{
    /// <summary>
    /// dotnet run -- framing                      -> camera shots (JSON lines) for Blender backgrounds
    /// dotnet run -- render out/ backgrounds/ icons/ -> one PNG per scenario, drawn by the real lobby UI code
    /// </summary>
    public static class Program
    {
        private const int W = 1920, H = 1080;
        private static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));

        public static int Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "framing") return Framing();
            if (args.Length >= 4 && args[0] == "render") return Render(args[1], args[2], args[3]);
            Console.Error.WriteLine("usage: framing | render <outDir> <backgroundDir> <iconDir>");
            return 1;
        }

        // ------------------------------------------------------------------ setup

        private static (LobbyContext ctx, LobbyScreens screens) NewLobby(string iconDir)
        {
            var db = ItemDatabase.Default();
            var catalog = AttachmentCatalog.M1911();
            var ctx = new LobbyContext
            {
                Db = db,
                Profile = StarterKit.Create(db, WeaponBuild.M1911Default, catalog), Clock = "21:47",
                Icons = new PreviewIcons(iconDir),
            };
            ctx.Profile.Nickname = "Cobos";
            ctx.Profile.Level = 4;
            return (ctx, new LobbyScreens(ctx));
        }

        private static UiRect CharacterArea(LobbyScreens screens, Ui ui, LobbyView view)
            => view == LobbyView.Character ? screens.Inventory.CharacterArea : screens.MainMenuCharacterArea(ui);

        private static int Framing()
        {
            foreach (LobbyView view in new[] { LobbyView.MainMenu, LobbyView.Character })
            {
                var (ctx, screens) = NewLobby(null);
                var input = new UiInput();
                var backend = new SkiaBackend(Path.Combine(Root, "Assets/_Project/Resources/Fonts"));
                using var bmp = new SKBitmap(W, H);
                using var canvas = new SKCanvas(bmp);
                backend.Canvas = canvas;
                var ui = new Ui(backend, input);
                screens.Tab = view == LobbyView.Character ? LobbyTab.Character : LobbyTab.MainMenu;
                ui.BeginFrame(W, H, 0.1f);
                screens.Frame(ui);
                ui.EndFrame();
                StageFraming.Shot s = StageFraming.Character(CharacterArea(screens, ui, view), W, H, 30f);
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0} {1} {2} {3} {4} {5} {6} {7}",
                    view, s.PosX, s.PosY, s.PosZ, s.LookX, s.LookY, s.LookZ, s.Fov));
            }
            return 0;
        }

        // ------------------------------------------------------------------ scenarios

        private sealed class Step
        {
            public UiVec Mouse;
            public bool Left, Right, Ctrl, Double;
            public UiKey? Key;
        }

        private static int Render(string outDir, string bgDir, string iconDir)
        {
            Directory.CreateDirectory(outDir);
            var backend = new SkiaBackend(Path.Combine(Root, "Assets/_Project/Resources/Fonts"));

            Run("01_menu_principal", "bg_main.png", (ctx, s, ui) => s.Tab = LobbyTab.MainMenu,
                (ctx, s, ui) => new UiVec(W * 0.32f, H * 0.2f + 320f), null);

            Run("02_personaje", "bg_character.png", (ctx, s, ui) => s.Tab = LobbyTab.Character,
                (ctx, s, ui) => Center(s, FindInStash(ctx, "helmet")), null);

            // Drag the helmet from the stash onto the headwear slot (occupied by the cap: swap, green).
            Run("03_arrastrar", "bg_character.png", (ctx, s, ui) => s.Tab = LobbyTab.Character, null, (ctx, s) =>
            {
                UiVec from = Center(s, FindInStash(ctx, "helmet"));
                s.Inventory.TryGetSlotRect(EquipSlot.Headwear, out UiRect slot);
                return new List<Step>
                {
                    new Step { Mouse = from }, new Step { Mouse = from, Left = true }, new Step { Mouse = from + new UiVec(30, 10), Left = true },
                    new Step { Mouse = slot.Center + new UiVec(8, 14), Left = true }, new Step { Mouse = slot.Center + new UiVec(8, 14), Left = true },
                };
            });

            // Drag ammo into a pocket that is taken (red), with the context menu of the pistol for comparison in 04.
            Run("04_menu_contextual", "bg_character.png", (ctx, s, ui) => s.Tab = LobbyTab.Character, null, (ctx, s) =>
            {
                UiVec at = Center(s, ctx.Profile.Equipped(EquipSlot.Holster));
                return new List<Step> { new Step { Mouse = at }, new Step { Mouse = at, Right = true }, new Step { Mouse = at + new UiVec(60, 64) } };
            });

            Run("05_inspeccionar", "bg_character.png", (ctx, s, ui) =>
            {
                s.Tab = LobbyTab.Character;
                s.Inventory.ShowWindow(FindInStash(ctx, "ammo_case"), false, new UiVec(1250, 520));
                s.Inventory.ShowWindow(ctx.Profile.Equipped(EquipSlot.Holster), true, new UiVec(640, 150));
            }, (ctx, s, ui) => new UiVec(1000, 900), null);

            Run("06_armero", "bg_armorer.png", (ctx, s, ui) =>
            {
                s.OpenArmorer(ctx.Profile.Equipped(EquipSlot.Holster));
                s.Armorer.SelectSlot(AttachmentSlot.Muzzle);
                ctx.SlotAnchor = Anchors(Path.Combine(bgDir, "bg_armorer_anchors.txt"));
            }, (ctx, s, ui) => new UiVec(1500, 220), null);

            Run("07_armero_silenciador", "bg_armorer_suppressor.png", (ctx, s, ui) =>
            {
                Item gun = ctx.Profile.Equipped(EquipSlot.Holster);
                WeaponBuild next = ArmorerModel.Select(WeaponBuild.M1911Default, AttachmentCatalog.M1911(), AttachmentSlot.Muzzle, "suppressor_45");
                WeaponParts.Apply(ctx.Profile, gun, WeaponBuild.M1911Default, next, out _);
                s.OpenArmorer(gun);
                s.Armorer.SelectSlot(AttachmentSlot.Barrel);
                ctx.SlotAnchor = Anchors(Path.Combine(bgDir, "bg_armorer_suppressor_anchors.txt"));
            }, (ctx, s, ui) => new UiVec(1700, 330), null);

            Run("08_personaje_silenciador", "bg_character.png", (ctx, s, ui) =>
            {
                Item gun = ctx.Profile.Equipped(EquipSlot.Holster);
                WeaponBuild next = ArmorerModel.Select(WeaponBuild.M1911Default, AttachmentCatalog.M1911(), AttachmentSlot.Muzzle, "suppressor_45");
                WeaponParts.Apply(ctx.Profile, gun, WeaponBuild.M1911Default, next, out _);
                s.Tab = LobbyTab.Character;
                s.Inventory.ShowWindow(ctx.Profile.Equipped(EquipSlot.Backpack), false, new UiVec(820, 330));
            }, (ctx, s, ui) => new UiVec(300, 1000), null);
            // In-raid pause menu over the main-menu backdrop (the arena has no offline render yet).
            {
                var input = new UiInput();
                var ui = new Ui(backend, input);
                using var bmp = new SKBitmap(W, H);
                using var canvas = new SKCanvas(bmp);
                backend.Canvas = canvas;
                SKImage background = LoadImage(Path.Combine(bgDir, "bg_character.png"));
                var settings = new PauseSettings { Presets = new[] { "Default", "Tactical (heavy)", "Arcade (snappy)" }, Preset = 1, Gore = 2 };
                for (int i = 0; i < 3; i++)
                {
                    input.BeginFrame(new UiVec(700, 900 - 60 + 30 + 20));
                    canvas.Clear(new SKColor(9, 10, 11));
                    if (background != null) canvas.DrawImage(background, new SKRect(0, 0, W, H));
                    ui.BeginFrame(W, H, 0.1f * (i + 1));
                    PauseScreen.Frame(ui, settings);
                    ui.EndFrame();
                }
                string path = Path.Combine(outDir, "09_pausa.png");
                using (var f = File.Create(path)) bmp.Encode(f, SKEncodedImageFormat.Png, 95);
                Console.WriteLine("wrote " + path);
            }
            return 0;

            void Run(string name, string bg, Action<LobbyContext, LobbyScreens, Ui> setup, Func<LobbyContext, LobbyScreens, Ui, UiVec> hover,
                Func<LobbyContext, LobbyScreens, List<Step>> steps)
            {
                var (ctx, screens) = NewLobby(iconDir);
                var input = new UiInput();
                var ui = new Ui(backend, input);
                using var bmp = new SKBitmap(W, H);
                using var canvas = new SKCanvas(bmp);
                backend.Canvas = canvas;
                SKImage background = LoadImage(Path.Combine(bgDir, bg));
                float t = 0f;

                void Frame(Step st)
                {
                    t += 1f / 60f;
                    input.BeginFrame(st.Mouse);
                    input.SetButton(0, st.Left);
                    input.SetButton(1, st.Right);
                    input.Ctrl = st.Ctrl;
                    input.DoubleClick = st.Double;
                    if (st.Key.HasValue) input.PressKey(st.Key.Value);
                    canvas.Clear(new SKColor(9, 10, 11));
                    if (background != null) canvas.DrawImage(background, new SKRect(0, 0, W, H));
                    ui.BeginFrame(W, H, t);
                    screens.Frame(ui);
                    ui.EndFrame();
                    screens.Requests.Clear();
                }

                setup(ctx, screens, ui);
                Frame(new Step { Mouse = new UiVec(-100, -100) });   // lay out once
                Frame(new Step { Mouse = new UiVec(-100, -100) });
                if (hover != null)
                {
                    UiVec m = hover(ctx, screens, ui);
                    for (int i = 0; i < 3; i++) Frame(new Step { Mouse = m });
                }
                if (steps != null) foreach (Step st in steps(ctx, screens)) Frame(st);

                string path = Path.Combine(outDir, name + ".png");
                using (var f = File.Create(path)) bmp.Encode(f, SKEncodedImageFormat.Png, 95);
                Console.WriteLine("wrote " + path);
            }
        }

        private static Item FindInStash(LobbyContext ctx, string id)
        {
            foreach (Placement p in ctx.Profile.Stash.Placements) if (p.Item.Def.Id == id) return p.Item;
            throw new InvalidOperationException("no " + id + " in the stash");
        }

        private static UiVec Center(LobbyScreens s, Item item)
            => s.Inventory.TryGetItemRect(item, out UiRect r) ? r.Center : new UiVec(-100, -100);

        private static Func<AttachmentSlot, UiVec?> Anchors(string file)
        {
            var map = new Dictionary<AttachmentSlot, UiVec>();
            if (File.Exists(file))
                foreach (string line in File.ReadAllLines(file))
                {
                    string[] f = line.Split(' ');
                    if (f.Length == 3 && Enum.TryParse(f[0], out AttachmentSlot slot))
                        map[slot] = new UiVec(float.Parse(f[1], CultureInfo.InvariantCulture), float.Parse(f[2], CultureInfo.InvariantCulture));
                }
            return slot => map.TryGetValue(slot, out UiVec v) ? v : (UiVec?)null;
        }

        private static SKImage LoadImage(string path)
        {
            if (!File.Exists(path)) return null;
            using var data = SKData.Create(path);
            return SKImage.FromEncodedData(data);
        }

        /// <summary>Blender-rendered icons for items with a 3D model (same idea as the Unity runtime renderer), else pictograms.</summary>
        private sealed class PreviewIcons : IItemIcons
        {
            private readonly string _dir;
            private readonly Dictionary<string, UiImage> _cache = new Dictionary<string, UiImage>();

            public PreviewIcons(string dir) { _dir = dir; }

            public UiImage Get(Item item, bool rotated)
            {
                string name = item.Def.Id;
                if (item.Def.Id == "m1911a1" && item.Build != null && item.Build.Contains("suppressor_45")) name += "_suppressed";
                string key = name + (rotated ? "/r" : "");
                if (_cache.TryGetValue(key, out UiImage cached)) return cached;
                UiImage img = null;
                string file = _dir != null ? Path.Combine(_dir, name + ".png") : null;
                if (file != null && File.Exists(file))
                {
                    SKImage src = LoadImage(file);
                    if (rotated)
                    {
                        using var surface = SKSurface.Create(new SKImageInfo(src.Height, src.Width));
                        surface.Canvas.Translate(src.Height, 0);
                        surface.Canvas.RotateDegrees(90);
                        surface.Canvas.DrawImage(src, 0, 0);
                        src = surface.Snapshot();
                    }
                    img = new UiImage(src, src.Width, src.Height);
                }
                img ??= ItemPictograms.Get(item.Def, rotated);
                _cache[key] = img;
                return img;
            }
        }
    }
}
