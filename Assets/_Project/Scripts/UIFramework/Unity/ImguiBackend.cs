using System.Collections.Generic;
using UnityEngine;

namespace Polykov.UI.Framework
{
    /// <summary>
    /// IUiBackend for Unity: the UI runs once per frame in Update and records draw commands; OnGUI replays them on
    /// the Repaint event, scaled from the 1080p reference to the real screen (text is rasterized at the real size).
    /// </summary>
    public sealed class ImguiBackend : IUiBackend
    {
        private enum Kind : byte { Fill, Line, Text, Image, PushClip, PopClip }

        private struct Cmd
        {
            public Kind Kind;
            public UiRect R;
            public UiVec A, B;
            public UiColor C;
            public float Width;
            public string Text;
            public UiFont Font;
            public int Size;
            public UiAlign Align;
            public bool Wrap;
            public UiImage Image;
        }

        private readonly List<Cmd> _cmds = new List<Cmd>(2048);
        private readonly Font _regular, _bold;
        private readonly Dictionary<int, GUIStyle> _styles = new Dictionary<int, GUIStyle>();
        private readonly Stack<Vector2> _origins = new Stack<Vector2>();
        private float _scale = 1f;

        public ImguiBackend(Font regular, Font bold)
        {
            _regular = regular;
            _bold = bold != null ? bold : regular;
        }

        public void BeginFrame(float scale)
        {
            _scale = scale;
            _cmds.Clear();
        }

        public void Fill(UiRect r, UiColor c) => _cmds.Add(new Cmd { Kind = Kind.Fill, R = r, C = c });
        public void Line(UiVec a, UiVec b, UiColor c, float width) => _cmds.Add(new Cmd { Kind = Kind.Line, A = a, B = b, C = c, Width = width });
        public void Image(UiRect r, UiImage image, UiColor tint) => _cmds.Add(new Cmd { Kind = Kind.Image, R = r, Image = image, C = tint });
        public void PushClip(UiRect r) => _cmds.Add(new Cmd { Kind = Kind.PushClip, R = r });
        public void PopClip() => _cmds.Add(new Cmd { Kind = Kind.PopClip });

        public void Text(UiRect r, string text, UiFont font, int size, UiColor c, UiAlign align, bool wrap = false)
        {
            if (!string.IsNullOrEmpty(text))
                _cmds.Add(new Cmd { Kind = Kind.Text, R = r, Text = text, Font = font, Size = size, C = c, Align = align, Wrap = wrap });
        }

        public float TextWidth(string text, UiFont font, int size)
        {
            Font f = font == UiFont.Bold ? _bold : _regular;
            if (f == null || string.IsNullOrEmpty(text)) return text == null ? 0f : text.Length * size * 0.5f;
            // Measure at the real raster size so layout matches what is drawn.
            int px = Mathf.Max(1, Mathf.RoundToInt(size * _scale));
            f.RequestCharactersInTexture(text, px, FontStyle.Normal);
            float w = 0f;
            foreach (char ch in text)
                if (f.GetCharacterInfo(ch, out CharacterInfo info, px, FontStyle.Normal)) w += info.advance;
            return w / _scale;
        }

        // ------------------------------------------------------------------ replay (OnGUI)

        public void Replay()
        {
            if (Event.current.type != EventType.Repaint) return;
            _origins.Clear();
            Vector2 origin = Vector2.zero;
            Color saved = GUI.color;
            foreach (Cmd c in _cmds)
            {
                switch (c.Kind)
                {
                    case Kind.Fill:
                        GUI.color = ToColor(c.C);
                        GUI.DrawTexture(ToRect(c.R, origin), Texture2D.whiteTexture);
                        break;
                    case Kind.Line:
                        DrawLine(c, origin);
                        break;
                    case Kind.Text:
                        GUI.color = Color.white;
                        GUIStyle style = StyleFor(c);
                        style.normal.textColor = ToColor(c.C);
                        GUI.Label(ToRect(c.R, origin), c.Text, style);
                        break;
                    case Kind.Image:
                        Texture tex = TextureOf(c.Image);
                        if (tex == null) break;
                        GUI.color = ToColor(c.C);
                        GUI.DrawTexture(ToRect(c.R, origin), tex, ScaleMode.StretchToFill, true);
                        break;
                    case Kind.PushClip:
                        Rect clip = ToRect(c.R, origin);
                        GUI.BeginClip(clip);
                        _origins.Push(origin);
                        origin = new Vector2(origin.x + clip.x, origin.y + clip.y);
                        break;
                    case Kind.PopClip:
                        if (_origins.Count == 0) break;
                        GUI.EndClip();
                        origin = _origins.Pop();
                        break;
                }
            }
            while (_origins.Count > 0) { GUI.EndClip(); _origins.Pop(); }
            GUI.color = saved;
        }

        private Rect ToRect(UiRect r, Vector2 origin)
            => new Rect(r.X * _scale - origin.x, r.Y * _scale - origin.y, r.W * _scale, r.H * _scale);

        private static Color ToColor(UiColor c) => new Color(c.R, c.G, c.B, c.A);

        private void DrawLine(Cmd c, Vector2 origin)
        {
            Vector2 a = new Vector2(c.A.X * _scale - origin.x, c.A.Y * _scale - origin.y);
            Vector2 b = new Vector2(c.B.X * _scale - origin.x, c.B.Y * _scale - origin.y);
            Vector2 d = b - a;
            float length = d.magnitude;
            if (length < 0.5f) return;
            float width = Mathf.Max(1f, c.Width * _scale);
            Matrix4x4 saved = GUI.matrix;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, a);
            GUI.color = ToColor(c.C);
            GUI.DrawTexture(new Rect(a.x, a.y - width * 0.5f, length, width), Texture2D.whiteTexture);
            GUI.matrix = saved;
        }

        private GUIStyle StyleFor(in Cmd c)
        {
            int px = Mathf.Max(1, Mathf.RoundToInt(c.Size * _scale));
            int key = (px << 5) | ((int)c.Font << 3) | ((int)c.Align << 1) | (c.Wrap ? 1 : 0);
            if (_styles.TryGetValue(key, out GUIStyle s)) return s;
            s = new GUIStyle(GUI.skin.label)
            {
                font = c.Font == UiFont.Bold ? _bold : _regular,
                fontSize = px,
                fontStyle = FontStyle.Normal,
                wordWrap = c.Wrap,
                clipping = TextClipping.Clip,
                richText = false,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
                alignment = c.Wrap ? TextAnchor.UpperLeft
                    : c.Align == UiAlign.Left ? TextAnchor.MiddleLeft : c.Align == UiAlign.Right ? TextAnchor.MiddleRight : TextAnchor.MiddleCenter,
            };
            _styles[key] = s;
            return s;
        }

        /// <summary>Unity texture for an image: native Texture2D, or built once from the raw RGBA (top row first).</summary>
        public static Texture TextureOf(UiImage image)
        {
            if (image == null) return null;
            if (image.Native is Texture t) return t;
            if (image.Rgba == null) return null;
            var tex = new Texture2D(image.Width, image.Height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "UiImage",
            };
            // Unity textures are bottom-up.
            var flipped = new byte[image.Rgba.Length];
            int row = image.Width * 4;
            for (int y = 0; y < image.Height; y++)
                System.Buffer.BlockCopy(image.Rgba, y * row, flipped, (image.Height - 1 - y) * row, row);
            tex.LoadRawTextureData(flipped);
            tex.Apply(false, true);
            image.Native = tex;
            return tex;
        }
    }
}
