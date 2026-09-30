using System;
using System.Collections.Generic;
using Polykov.Inventory;
using Polykov.UI.Framework;
using Polykov.Weapons;

namespace Polykov.Lobby
{
    /// <summary>
    /// The character screen (Tarkov "Character > Gear"): equipment slots around the 3D character, carried containers
    /// (rig, pockets, backpack, secure container) and the stash. Drag and drop with R to rotate, Ctrl+click to quick
    /// move, double-click to open containers, right-click for the context menu.
    /// </summary>
    public sealed class InventoryView
    {
        private const float C = UiTheme.Cell;
        private const float SlotSize = 110f;

        private readonly LobbyContext _ctx;
        private readonly LobbyRequests _req;

        private enum ZoneKind : byte { Grid, Slot, Card }

        private struct Zone
        {
            public ZoneKind Kind;
            public GridContainer Grid;
            public EquipSlot Slot;
            public UiRect Rect;
            public UiRect Clip;
            /// <summary>Card zones: the weapon whose slot card this is, and the slot.</summary>
            public Item Weapon;
            public AttachmentSlot CardSlot;
        }

        private sealed class Window
        {
            public Item Item;
            public bool Inspect;
            public UiVec Pos;
            public UiRect Rect;
        }

        private readonly List<Zone> _zones = new List<Zone>();
        private readonly Dictionary<Item, UiRect> _itemRects = new Dictionary<Item, UiRect>();
        private readonly Dictionary<EquipSlot, UiRect> _slotRects = new Dictionary<EquipSlot, UiRect>();
        private readonly List<Window> _windows = new List<Window>();
        private readonly List<UiRect> _windowRects = new List<UiRect>();

        // Press / drag state
        private Item _pressed;
        private UiVec _pressPos;
        private UiVec _pressGrab;
        private Item _drag;
        private bool _dragRot;
        private UiVec _grab;

        // Weapon modding by drag: a part taken out of a slot card is a stand-in item (never in the profile).
        private readonly Dictionary<ItemDef, Item> _proxies = new Dictionary<ItemDef, Item>();
        private Item _detachWeapon;
        private AttachmentSlot _detachSlot;
        // Cached "can this dragged part be mounted on that weapon" (recomputed only when the pair or the build changes).
        private Item _mountWeapon, _mountPart;
        private string _mountBuild;
        private bool _mountOk;
        private string _mountReason;
        private AttachmentSlot _mountSlot;
        private readonly Dictionary<int, string> _capacityText = new Dictionary<int, string>();

        // Context menu
        private Item _menuItem;
        private UiVec _menuPos;

        private Window _moving;
        private UiVec _moveOffset;
        private float _stashScroll;
        private string _message;
        private float _messageUntil;

        /// <summary>Raised by "Modificar" on a weapon.</summary>
        public Action<Item> OpenArmorer;
        /// <summary>In a raid the stash is out of reach: its panel is drawn disabled and takes no items.</summary>
        public bool RaidMode;
        /// <summary>Screen rect where the 3D character is shown (the host frames its camera on it).</summary>
        public UiRect CharacterArea { get; private set; }

        public InventoryView(LobbyContext ctx, LobbyRequests req)
        {
            _ctx = ctx;
            _req = req;
        }

        private Profile P => _ctx.Profile;

        // ================================================================== frame

        public void Frame(Ui ui, UiRect area)
        {
            _zones.Clear();
            _itemRects.Clear();
            _slotRects.Clear();
            _windowRects.Clear();
            foreach (Window w in _windows) _windowRects.Add(w.Rect);
            // Items that were discarded/merged away close their windows.
            _windows.RemoveAll(w => w.Item.Location == null && !w.Inspect);

            float gearW = 568f, contW = 588f, gap = 12f;
            var gear = new UiRect(area.X, area.Y, gearW, area.H);
            var cont = new UiRect(gear.XMax + gap, area.Y, contW, area.H);
            var stash = new UiRect(cont.XMax + gap, area.Y, area.XMax - cont.XMax - gap, area.H);

            DrawGear(ui, gear);
            DrawContainers(ui, cont);
            if (RaidMode) DrawStashLocked(ui, stash);
            else DrawStash(ui, stash);
            HandlePressAndDrag(ui);

            foreach (Window w in _windows)
            {
                Window win = w;
                ui.BlockArea(win.Rect);
                ui.Defer(() => DrawWindow(ui, win));
            }
            if (_menuItem != null) ui.Defer(() => DrawMenu(ui));
            ui.Defer(() => DrawDragAndDrop(ui));

            if (_message != null && ui.Time < _messageUntil)
            {
                float w = ui.Draw.TextWidth(_message, UiFont.Bold, UiTheme.SizeBody) + 40f;
                var r = new UiRect(area.X + (area.W - w) * 0.5f, area.YMax - 44f, w, 34f);
                ui.Fill(r, UiColor.Hex(0x2A0F0C, 0.95f));
                ui.Frame(r, UiTheme.Bad);
                ui.Label(r, _message, UiTheme.SizeBody, UiFont.Bold, UiAlign.Center, UiTheme.TextBright);
            }

            if (ui.Input.KeyPressed(UiKey.Escape) && (_menuItem != null || _windows.Count > 0 || _drag != null))
            {
                ui.Input.ConsumeKey(UiKey.Escape);
                if (_drag != null) EndDrag();
                else if (_menuItem != null) _menuItem = null;
                else _windows.RemoveAt(_windows.Count - 1);
            }
        }

        public bool Busy => _drag != null || _menuItem != null;

        /// <summary>Where an item / slot was drawn last frame (scripted previews, tutorials).</summary>
        public bool TryGetItemRect(Item item, out UiRect r) => _itemRects.TryGetValue(item, out r);
        public bool TryGetSlotRect(EquipSlot slot, out UiRect r) => _slotRects.TryGetValue(slot, out r);

        private void Say(Ui ui, string message)
        {
            _message = message;
            _messageUntil = ui.Time + 2.2f;
        }

        // ================================================================== gear panel

        private static readonly (EquipSlot slot, string label)[] LeftSlots =
        {
            (EquipSlot.Earpiece, "AURICULARES"), (EquipSlot.Headwear, "CASCO"), (EquipSlot.FaceCover, "CARA"),
            (EquipSlot.Eyewear, "GAFAS"), (EquipSlot.Armband, "BRAZALETE"),
        };

        private void DrawGear(Ui ui, UiRect r)
        {
            // See-through: the 3D character stands between the slots.
            UiRect inner = ui.Panel(r, "EQUIPO", P.Nickname.ToUpperInvariant() + "  ·  NIVEL " + P.Level, 0.3f);
            float x0 = inner.X + 12f, y0 = inner.Y + 12f;
            for (int i = 0; i < LeftSlots.Length; i++)
                DrawSlot(ui, new UiRect(x0, y0 + i * (SlotSize + 10f), SlotSize, SlotSize), LeftSlots[i].slot, LeftSlots[i].label);

            float rx = inner.XMax - 12f - SlotSize;
            DrawSlot(ui, new UiRect(rx, y0, SlotSize, SlotSize), EquipSlot.BodyArmor, "CHALECO BALÍSTICO");
            DrawHealth(ui, new UiRect(rx, y0 + SlotSize + 10f, SlotSize, 4 * (SlotSize + 10f) - 10f));

            CharacterArea = new UiRect(x0 + SlotSize + 8f, y0, rx - x0 - SlotSize - 16f, 5 * (SlotSize + 10f) - 10f);

            float wy = y0 + 5 * (SlotSize + 10f) + 8f;
            float fullW = inner.W - 24f, wh = (inner.YMax - 12f - wy - 20f) / 3f;
            DrawSlot(ui, new UiRect(x0, wy, fullW, wh), EquipSlot.Primary, "ARMA PRINCIPAL");
            DrawSlot(ui, new UiRect(x0, wy + wh + 10f, fullW, wh), EquipSlot.Secondary, "ARMA SECUNDARIA");
            float half = (fullW - 10f) * 0.5f;
            DrawSlot(ui, new UiRect(x0, wy + 2 * (wh + 10f), half, wh), EquipSlot.Holster, "PISTOLERA");
            DrawSlot(ui, new UiRect(x0 + half + 10f, wy + 2 * (wh + 10f), half, wh), EquipSlot.Sheath, "FUNDA");
        }

        private static readonly (string part, int hp)[] HealthParts =
        {
            ("Cabeza", 35), ("Tórax", 85), ("Estómago", 70), ("Brazo izq.", 60), ("Brazo der.", 60), ("Pierna izq.", 65), ("Pierna der.", 65),
        };

        private void DrawHealth(Ui ui, UiRect r)
        {
            ui.Fill(r, UiColor.Hex(0x0E0F10, 0.9f));
            ui.Frame(r, UiTheme.Border);
            ui.Label(new UiRect(r.X + 8f, r.Y + 6f, r.W - 16f, 18f), "SALUD", UiTheme.SizeSmall, UiFont.Bold, UiAlign.Left, UiTheme.TextDim);
            float y = r.Y + 30f;
            foreach (var (part, hp) in HealthParts)
            {
                ui.Label(new UiRect(r.X + 8f, y, r.W - 16f, 16f), part, 12, UiFont.Regular, UiAlign.Left, UiTheme.TextDim);
                ui.Label(new UiRect(r.X + 8f, y, r.W - 16f, 16f), hp + "/" + hp, 12, UiFont.Regular, UiAlign.Right, UiTheme.Text);
                ui.Fill(new UiRect(r.X + 8f, y + 18f, r.W - 16f, 3f), UiColor.Hex(0x4E6E3A));
                y += 30f;
            }
            ui.Fill(new UiRect(r.X + 8f, y + 4f, r.W - 16f, 1f), UiTheme.Border);
            ui.Label(new UiRect(r.X + 8f, y + 10f, r.W - 16f, 18f), "440/440", UiTheme.SizeBody, UiFont.Bold, UiAlign.Center, UiTheme.Text);
            ui.Label(new UiRect(r.X + 8f, y + 34f, r.W - 16f, 16f), "PESO", 12, UiFont.Bold, UiAlign.Left, UiTheme.TextDim);
            ui.Label(new UiRect(r.X + 8f, y + 52f, r.W - 16f, 16f), Format.Kg(P.CarriedWeight), 12, UiFont.Regular, UiAlign.Left, UiTheme.Text);
        }

        // ================================================================== containers panel

        private void DrawContainers(Ui ui, UiRect r)
        {
            UiRect inner = ui.Panel(r, "CONTENEDORES", "Peso: " + Format.Kg(P.CarriedWeight));
            float x = inner.X + 12f, y = inner.Y + 8f, w = inner.W - 24f;

            y = ContainerSection(ui, "CHALECO TÁCTICO", EquipSlot.Rig, x, y, w);
            // Pockets: four 1x1 grids.
            ui.SectionTitle(new UiRect(x, y, w, 22f), "BOLSILLOS");
            y += 28f;
            for (int i = 0; i < P.Pockets.Length; i++) DrawGrid(ui, P.Pockets[i], x + i * (C + 8f), y);
            y += C + 14f;
            y = ContainerSection(ui, "MOCHILA", EquipSlot.Backpack, x, y, w);
            ContainerSection(ui, "CONTENEDOR SEGURO", EquipSlot.SecureContainer, x, y, w);
        }

        private float ContainerSection(Ui ui, string title, EquipSlot slot, float x, float y, float w)
        {
            Item item = P.Equipped(slot);
            ui.SectionTitle(new UiRect(x, y, w, 22f), title, item != null ? item.Def.Name : null);
            y += 28f;
            DrawSlot(ui, new UiRect(x, y, SlotSize, SlotSize), slot, null);
            float h = SlotSize;
            if (item != null) h = Math.Max(h, DrawGridsOf(ui, item, x + SlotSize + 12f, y, w - SlotSize - 12f));
            return y + h + 14f;
        }

        /// <summary>Draws a container's grids left to right, wrapping; returns the height used.</summary>
        private float DrawGridsOf(Ui ui, Item item, float x, float y, float maxW)
        {
            float cx = x, cy = y, rowH = 0f;
            foreach (GridContainer g in item.Grids)
            {
                float gw = g.Width * C, gh = g.Height * C;
                if (cx > x && cx + gw > x + maxW) { cx = x; cy += rowH + 8f; rowH = 0f; }
                DrawGrid(ui, g, cx, cy);
                cx += gw + 8f;
                rowH = Math.Max(rowH, gh);
            }
            return cy + rowH - y;
        }

        private static float GridsHeight(Item item, float maxW)
        {
            float cx = 0f, cy = 0f, rowH = 0f;
            foreach (GridContainer g in item.Grids)
            {
                float gw = g.Width * C, gh = g.Height * C;
                if (cx > 0f && cx + gw > maxW) { cx = 0f; cy += rowH + 8f; rowH = 0f; }
                cx += gw + 8f;
                rowH = Math.Max(rowH, gh);
            }
            return cy + rowH;
        }

        private static float GridsWidth(Item item, float maxW)
        {
            float cx = 0f, best = 0f;
            foreach (GridContainer g in item.Grids)
            {
                float gw = g.Width * C;
                if (cx > 0f && cx + gw > maxW) cx = 0f;
                cx += gw + 8f;
                best = Math.Max(best, cx - 8f);
            }
            return best;
        }

        // ================================================================== stash panel

        private void DrawStashLocked(Ui ui, UiRect r)
        {
            UiRect inner = ui.Panel(r, "ALIJO", "no disponible");
            ui.Label(inner, "ALIJO NO DISPONIBLE EN INCURSIÓN", UiTheme.SizeLabel, UiFont.Bold, UiAlign.Center, UiTheme.TextDim);
        }

        private void DrawStash(Ui ui, UiRect r)
        {
            int used = P.Stash.UsedRows();
            UiRect inner = ui.Panel(r, "ALIJO", Profile.StashWidth + " × " + Profile.StashHeight);
            var bar = new UiRect(inner.X + 10f, inner.Y + 8f, inner.W - 20f, 30f);
            if (ui.Button(new UiRect(bar.X, bar.Y, 120f, bar.H), "ORDENAR", ButtonStyle.Normal, null, UiTheme.SizeBody))
            {
                P.Sort(P.Stash);
                _req.SaveProfile = true;
            }
            ui.Label(new UiRect(bar.X + 132f, bar.Y, bar.W - 132f, bar.H),
                "Ocupado: " + used + " filas  ·  ₽ " + Format.Thousands(P.Money), UiTheme.SizeSmall, UiFont.Regular, UiAlign.Right, UiTheme.TextDim);

            float gridW = Profile.StashWidth * C;
            var view = new UiRect(inner.X + (inner.W - gridW - 10f) * 0.5f, bar.YMax + 10f, gridW + 10f, inner.YMax - bar.YMax - 20f);
            view.H = (float)Math.Floor(view.H / C) * C;
            float scroll = ui.BeginScroll(view, ref _stashScroll, Profile.StashHeight * C);
            DrawGrid(ui, P.Stash, view.X, view.Y - scroll, view);
            ui.EndScroll();
        }

        // ================================================================== grids, slots, items

        private static UiColor CategoryColor(ItemCategory c)
        {
            switch (c)
            {
                case ItemCategory.Weapon: case ItemCategory.Melee: return UiColor.Hex(0x2C2F31);
                case ItemCategory.WeaponPart: case ItemCategory.Magazine: return UiColor.Hex(0x232C36);
                case ItemCategory.Ammo: return UiColor.Hex(0x39331F);
                case ItemCategory.Medical: return UiColor.Hex(0x263A28);
                case ItemCategory.Food: return UiColor.Hex(0x3C2E22);
                case ItemCategory.Key: return UiColor.Hex(0x3C2424);
                case ItemCategory.Money: return UiColor.Hex(0x283826);
                case ItemCategory.Barter: return UiColor.Hex(0x2C3435);
                case ItemCategory.SecureContainer: return UiColor.Hex(0x1C1D1E);
                case ItemCategory.Rig: case ItemCategory.Backpack: case ItemCategory.Container: return UiColor.Hex(0x33302A);
                default: return UiColor.Hex(0x2D2C35);
            }
        }

        private void DrawGrid(Ui ui, GridContainer g, float x, float y, UiRect? clip = null)
        {
            var rect = new UiRect(x, y, g.Width * C, g.Height * C);
            UiRect visible = clip ?? rect;
            _zones.Add(new Zone { Kind = ZoneKind.Grid, Grid = g, Rect = rect, Clip = visible });

            ui.Fill(rect, UiTheme.CellBack);
            for (int i = 0; i <= g.Width; i++) ui.Fill(new UiRect(x + i * C - (i == g.Width ? 1f : 0f), y, 1f, rect.H), UiTheme.CellLine);
            for (int j = 0; j <= g.Height; j++)
            {
                float ly = y + j * C - (j == g.Height ? 1f : 0f);
                if (ly < visible.Y - 1f || ly > visible.YMax + 1f) continue;
                ui.Fill(new UiRect(x, ly, rect.W, 1f), UiTheme.CellLine);
            }

            foreach (Placement p in g.Placements)
            {
                var r = new UiRect(x + p.X * C, y + p.Y * C, p.W * C, p.H * C);
                if (!r.Overlaps(visible)) continue;
                DrawItem(ui, p.Item, r, p.Rotated, p.Item == _drag);
                _itemRects[p.Item] = r;
                if (visible.Contains(ui.Input.Mouse)) Interact(ui, p.Item, r);
            }
        }

        private void DrawSlot(Ui ui, UiRect r, EquipSlot slot, string label)
        {
            _zones.Add(new Zone { Kind = ZoneKind.Slot, Slot = slot, Rect = r, Clip = r });
            _slotRects[slot] = r;
            Item item = P.Equipped(slot);
            if (item != null) _itemRects[item] = r;
            ui.Fill(r, UiColor.Hex(0x0E0F10, 0.92f));
            if (item == null || item == _drag) ui.Stripes(r, UiTheme.Stripe);
            ui.Frame(r, UiTheme.Border);
            if (item != null) DrawItem(ui, item, r.Shrink(1f), false, item == _drag, fitSlot: true);
            if (label != null)
                ui.Label(new UiRect(r.X + 6f, r.Y + 3f, r.W - 12f, 16f), label, 12, UiFont.Bold, UiAlign.Left,
                    item != null ? UiTheme.TextDim : UiColor.Hex(0x66665F));
            if (item != null) Interact(ui, item, r);
        }

        private void DrawItem(Ui ui, Item item, UiRect r, bool rotated, bool dim, bool fitSlot = false)
        {
            UiColor back = CategoryColor(item.Def.Category);
            ui.Fill(r.Shrink(1f), dim ? back.WithAlpha(0.35f) : back.WithAlpha(0.92f));
            bool hover = !dim && _drag == null && ui.Hover(r);
            ui.Frame(r.Shrink(1f), hover ? UiTheme.Highlight : UiColor.Hex(0x4A4E50, dim ? 0.4f : 1f));

            UiImage icon = _ctx.Icons.Get(item, rotated);
            if (icon != null)
            {
                UiRect box = r.Shrink(fitSlot ? 10f : 4f);
                if (fitSlot) box = box.CutTop(12f);
                float s = Math.Min(box.W / icon.Width, box.H / icon.Height);
                var ir = UiRect.Around(box.Center, icon.Width * s, icon.Height * s);
                ui.Draw.Image(ir, icon, new UiColor(1f, 1f, 1f, dim ? 0.3f : 1f));
            }
            UiColor ink = dim ? UiTheme.TextDim.WithAlpha(0.4f) : UiTheme.TextBright;
            if (!fitSlot) ui.Label(new UiRect(r.X + 4f, r.Y + 2f, r.W - 8f, 15f), item.Def.ShortName, 12, UiFont.Regular, UiAlign.Right, ink);
            else ui.Label(new UiRect(r.X + 4f, r.YMax - 18f, r.W - 8f, 15f), item.Def.ShortName, 12, UiFont.Regular, UiAlign.Right, ink);
            string info = InfoText(item);
            if (info != null && !fitSlot)
                ui.Label(new UiRect(r.X + 4f, r.YMax - 17f, r.W - 8f, 15f), info, 12, UiFont.Bold, UiAlign.Right, ink);
            if (hover) ui.Tooltip(item.Def.Name);
        }

        private string InfoText(Item item)
        {
            if (item.Def.Stackable) return item.Def.Category == ItemCategory.Money ? Format.Thousands(item.Count) : item.Count.ToString();
            if (item.Def.Category == ItemCategory.Magazine && item.Def.Capacity > 0) return CapacityText(item.Def.Capacity);
            return null;
        }

        private string CapacityText(int capacity)
        {
            if (!_capacityText.TryGetValue(capacity, out string text)) _capacityText[capacity] = text = capacity + "/" + capacity;
            return text;
        }

        // ================================================================== interaction

        private void Interact(Ui ui, Item item, UiRect r)
        {
            if (_drag != null || !ui.Hover(r)) return;
            if (ui.Input.Pressed(1))
            {
                ui.Input.Consume(1);
                _menuItem = item;
                _menuPos = ui.Input.Mouse;
                return;
            }
            if (!ui.Input.Pressed(0)) return;
            ui.Input.Consume(0);
            _menuItem = null;
            if (ui.Input.Ctrl)
            {
                if (P.QuickMove(item)) _req.SaveProfile = true;
                else Say(ui, "No hay sitio");
                return;
            }
            if (ui.Input.DoubleClick)
            {
                if (item.Def.IsContainer) Open(ui, item, false);
                else if (item.Def.Category == ItemCategory.Weapon) OpenArmorer?.Invoke(item);
                else Open(ui, item, true);
                return;
            }
            _pressed = item;
            _detachWeapon = null;
            _pressPos = ui.Input.Mouse;
            bool inGrid = item.Location != null && !item.Location.IsSlot;
            _pressGrab = inGrid ? ui.Input.Mouse - new UiVec(r.X, r.Y) : new UiVec(C * 0.5f, C * 0.5f);
        }

        private void HandlePressAndDrag(Ui ui)
        {
            if (_pressed != null && _drag == null)
            {
                if (!ui.Input.Down(0)) { _pressed = null; _detachWeapon = null; }
                else if ((ui.Input.Mouse - _pressPos).Length > 5f)
                {
                    _drag = _pressed;
                    _dragRot = _drag.Location != null && !_drag.Location.IsSlot && _drag.Location.Rotated;
                    _grab = _pressGrab;
                    _pressed = null;
                }
            }
            if (_drag != null && ui.Input.KeyPressed(UiKey.R))
            {
                _dragRot = !_dragRot;
                _grab = new UiVec(C * 0.5f, C * 0.5f);
            }
        }

        /// <summary>Drop target under the mouse for the dragged item (overlay zones win).</summary>
        private bool FindTarget(UiVec mouse, out MoveTarget target, out Zone zone)
        {
            for (int i = _zones.Count - 1; i >= 0; i--)
            {
                Zone z = _zones[i];
                if (!z.Rect.Contains(mouse) || !z.Clip.Contains(mouse)) continue;
                zone = z;
                if (z.Kind == ZoneKind.Card)
                {
                    target = default;
                    return true;
                }
                if (z.Kind == ZoneKind.Slot)
                {
                    target = MoveTarget.ToSlot(z.Slot);
                    return true;
                }
                int mx = (int)Math.Floor((mouse.X - z.Rect.X) / C), my = (int)Math.Floor((mouse.Y - z.Rect.Y) / C);
                Item under = z.Grid.ItemAt(mx, my);
                if (under != null && under != _drag && _drag.Def.Stackable && under.Def == _drag.Def)
                {
                    target = MoveTarget.ToCell(z.Grid, mx, my, _dragRot);
                    return true;
                }
                UiVec tl = mouse - _grab;
                int cx = (int)Math.Round((tl.X - z.Rect.X) / C), cy = (int)Math.Round((tl.Y - z.Rect.Y) / C);
                target = MoveTarget.ToCell(z.Grid, cx, cy, _dragRot);
                return true;
            }
            target = default;
            zone = default;
            return false;
        }

        private void DrawDragAndDrop(Ui ui)
        {
            if (_drag == null) return;
            UiVec mouse = ui.Input.Mouse;
            bool overWindow = false;
            foreach (UiRect w in _windowRects) if (w.Contains(mouse)) overWindow = true;
            bool has = FindTarget(mouse, out MoveTarget target, out Zone zone);
            // Inside a window but not on one of its grids: never drop "through" the window.
            if (has && overWindow)
            {
                bool zoneInWindow = false;
                foreach (UiRect w in _windowRects) if (w.Contains(new UiVec(zone.Rect.X + 1f, zone.Rect.Y + 1f))) zoneInWindow = true;
                has = zoneInWindow;
            }
            bool detaching = _detachWeapon != null;
            // Mounting: a part dragged over a weapon (in a grid or an equipment slot) or over one of that weapon's slot cards.
            Item mountWeapon = null;
            bool mountOk = false;
            string mountReason = null;
            if (has && !detaching)
            {
                mountWeapon = zone.Kind == ZoneKind.Card ? zone.Weapon : WeaponUnder(zone, mouse);
                if (mountWeapon != null)
                    mountOk = CheckMount(mountWeapon, zone.Kind == ZoneKind.Card ? zone.CardSlot : (AttachmentSlot?)null, out mountReason);
            }

            MoveResult check = has && zone.Kind != ZoneKind.Card ? P.Check(_drag, target) : MoveResult.NotAllowed;
            bool ok = check == MoveResult.Ok || check == MoveResult.Merged || check == MoveResult.Swapped;
            if (detaching) ok = ok && zone.Kind == ZoneKind.Grid;

            if (mountWeapon != null)
            {
                UiRect hit = zone.Kind == ZoneKind.Card || !_itemRects.TryGetValue(mountWeapon, out UiRect wr) ? zone.Rect : wr;
                ui.Fill(hit, (mountOk ? UiTheme.Good : UiTheme.Bad).WithAlpha(0.35f));
                ui.Frame(hit, mountOk ? UiTheme.Good : UiTheme.Bad, 2f);
                if (!mountOk && mountReason != null) ui.Tooltip(mountReason);
            }
            else if (has && zone.Kind != ZoneKind.Card)
            {
                UiColor tint = ok ? UiTheme.Good.WithAlpha(0.35f) : UiTheme.Bad.WithAlpha(0.35f);
                if (zone.Kind == ZoneKind.Slot)
                {
                    ui.Fill(zone.Rect, tint);
                    ui.Frame(zone.Rect, ok ? UiTheme.Good : UiTheme.Bad, 2f);
                }
                else
                {
                    int w = GridContainer.FootprintW(_drag.Def, _dragRot), h = GridContainer.FootprintH(_drag.Def, _dragRot);
                    if (check == MoveResult.Merged) { w = 1; h = 1; }
                    var fr = new UiRect(zone.Rect.X + target.X * C, zone.Rect.Y + target.Y * C, w * C, h * C);
                    ui.Draw.PushClip(zone.Clip);
                    ui.Fill(fr, tint);
                    ui.Draw.PopClip();
                }
            }
            else if (has && detaching)
            {
                ui.Fill(zone.Rect, UiTheme.Bad.WithAlpha(0.35f));
                ui.Frame(zone.Rect, UiTheme.Bad, 2f);
            }

            // The item under the cursor.
            float iw = GridContainer.FootprintW(_drag.Def, _dragRot) * C, ih = GridContainer.FootprintH(_drag.Def, _dragRot) * C;
            var ghost = new UiRect(mouse.X - _grab.X, mouse.Y - _grab.Y, iw, ih);
            DrawGhost(ui, ghost);

            if (!ui.Input.Released(0)) return;
            Item dragged = _drag;
            Item detachFrom = _detachWeapon;
            AttachmentSlot detachSlot = _detachSlot;
            EndDrag();
            if (!has) return;

            string reason;
            if (detachFrom != null)
            {
                if (zone.Kind != ZoneKind.Grid) { Say(ui, "Suéltala en una cuadrícula del inventario"); return; }
                if (!ok) { Say(ui, "No cabe"); return; }
                if (WeaponParts.TryDetach(P, detachFrom, detachSlot, _ctx.FamilyOf(detachFrom), target, out reason))
                {
                    _req.SaveProfile = true;
                    _req.BuildChanged = detachFrom;
                }
                else Say(ui, reason);
                return;
            }
            if (mountWeapon != null)
            {
                if (!mountOk) { Say(ui, mountReason); return; }
                if (WeaponParts.TryMountItem(P, mountWeapon, dragged, _ctx.FamilyOf(mountWeapon), out reason))
                {
                    _req.SaveProfile = true;
                    _req.BuildChanged = mountWeapon;
                }
                else Say(ui, reason);
                return;
            }
            if (zone.Kind == ZoneKind.Card) { Say(ui, "Eso no va en una ranura del arma"); return; }
            MoveResult result = P.Move(dragged, target);
            if (result == MoveResult.Ok || result == MoveResult.Merged || result == MoveResult.Swapped)
            {
                _req.SaveProfile = true;
                return;
            }
            Say(ui, result == MoveResult.WrongSlot ? "No va en esa ranura"
                : result == MoveResult.IntoItself ? "No puedes meter un contenedor dentro de sí mismo"
                : result == MoveResult.NotAllowed ? "No se puede guardar ahí" : "No cabe");
        }

        private void EndDrag()
        {
            _drag = null;
            _detachWeapon = null;
            _mountWeapon = null;
            _mountPart = null;
        }

        /// <summary>The weapon under the mouse when the dragged item is a weapon part (grid cell or equipment slot), else null.</summary>
        private Item WeaponUnder(Zone zone, UiVec mouse)
        {
            if (_drag.Def.AttachmentId == null) return null;
            Item under = null;
            if (zone.Kind == ZoneKind.Slot) under = P.Equipped(zone.Slot);
            else if (zone.Kind == ZoneKind.Grid)
                under = zone.Grid.ItemAt((int)Math.Floor((mouse.X - zone.Rect.X) / C), (int)Math.Floor((mouse.Y - zone.Rect.Y) / C));
            return under != null && under.Def.Category == ItemCategory.Weapon && under.Def.WeaponId != null ? under : null;
        }

        /// <summary>Pure check (cached per weapon / part / build) that the dragged part can be mounted, optionally onto a given card slot.</summary>
        private bool CheckMount(Item weapon, AttachmentSlot? cardSlot, out string reason)
        {
            if (weapon != _mountWeapon || _drag != _mountPart || weapon.Build != _mountBuild)
            {
                _mountWeapon = weapon;
                _mountPart = _drag;
                _mountBuild = weapon.Build;
                WeaponFamily family = _ctx.FamilyOf(weapon);
                _mountOk = WeaponParts.CanMount(P, weapon, _drag, family, out _mountReason);
                if (family.Catalog.TryGet(_drag.Def.AttachmentId, out AttachmentRules rules)) _mountSlot = rules.Slot;
            }
            reason = _mountReason;
            if (_mountOk && cardSlot.HasValue && cardSlot.Value != _mountSlot)
            {
                reason = "No va en esa ranura.";
                return false;
            }
            return _mountOk;
        }

        private void DrawGhost(Ui ui, UiRect r)
        {
            UiColor back = CategoryColor(_drag.Def.Category);
            ui.Fill(r, back.WithAlpha(0.7f));
            ui.Frame(r, UiTheme.Highlight);
            UiImage icon = _ctx.Icons.Get(_drag, _dragRot);
            if (icon == null) return;
            UiRect box = r.Shrink(4f);
            float s = Math.Min(box.W / icon.Width, box.H / icon.Height);
            ui.Draw.Image(UiRect.Around(box.Center, icon.Width * s, icon.Height * s), icon, new UiColor(1, 1, 1, 0.85f));
        }

        // ================================================================== context menu

        private void DrawMenu(Ui ui)
        {
            Item item = _menuItem;
            if (item == null || item.Location == null) { _menuItem = null; return; }
            var entries = new List<(string text, Action act)>();
            entries.Add(("INSPECCIONAR", () => Open(ui, item, true)));
            if (item.Def.IsContainer) entries.Add(("ABRIR", () => Open(ui, item, false)));
            if (item.Def.Category == ItemCategory.Weapon) entries.Add(("MODIFICAR", () => OpenArmorer?.Invoke(item)));
            bool inStash = P.InStash(item);
            string quick = inStash ? (CanEquipFromStash(item) ? "EQUIPAR" : "LLEVAR ENCIMA") : "AL ALIJO";
            entries.Add((quick, () =>
            {
                if (P.QuickMove(item)) _req.SaveProfile = true;
                else Say(ui, "No hay sitio");
            }));
            if (item.Def.Stackable && item.Count > 1)
                entries.Add(("DIVIDIR", () =>
                {
                    if (P.Split(item, item.Count / 2) != null) _req.SaveProfile = true;
                    else Say(ui, "No hay sitio para dividir");
                }));
            if (item.Def.IsContainer && item.Grids.Length == 1)
                entries.Add(("ORDENAR", () => { P.Sort(item.Grids[0]); _req.SaveProfile = true; }));
            if (item.Def.Category != ItemCategory.SecureContainer)
                entries.Add(("TIRAR", () =>
                {
                    P.Discard(item);
                    _windows.RemoveAll(w => w.Item == item);
                    _req.SaveProfile = true;
                }));

            float w = 200f, h = 30f;
            var r = new UiRect(_menuPos.X + 2f, _menuPos.Y + 2f, w, entries.Count * h + 30f);
            if (r.XMax > ui.Width - 4f) r.X = ui.Width - 4f - r.W;
            if (r.YMax > ui.Height - 4f) r.Y = ui.Height - 4f - r.H;
            ui.BlockArea(r);
            ui.Fill(r, UiColor.Hex(0x0C0D0E, 0.98f));
            ui.Frame(r, UiTheme.BorderLight);
            ui.Label(new UiRect(r.X + 10f, r.Y + 4f, r.W - 20f, 22f), item.Def.ShortName.ToUpperInvariant(), UiTheme.SizeSmall, UiFont.Bold, UiAlign.Left, UiTheme.TextDim);
            for (int i = 0; i < entries.Count; i++)
            {
                var er = new UiRect(r.X + 1f, r.Y + 28f + i * h, r.W - 2f, h);
                if (ui.Button(er, entries[i].text, ButtonStyle.Flat, null, UiTheme.SizeBody))
                {
                    _menuItem = null;
                    entries[i].act();
                    return;
                }
            }
            // Any click outside closes it.
            if ((ui.Input.Pressed(0) || ui.Input.Pressed(1)) && !r.Contains(ui.Input.Mouse)) _menuItem = null;
        }

        private bool CanEquipFromStash(Item item)
        {
            foreach (EquipSlot s in (EquipSlot[])Enum.GetValues(typeof(EquipSlot)))
                if (P.Equipped(s) == null && item.Def.Fits(s)) return true;
            return false;
        }

        // ================================================================== windows (containers / inspect)

        /// <summary>Opens a container or inspect window at a given position (also used by scripted previews).</summary>
        public void ShowWindow(Item item, bool inspect, UiVec pos)
        {
            _windows.RemoveAll(w => w.Item == item && w.Inspect == inspect);
            _windows.Add(new Window { Item = item, Inspect = inspect, Pos = pos });
        }

        private void Open(Ui ui, Item item, bool inspect)
        {
            foreach (Window w in _windows)
                if (w.Item == item && w.Inspect == inspect) { _windows.Remove(w); _windows.Add(w); return; }
            var pos = new UiVec(Math.Min(ui.Input.Mouse.X + 20f, ui.Width - 520f), Math.Min(ui.Input.Mouse.Y - 40f, ui.Height - 560f));
            _windows.Add(new Window { Item = item, Inspect = inspect, Pos = new UiVec(Math.Max(20f, pos.X), Math.Max(60f, pos.Y)) });
        }

        private void DrawWindow(Ui ui, Window win)
        {
            Item item = win.Item;
            float w, h;
            if (win.Inspect)
            {
                w = 460f;
                h = 330f + item.Def.Properties.Length * 22f + (item.Def.Category == ItemCategory.Weapon ? 6 * 22f + 60f + CardsHeight(_ctx.FamilyOf(item)) : 0f);
            }
            else
            {
                w = Math.Max(300f, GridsWidth(item, 600f) + 28f);
                h = GridsHeight(item, 600f) + 58f;
            }
            win.Rect = new UiRect(win.Pos.X, win.Pos.Y, w, h);
            ui.Fill(win.Rect, UiColor.Hex(0x0F1011, 0.98f));
            ui.Frame(win.Rect, UiTheme.BorderLight);
            var header = new UiRect(win.Rect.X + 1f, win.Rect.Y + 1f, w - 2f, 30f);
            ui.Fill(header, UiTheme.Header);
            ui.Label(header.Shrink(12f, 0f), item.Def.Name, UiTheme.SizeBody, UiFont.Bold, UiAlign.Left, UiTheme.TextBright);
            var close = new UiRect(header.XMax - 30f, header.Y, 30f, 30f);
            if (ui.Button(close, "×", ButtonStyle.Flat, null, UiTheme.SizeTitle)) { _windows.Remove(win); return; }

            // Move by dragging the header.
            if (_moving == null && _drag == null && ui.Hover(header) && ui.Input.Pressed(0))
            {
                ui.Input.Consume(0);
                _moving = win;
                _moveOffset = ui.Input.Mouse - win.Pos;
            }
            if (_moving == win)
            {
                if (ui.Input.Down(0)) win.Pos = ui.Input.Mouse - _moveOffset;
                else _moving = null;
            }
            // Clicking a window brings it to the front.
            if (ui.Hover(win.Rect) && ui.Input.Pressed(0) && _windows[_windows.Count - 1] != win)
            {
                _windows.Remove(win);
                _windows.Add(win);
            }

            var body = new UiRect(win.Rect.X + 14f, header.YMax + 14f, w - 28f, h - 58f);
            if (win.Inspect) DrawInspect(ui, item, body);
            else DrawGridsOf(ui, item, body.X, body.Y, 600f);
        }

        private void DrawInspect(Ui ui, Item item, UiRect r)
        {
            var iconBox = new UiRect(r.X, r.Y, 180f, 150f);
            ui.Fill(iconBox, CategoryColor(item.Def.Category).WithAlpha(0.9f));
            ui.Frame(iconBox, UiTheme.Border);
            UiImage icon = _ctx.Icons.Get(item, false);
            if (icon != null)
            {
                UiRect box = iconBox.Shrink(10f);
                float s = Math.Min(box.W / icon.Width, box.H / icon.Height);
                ui.Draw.Image(UiRect.Around(box.Center, icon.Width * s, icon.Height * s), icon, new UiColor(1, 1, 1, 1));
            }
            float x = iconBox.XMax + 16f, tw = r.XMax - x;
            float y = r.Y;
            void Row(string k, string v)
            {
                ui.Label(new UiRect(x, y, tw, 20f), k, UiTheme.SizeSmall, UiFont.Regular, UiAlign.Left, UiTheme.TextDim);
                ui.Label(new UiRect(x, y, tw, 20f), v, UiTheme.SizeSmall, UiFont.Bold, UiAlign.Right, UiTheme.Text);
                y += 24f;
            }
            Row("Tipo", CategoryName(item.Def.Category));
            Row("Tamaño", item.Def.Width + "×" + item.Def.Height);
            Row("Peso", Format.Kg(item.TotalWeight));
            Row("Precio", "₽ " + Format.Thousands(item.Def.Price * (item.Def.Stackable ? item.Count : 1)));
            if (item.Def.Stackable) Row("Cantidad", item.Count + "/" + item.Def.MaxStack);

            y = Math.Max(y, iconBox.YMax) + 12f;
            ui.Fill(new UiRect(r.X, y, r.W, 1f), UiTheme.Border);
            y += 10f;
            ui.Paragraph(new UiRect(r.X, y, r.W, 60f), item.Def.Description);
            y += 54f;
            foreach (string prop in item.Def.Properties)
            {
                ui.Label(new UiRect(r.X, y, r.W, 20f), prop, UiTheme.SizeBody, UiFont.Regular, UiAlign.Left, UiTheme.Text);
                y += 22f;
            }
            if (item.Def.Category != ItemCategory.Weapon) return;

            WeaponFamily family = _ctx.FamilyOf(item);
            WeaponBuild build = WeaponParts.BuildOf(item, family.FactoryBuild);
            foreach (StatRow row in ArmorerModel.Stats(family, build))
            {
                ui.Label(new UiRect(r.X, y, r.W, 20f), ArmorerView.StatName(row.Kind), UiTheme.SizeBody, UiFont.Regular, UiAlign.Left, UiTheme.TextDim);
                ui.Label(new UiRect(r.X, y, r.W, 20f), ArmorerView.FormatStat(row.Kind, row.Current), UiTheme.SizeBody, UiFont.Bold, UiAlign.Right,
                    row.Verdict == StatVerdict.Better ? UiTheme.Good : row.Verdict == StatVerdict.Worse ? UiTheme.Bad : UiTheme.Text);
                y += 22f;
            }
            DrawSlotCards(ui, item, family, build, r.X, y + 8f, r.W);
            if (ui.Button(new UiRect(r.X, r.YMax - 40f, r.W, 40f), "MODIFICAR", ButtonStyle.Normal, null, UiTheme.SizeBody))
                OpenArmorer?.Invoke(item);
        }

        // ------------------------------------------------------------------ weapon slot cards (Tarkov "modding" cards)

        private const int CardCols = 3;
        private const float CardH = 76f, CardGap = 8f, CardsTitleH = 28f;

        private static float CardsHeight(WeaponFamily family)
        {
            int rows = (family.Slots.Length + CardCols - 1) / CardCols;
            return CardsTitleH + rows * (CardH + CardGap) + 8f;
        }

        /// <summary>
        /// One card per slot of the weapon showing the mounted part. Drop a compatible part on a card to mount it; drag the
        /// part of an optional slot (muzzle device) out of its card to a grid to take it off.
        /// </summary>
        private void DrawSlotCards(Ui ui, Item weapon, WeaponFamily family, WeaponBuild build, float x, float y, float w)
        {
            ui.SectionTitle(new UiRect(x, y, w, 22f), "PIEZAS MONTADAS");
            y += CardsTitleH;
            float cw = (w - (CardCols - 1) * CardGap) / CardCols;
            for (int i = 0; i < family.Slots.Length; i++)
            {
                var rect = new UiRect(x + (i % CardCols) * (cw + CardGap), y + (i / CardCols) * (CardH + CardGap), cw, CardH);
                DrawCard(ui, weapon, family, family.Slots[i], build.Get(family.Slots[i]), rect);
            }
        }

        private void DrawCard(Ui ui, Item weapon, WeaponFamily family, AttachmentSlot slot, string partId, UiRect r)
        {
            ItemDef def = partId == null ? null : _ctx.Db.ForAttachment(partId);
            _zones.Add(new Zone { Kind = ZoneKind.Card, Weapon = weapon, CardSlot = slot, Rect = r, Clip = r });
            ui.Fill(r, UiColor.Hex(0x0E0F10, 0.92f));
            if (def == null) ui.Stripes(r, UiTheme.Stripe);
            bool hover = def != null && _drag == null && ui.Hover(r);
            ui.Frame(r, hover ? UiTheme.Highlight : UiTheme.Border);
            ui.Label(new UiRect(r.X + 6f, r.Y + 3f, r.W - 12f, 16f), ArmorerView.SlotName(slot, family), 12, UiFont.Bold, UiAlign.Left,
                def != null ? UiTheme.TextDim : UiColor.Hex(0x66665F));
            if (def == null) return;

            Item proxy = Proxy(def);
            UiImage icon = _ctx.Icons.Get(proxy, false);
            if (icon != null)
            {
                var box = new UiRect(r.X + 6f, r.Y + 20f, r.W - 12f, r.H - 38f);
                float s = Math.Min(box.W / icon.Width, box.H / icon.Height);
                ui.Draw.Image(UiRect.Around(box.Center, icon.Width * s, icon.Height * s), icon, new UiColor(1f, 1f, 1f, _drag == proxy ? 0.3f : 1f));
            }
            ui.Label(new UiRect(r.X + 6f, r.YMax - 18f, r.W - 12f, 15f), def.ShortName, 12, UiFont.Regular, UiAlign.Right, UiTheme.TextBright);
            if (hover) ui.Tooltip(ArmorerModel.IsOptional(slot) ? def.Name : def.Name + " (imprescindible)");
            if (hover && ArmorerModel.IsOptional(slot) && ui.Input.Pressed(0))
            {
                ui.Input.Consume(0);
                _menuItem = null;
                _pressed = proxy;
                _detachWeapon = weapon;
                _detachSlot = slot;
                _pressPos = ui.Input.Mouse;
                _pressGrab = new UiVec(C * 0.5f, C * 0.5f);
            }
        }

        /// <summary>Stand-in item of a part type (for icons and for dragging a mounted part); never added to the profile.</summary>
        private Item Proxy(ItemDef def)
        {
            if (!_proxies.TryGetValue(def, out Item item)) _proxies[def] = item = new Item(-1, def);
            return item;
        }

        public static string CategoryName(ItemCategory c)
        {
            switch (c)
            {
                case ItemCategory.Weapon: return "Arma";
                case ItemCategory.WeaponPart: return "Pieza de arma";
                case ItemCategory.Magazine: return "Cargador";
                case ItemCategory.Ammo: return "Munición";
                case ItemCategory.Medical: return "Médico";
                case ItemCategory.Food: return "Provisiones";
                case ItemCategory.Headwear: return "Casco / gorra";
                case ItemCategory.Earpiece: return "Auriculares";
                case ItemCategory.FaceCover: return "Cara";
                case ItemCategory.Eyewear: return "Gafas";
                case ItemCategory.Armband: return "Brazalete";
                case ItemCategory.BodyArmor: return "Chaleco balístico";
                case ItemCategory.Rig: return "Chaleco táctico";
                case ItemCategory.Backpack: return "Mochila";
                case ItemCategory.SecureContainer: return "Contenedor seguro";
                case ItemCategory.Melee: return "Cuerpo a cuerpo";
                case ItemCategory.Barter: return "Intercambio";
                case ItemCategory.Key: return "Llave";
                case ItemCategory.Money: return "Dinero";
                default: return "Contenedor";
            }
        }
    }
}
