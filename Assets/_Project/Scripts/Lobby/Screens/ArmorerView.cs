using System;
using System.Collections.Generic;
using Polykov.Inventory;
using Polykov.UI.Framework;
using Polykov.Weapons;

namespace Polykov.Lobby
{
    /// <summary>
    /// Weapon modding (Tarkov "Modding" screen): the 3D weapon in the middle (drawn by the host), slot callouts wired
    /// to the model, stat bars on the left and the compatible parts on the right. Parts come from the inventory: you
    /// can only mount what you own, and removed parts go back to the stash.
    /// </summary>
    public sealed class ArmorerView
    {
        private readonly LobbyContext _ctx;
        private readonly LobbyRequests _req;
        private AttachmentSlot _slot = AttachmentSlot.Muzzle;
        private bool _slotChosen;
        private string _message;
        private float _messageUntil;
        private bool _messageGood;

        /// <summary>The weapon being modified (defaults to the holster weapon).</summary>
        public Item Weapon;
        /// <summary>Free screen area around the weapon (orbit input, 3D framing).</summary>
        public UiRect OrbitArea { get; private set; }
        public Action Back;

        private static readonly AttachmentSlot[] Slots = { AttachmentSlot.Muzzle, AttachmentSlot.Barrel, AttachmentSlot.Grips, AttachmentSlot.Magazine };
        // Offset (px) from each slot's anchor to its callout box: muzzle above-right, barrel above, grips below-left, magazine below-right.
        private static readonly UiVec[] BoxOffsets = { new UiVec(130f, -190f), new UiVec(-170f, -200f), new UiVec(-270f, 230f), new UiVec(230f, 130f) };

        public ArmorerView(LobbyContext ctx, LobbyRequests req)
        {
            _ctx = ctx;
            _req = req;
        }

        public void SelectSlot(AttachmentSlot slot)
        {
            _slot = slot;
            _slotChosen = true;
        }

        private WeaponBuild Build => WeaponParts.BuildOf(Weapon, _ctx.FactoryBuild);

        public void Frame(Ui ui, UiRect area)
        {
            if (Weapon == null || Weapon.Location == null) { Back?.Invoke(); return; }
            const float leftW = 380f, rightW = 420f;
            OrbitArea = new UiRect(area.X + leftW + 20f, area.Y + 40f, area.W - leftW - rightW - 40f, area.H - 110f);

            ui.Label(new UiRect(area.X + leftW + 20f, area.Y, OrbitArea.W, 34f), Weapon.Def.Name.ToUpperInvariant(), UiTheme.SizeTitle,
                UiFont.Bold, UiAlign.Center, UiTheme.TextBright);
            HandleOrbit(ui);
            DrawStats(ui, new UiRect(area.X, area.Y, leftW, 420f));
            DrawCallouts(ui);
            DrawParts(ui, new UiRect(area.XMax - rightW, area.Y, rightW, area.H - 70f));

            float by = area.YMax - 52f;
            if (ui.Button(new UiRect(area.X, by, 230f, 48f), "< PERSONAJE", ButtonStyle.Normal, null, UiTheme.SizeBody)) Back?.Invoke();
            if (ui.Button(new UiRect(area.X + 242f, by, 230f, 48f), "DE FÁBRICA", ButtonStyle.Normal, null, UiTheme.SizeBody)) SetBuild(ui, _ctx.FactoryBuild);
            ui.Label(new UiRect(OrbitArea.X, by, OrbitArea.W, 48f), "Arrastra para girar  ·  Rueda para acercar  ·  Esc para volver",
                UiTheme.SizeSmall, UiFont.Regular, UiAlign.Center, UiTheme.TextDim);
            if (_message != null && ui.Time < _messageUntil)
                ui.Label(new UiRect(area.X + 490f, by, 420f, 48f), _message, UiTheme.SizeBody, UiFont.Bold, UiAlign.Left, _messageGood ? UiTheme.Good : UiTheme.Bad);

            if (ui.Input.KeyPressed(UiKey.Escape))
            {
                ui.Input.ConsumeKey(UiKey.Escape);
                Back?.Invoke();
            }
        }

        private void HandleOrbit(Ui ui)
        {
            if (!ui.Hover(OrbitArea)) return;
            if (ui.Input.Down(0)) _req.OrbitDrag = _req.OrbitDrag + ui.Input.MouseDelta;
            float wheel = ui.Input.TakeScroll();
            if (wheel != 0f) _req.OrbitZoom += wheel;
        }

        // ------------------------------------------------------------------ stats

        private static readonly Dictionary<StatKind, float> StatMax = new Dictionary<StatKind, float>
        {
            { StatKind.Ergonomics, 100f }, { StatKind.Recoil, 5f }, { StatKind.Weight, 3f }, { StatKind.Length, 60f },
            { StatKind.Loudness, 100f }, { StatKind.MuzzleFlash, 100f },
        };

        private void DrawStats(Ui ui, UiRect r)
        {
            UiRect inner = ui.Panel(r, "CARACTERÍSTICAS");
            float y = inner.Y + 14f;
            foreach (StatRow row in ArmorerModel.Stats(_ctx.BaseStats, _ctx.FactoryBuild, Build, _ctx.Catalog))
            {
                UiColor c = row.Verdict == StatVerdict.Better ? UiTheme.Good : row.Verdict == StatVerdict.Worse ? UiTheme.Bad : UiTheme.Text;
                var line = new UiRect(inner.X + 16f, y, inner.W - 32f, 20f);
                ui.Label(line, StatName(row.Kind).ToUpperInvariant(), UiTheme.SizeSmall, UiFont.Bold, UiAlign.Left, UiTheme.TextDim);
                string value = FormatStat(row.Kind, row.Current);
                if (row.Verdict != StatVerdict.Same) value = "(" + (row.Delta > 0 ? "+" : "") + FormatStat(row.Kind, row.Delta) + ")  " + value;
                ui.Label(line, value, UiTheme.SizeBody, UiFont.Bold, UiAlign.Right, c);
                float max = StatMax[row.Kind];
                var bar = new UiRect(inner.X + 16f, y + 24f, inner.W - 32f, 6f);
                ui.Fill(bar, UiColor.Hex(0x222527));
                float f = Math.Min(1f, row.Factory / max), k = Math.Min(1f, row.Current / max);
                ui.Fill(new UiRect(bar.X, bar.Y, bar.W * Math.Min(f, k), bar.H), UiTheme.Text.WithAlpha(0.8f));
                if (k > f) ui.Fill(new UiRect(bar.X + bar.W * f, bar.Y, bar.W * (k - f), bar.H), c);
                else if (f > k) ui.Fill(new UiRect(bar.X + bar.W * k, bar.Y, bar.W * (f - k), bar.H), c.WithAlpha(0.45f));
                y += 50f;
            }
            ui.Paragraph(new UiRect(inner.X + 16f, y + 4f, inner.W - 32f, 40f), "Verde: mejora respecto a fábrica. Rojo: empeora.", UiTheme.SizeSmall);
        }

        public static string StatName(StatKind kind)
        {
            switch (kind)
            {
                case StatKind.Ergonomics: return "Ergonomía";
                case StatKind.Recoil: return "Retroceso vertical";
                case StatKind.Weight: return "Peso";
                case StatKind.Length: return "Longitud";
                case StatKind.Loudness: return "Sonoridad";
                default: return "Fogonazo";
            }
        }

        public static string FormatStat(StatKind kind, float v)
        {
            switch (kind)
            {
                case StatKind.Recoil: return v.ToString("0.00") + "°";
                case StatKind.Weight: return v.ToString("0.00") + " kg";
                case StatKind.Length: return v.ToString("0.0") + " cm";
                case StatKind.Ergonomics: return v.ToString("0");
                default: return v.ToString("0") + " %";
            }
        }

        // ------------------------------------------------------------------ callouts

        private void DrawCallouts(Ui ui)
        {
            WeaponBuild build = Build;
            for (int i = 0; i < Slots.Length; i++)
            {
                AttachmentSlot slot = Slots[i];
                UiVec? anchorOpt = _ctx.SlotAnchor(slot);
                UiVec anchor = anchorOpt ?? new UiVec(OrbitArea.Center.X + (i % 2 == 0 ? 120f : -120f), OrbitArea.Center.Y + (i < 2 ? -40f : 60f));
                bool selected = _slotChosen && slot == _slot;
                UiVec c = anchor + BoxOffsets[i];
                c.X = Math.Max(OrbitArea.X + 130f, Math.Min(OrbitArea.XMax - 130f, c.X));
                c.Y = Math.Max(OrbitArea.Y + 40f, Math.Min(OrbitArea.YMax - 40f, c.Y));
                var box = UiRect.Around(c, 240f, 60f);
                if (anchorOpt.HasValue)
                {
                    ui.Draw.Line(box.Center, anchor, selected ? UiTheme.Highlight : UiColor.Hex(0x8A8A82, 0.8f), selected ? 2f : 1f);
                    ui.Fill(UiRect.Around(anchor, 8f, 8f), selected ? UiTheme.Highlight : UiTheme.Text);
                }
                string part = build.Get(slot);
                ItemDef def = _ctx.Db.ForAttachment(part);
                if (ui.Button(box, SlotName(slot), selected ? ButtonStyle.Selected : ButtonStyle.Normal,
                        string.IsNullOrEmpty(part) ? "vacío" : def != null ? def.Name : part, UiTheme.SizeBody))
                {
                    _slot = slot;
                    _slotChosen = true;
                }
            }
        }

        public static string SlotName(AttachmentSlot slot)
        {
            switch (slot)
            {
                case AttachmentSlot.Muzzle: return "BOCA";
                case AttachmentSlot.Barrel: return "CAÑÓN";
                case AttachmentSlot.Grips: return "CACHAS";
                default: return "CARGADOR";
            }
        }

        // ------------------------------------------------------------------ parts list

        private void DrawParts(Ui ui, UiRect r)
        {
            UiRect inner = ui.Panel(r, _slotChosen ? "PIEZAS · " + SlotName(_slot) : "PIEZAS");
            if (!_slotChosen)
            {
                ui.Paragraph(new UiRect(inner.X + 16f, inner.Y + 16f, inner.W - 32f, 80f),
                    "Elige una ranura del arma para ver las piezas compatibles. Solo puedes montar las piezas que tengas en el alijo.");
                return;
            }
            WeaponBuild build = Build;
            float y = inner.Y + 12f;
            foreach (SlotOption option in ArmorerModel.Options(build, _ctx.Catalog, _slot))
            {
                ItemDef def = _ctx.Db.ForAttachment(option.Id);
                string name = string.IsNullOrEmpty(option.Id) ? "Ninguno" : def != null ? def.Name : option.Id;
                ButtonStyle style = option.State == OptionState.Equipped ? ButtonStyle.Selected : ButtonStyle.Normal;
                string sub;
                WeaponBuild next = build;
                if (option.State == OptionState.Equipped) sub = "MONTADO";
                else if (option.State == OptionState.Blocked) { sub = option.Reason; style = ButtonStyle.Locked; }
                else
                {
                    next = ArmorerModel.Select(build, _ctx.Catalog, _slot, option.Id);
                    List<string> missing = WeaponParts.Missing(_ctx.Profile, build, next);
                    if (missing.Count > 0) { sub = "Falta en el alijo: " + string.Join(", ", missing); style = ButtonStyle.Locked; }
                    else sub = Notes(option);
                }
                var box = new UiRect(inner.X + 12f, y, inner.W - 24f, 64f);
                if (ui.Button(box, name, style, sub, UiTheme.SizeBody) && option.State == OptionState.Available) SetBuild(ui, next);
                y += 72f;
            }
        }

        private string Notes(SlotOption option)
        {
            var notes = new List<string>();
            if (option.AlsoMounts != null && option.AlsoMounts.Count > 0) notes.Add("monta también: " + Names(option.AlsoMounts));
            if (option.Removes != null && option.Removes.Count > 0) notes.Add("quita: " + Names(option.Removes));
            return notes.Count == 0 ? "En el alijo" : string.Join(" · ", notes);
        }

        private string Names(IReadOnlyList<string> ids)
        {
            var names = new List<string>();
            foreach (string id in ids)
            {
                ItemDef def = _ctx.Db.ForAttachment(id);
                names.Add(def != null ? def.ShortName : id);
            }
            return string.Join(", ", names);
        }

        private void SetBuild(Ui ui, WeaponBuild next)
        {
            WeaponBuild current = Build;
            if (next == current) return;
            if (WeaponParts.Apply(_ctx.Profile, Weapon, current, next, out string reason))
            {
                _req.BuildChanged = Weapon;
                _req.SaveProfile = true;
                Say(ui, "Configuración guardada", true);
            }
            else Say(ui, reason, false);
        }

        private void Say(Ui ui, string text, bool good)
        {
            _message = text;
            _messageGood = good;
            _messageUntil = ui.Time + 2.5f;
        }
    }
}
