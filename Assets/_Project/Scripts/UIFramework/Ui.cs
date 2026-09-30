using System;
using System.Collections.Generic;

namespace Polykov.UI.Framework
{
    public enum ButtonStyle : byte
    {
        Normal,
        /// <summary>Main call to action (play): brighter border and text.</summary>
        Primary,
        /// <summary>Not available yet: dim, never clickable.</summary>
        Locked,
        /// <summary>Currently chosen option: inverted colours like a hovered button.</summary>
        Selected,
        /// <summary>Flat text button (menus, context menu entries).</summary>
        Flat,
    }

    /// <summary>
    /// Immediate-mode UI context. Screens call it every frame between <see cref="BeginFrame"/> and <see cref="EndFrame"/>;
    /// everything is drawn through <see cref="Draw"/>. Overlays (context menus, windows, the dragged item, tooltips)
    /// are deferred to the end of the frame and block the base layer under them.
    /// </summary>
    public sealed class Ui
    {
        public IUiBackend Draw { get; }
        public UiInput Input { get; }
        public float Width { get; private set; }
        public float Height { get; private set; }
        public float Time { get; private set; }
        public float DeltaTime { get; private set; }
        /// <summary>True while overlay content runs (see <see cref="Defer"/>).</summary>
        public bool InOverlay { get; private set; }

        private readonly List<UiRect> _overlayPrev = new List<UiRect>();
        private readonly List<UiRect> _overlayNow = new List<UiRect>();
        private readonly List<Action> _deferred = new List<Action>();
        private string _tooltip;
        private string _hotSlider;

        public Ui(IUiBackend draw, UiInput input)
        {
            Draw = draw;
            Input = input;
        }

        public void BeginFrame(float width, float height, float time)
        {
            DeltaTime = Math.Max(0f, time - Time);
            Width = width;
            Height = height;
            Time = time;
            _overlayPrev.Clear();
            _overlayPrev.AddRange(_overlayNow);
            _overlayNow.Clear();
            _deferred.Clear();
            _tooltip = null;
            if (!Input.Down(0)) _hotSlider = null;
        }

        public void EndFrame()
        {
            InOverlay = true;
            // Deferred overlays may defer more (a window opening a menu): run until drained.
            for (int i = 0; i < _deferred.Count; i++) _deferred[i]();
            if (_tooltip != null) DrawTooltip(_tooltip);
            InOverlay = false;
        }

        /// <summary>Runs <paramref name="overlay"/> after the base layer (on top of it), with <see cref="InOverlay"/> set.</summary>
        public void Defer(Action overlay) => _deferred.Add(overlay);

        /// <summary>Declares an overlay area: next frame the base layer does not react to the mouse inside it.</summary>
        public void BlockArea(UiRect r) => _overlayNow.Add(r);

        public bool MouseOverOverlay()
        {
            foreach (UiRect r in _overlayPrev)
                if (r.Contains(Input.Mouse)) return true;
            return false;
        }

        public bool Hover(UiRect r) => r.Contains(Input.Mouse) && (InOverlay || !MouseOverOverlay());

        /// <summary>Hover + press this frame; consumes the press so nothing under it reacts.</summary>
        public bool Clicked(UiRect r, int button = 0)
        {
            if (!Hover(r) || !Input.Pressed(button)) return false;
            Input.Consume(button);
            return true;
        }

        public void Tooltip(string text) => _tooltip = text;

        // ------------------------------------------------------------------ primitives

        public void Fill(UiRect r, UiColor c) => Draw.Fill(r, c);

        public void Frame(UiRect r, UiColor c, float t = 1f)
        {
            Draw.Fill(new UiRect(r.X, r.Y, r.W, t), c);
            Draw.Fill(new UiRect(r.X, r.YMax - t, r.W, t), c);
            Draw.Fill(new UiRect(r.X, r.Y + t, t, r.H - 2 * t), c);
            Draw.Fill(new UiRect(r.XMax - t, r.Y + t, t, r.H - 2 * t), c);
        }

        public void Label(UiRect r, string text, int size = UiTheme.SizeBody, UiFont font = UiFont.Regular,
            UiAlign align = UiAlign.Left, UiColor? color = null)
            => Draw.Text(r, text, font, size, color ?? UiTheme.Text, align);

        public void Paragraph(UiRect r, string text, int size = UiTheme.SizeBody, UiColor? color = null)
            => Draw.Text(r, text, UiFont.Regular, size, color ?? UiTheme.TextDim, UiAlign.Left, true);

        /// <summary>Text with extra letter spacing (logos, big headings).</summary>
        public void SpacedText(UiRect r, string text, int size, UiFont font, float spacing, UiColor color, UiAlign align = UiAlign.Center)
        {
            float total = 0f;
            for (int i = 0; i < text.Length; i++) total += Draw.TextWidth(text[i].ToString(), font, size) + (i > 0 ? spacing : 0f);
            float x = align == UiAlign.Left ? r.X : align == UiAlign.Right ? r.XMax - total : r.X + (r.W - total) * 0.5f;
            for (int i = 0; i < text.Length; i++)
            {
                string ch = text[i].ToString();
                float w = Draw.TextWidth(ch, font, size);
                Draw.Text(new UiRect(x, r.Y, w + 4f, r.H), ch, font, size, color, UiAlign.Left);
                x += w + spacing;
            }
        }

        /// <summary>Faint diagonal hatching (empty slots, locked areas).</summary>
        public void Stripes(UiRect r, UiColor c, float spacing = 9f)
        {
            Draw.PushClip(r);
            for (float d = -r.H; d < r.W; d += spacing)
                Draw.Line(new UiVec(r.X + d, r.YMax), new UiVec(r.X + d + r.H, r.Y), c, 1f);
            Draw.PopClip();
        }

        // ------------------------------------------------------------------ Tarkov-like widgets

        /// <summary>Dark panel with a 1 px border and an optional header bar.</summary>
        public UiRect Panel(UiRect r, string title = null, string rightInfo = null)
        {
            Fill(r, UiTheme.Panel);
            Frame(r, UiTheme.Border);
            if (title == null) return r.Shrink(1f);
            UiRect header = new UiRect(r.X + 1, r.Y + 1, r.W - 2, 30f);
            Fill(header, UiTheme.Header);
            Fill(new UiRect(header.X, header.YMax, header.W, 1f), UiTheme.Border);
            Label(header.Shrink(12f, 0f), title, UiTheme.SizeBody, UiFont.Bold, UiAlign.Left, UiTheme.Text);
            if (rightInfo != null) Label(header.Shrink(12f, 0f), rightInfo, UiTheme.SizeSmall, UiFont.Regular, UiAlign.Right, UiTheme.TextDim);
            return new UiRect(r.X + 1, header.YMax + 1, r.W - 2, r.YMax - header.YMax - 2);
        }

        /// <summary>Section title with a rule under it.</summary>
        public void SectionTitle(UiRect r, string text, string right = null)
        {
            Label(r, text, UiTheme.SizeSmall, UiFont.Bold, UiAlign.Left, UiTheme.TextDim);
            if (right != null) Label(r, right, UiTheme.SizeSmall, UiFont.Regular, UiAlign.Right, UiTheme.TextDim);
            Fill(new UiRect(r.X, r.YMax - 1f, r.W, 1f), UiTheme.Border);
        }

        public bool Button(UiRect r, string text, ButtonStyle style = ButtonStyle.Normal, string subtitle = null, int size = UiTheme.SizeLabel)
        {
            bool enabled = style != ButtonStyle.Locked;
            bool hover = enabled && Hover(r);
            bool inverted = hover || style == ButtonStyle.Selected;
            UiColor back = inverted ? UiTheme.Highlight
                : style == ButtonStyle.Flat ? new UiColor(0, 0, 0, 0)
                : style == ButtonStyle.Locked ? UiColor.Hex(0x0D0E0F, 0.9f)
                : style == ButtonStyle.Primary ? UiColor.Hex(0x1C1D1B, 0.95f) : UiColor.Hex(0x151617, 0.94f);
            if (back.A > 0f) Fill(r, back);
            if (style != ButtonStyle.Flat)
                Frame(r, inverted ? UiTheme.Highlight : style == ButtonStyle.Primary ? UiTheme.BorderLight : UiTheme.Border);
            UiColor ink = inverted ? UiTheme.Ink : !enabled ? UiTheme.TextDisabled
                : style == ButtonStyle.Primary ? UiTheme.TextBright : UiTheme.Text;
            UiRect content = r.Shrink(style == ButtonStyle.Flat ? 10f : 16f, 0f);
            if (subtitle == null)
            {
                Label(content, text, size, UiFont.Bold, style == ButtonStyle.Primary ? UiAlign.Center : UiAlign.Left, ink);
            }
            else
            {
                Label(new UiRect(content.X, r.Y + r.H * 0.12f, content.W, r.H * 0.5f), text, size, UiFont.Bold, UiAlign.Left, ink);
                UiColor sub = inverted ? UiColor.Hex(0x3A3B37) : !enabled ? UiColor.Hex(0x5A4B45) : UiTheme.TextDim;
                Label(new UiRect(content.X, r.Y + r.H * 0.56f, content.W, r.H * 0.34f), subtitle, UiTheme.SizeSmall, UiFont.Regular, UiAlign.Left, sub);
            }
            if (!enabled) return false;
            return Clicked(r);
        }

        /// <summary>Navigation tab (bottom bar): selected = light top line and lighter background.</summary>
        public bool Tab(UiRect r, string text, bool selected, bool enabled = true)
        {
            bool hover = enabled && Hover(r);
            if (selected || hover) Fill(r, selected ? UiColor.Hex(0x2A2C2D, 0.96f) : UiColor.Hex(0x1C1E1F, 0.96f));
            if (selected) Fill(new UiRect(r.X, r.Y, r.W, 2f), UiTheme.Highlight);
            UiColor ink = !enabled ? UiTheme.TextDisabled : selected || hover ? UiTheme.TextBright : UiTheme.TextDim;
            Label(r, text, UiTheme.SizeBody, UiFont.Bold, UiAlign.Center, ink);
            if (!enabled && Hover(r)) Tooltip("Próximamente");
            return enabled && !selected && Clicked(r);
        }

        public bool Toggle(UiRect r, bool value, string text)
        {
            bool hover = Hover(r);
            UiRect box = new UiRect(r.X, r.Y + (r.H - 16f) * 0.5f, 16f, 16f);
            Fill(box, UiColor.Hex(0x0D0E0F));
            Frame(box, hover ? UiTheme.Highlight : UiTheme.BorderLight);
            if (value) Fill(box.Shrink(4f), UiTheme.Text);
            Label(new UiRect(box.XMax + 10f, r.Y, r.W - 26f, r.H), text, UiTheme.SizeBody, UiFont.Regular, UiAlign.Left,
                hover ? UiTheme.TextBright : UiTheme.Text);
            return Clicked(r) ? !value : value;
        }

        /// <summary>Horizontal slider; <paramref name="id"/> must be unique among the sliders visible at once.</summary>
        public float Slider(string id, UiRect r, float value, float min, float max, string format = "0.00")
        {
            UiRect track = new UiRect(r.X, r.Y + r.H * 0.5f - 2f, r.W - 70f, 4f);
            UiRect hit = new UiRect(track.X - 6f, r.Y, track.W + 12f, r.H);
            if (Hover(hit) && Input.Pressed(0)) { _hotSlider = id; Input.Consume(0); }
            if (_hotSlider == id)
            {
                float t = Math.Max(0f, Math.Min(1f, (Input.Mouse.X - track.X) / track.W));
                value = min + (max - min) * t;
            }
            float k = max > min ? (value - min) / (max - min) : 0f;
            Fill(track, UiColor.Hex(0x2A2D2F));
            Fill(new UiRect(track.X, track.Y, track.W * k, track.H), UiTheme.Text);
            UiRect knob = UiRect.Around(new UiVec(track.X + track.W * k, track.Y + 2f), 8f, 18f);
            Fill(knob, _hotSlider == id || Hover(hit) ? UiTheme.TextBright : UiTheme.Text);
            Label(new UiRect(track.XMax + 10f, r.Y, 60f, r.H), value.ToString(format), UiTheme.SizeBody, UiFont.Regular, UiAlign.Right, UiTheme.Text);
            return value;
        }

        /// <summary>Scrollable vertical region: returns the content origin offset; call <see cref="EndScroll"/> after drawing.</summary>
        public float BeginScroll(UiRect view, ref float scroll, float contentHeight, float step = UiTheme.Cell)
        {
            float max = Math.Max(0f, contentHeight - view.H);
            if (Hover(view))
            {
                float wheel = Input.TakeScroll();
                if (wheel != 0f) scroll += wheel * step;
            }
            scroll = Math.Max(0f, Math.Min(max, scroll));
            Draw.PushClip(view);
            if (max > 0f)
            {
                UiRect bar = new UiRect(view.XMax - 5f, view.Y, 4f, view.H);
                Fill(bar, UiColor.Hex(0x1A1C1D));
                float h = Math.Max(30f, view.H * view.H / contentHeight);
                Fill(new UiRect(bar.X, view.Y + (view.H - h) * (scroll / max), bar.W, h), UiTheme.BorderLight);
            }
            return scroll;
        }

        public void EndScroll() => Draw.PopClip();

        private void DrawTooltip(string text)
        {
            float w = Draw.TextWidth(text, UiFont.Regular, UiTheme.SizeBody) + 20f;
            var r = new UiRect(Input.Mouse.X + 16f, Input.Mouse.Y + 20f, w, 26f);
            if (r.XMax > Width - 4f) r.X = Width - 4f - r.W;
            if (r.YMax > Height - 4f) r.Y = Input.Mouse.Y - 30f;
            Fill(r, UiColor.Hex(0x0B0C0D, 0.96f));
            Frame(r, UiTheme.BorderLight);
            Label(r.Shrink(10f, 0f), text, UiTheme.SizeBody, UiFont.Regular, UiAlign.Left, UiTheme.TextBright);
        }
    }
}
