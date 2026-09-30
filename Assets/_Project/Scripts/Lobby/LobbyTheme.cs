using UnityEngine;

namespace Polykov.Lobby
{
    public enum ButtonKind { Normal, Primary, Locked, Selected, Blocked }

    /// <summary>
    /// Dark, industrial look inspired by the Escape from Tarkov menus: charcoal panels, thin grey borders, warm
    /// off-white text and one muted gold accent. Immediate-mode helpers (IMGUI), no assets needed.
    /// </summary>
    public static class LobbyTheme
    {
        public static readonly Color Background = new Color(0.035f, 0.038f, 0.04f, 1f);
        public static readonly Color Panel = new Color(0.075f, 0.08f, 0.085f, 0.94f);
        public static readonly Color PanelLight = new Color(0.115f, 0.12f, 0.125f, 0.96f);
        public static readonly Color Border = new Color(0.27f, 0.28f, 0.285f, 1f);
        public static readonly Color Text = new Color(0.84f, 0.83f, 0.78f, 1f);
        public static readonly Color TextDim = new Color(0.52f, 0.52f, 0.5f, 1f);
        public static readonly Color Accent = new Color(0.78f, 0.68f, 0.42f, 1f);
        public static readonly Color Good = new Color(0.45f, 0.76f, 0.4f, 1f);
        public static readonly Color Bad = new Color(0.86f, 0.32f, 0.27f, 1f);

        private static GUIStyle _label, _title, _small, _right, _center, _button;

        public static GUIStyle Label => _label ??= Make(16, FontStyle.Normal, TextAnchor.MiddleLeft, Text);
        public static GUIStyle Title => _title ??= Make(26, FontStyle.Bold, TextAnchor.MiddleLeft, Text);
        public static GUIStyle Small => _small ??= Make(13, FontStyle.Normal, TextAnchor.MiddleLeft, TextDim);
        public static GUIStyle Right => _right ??= Make(16, FontStyle.Normal, TextAnchor.MiddleRight, Text);
        public static GUIStyle Center => _center ??= Make(16, FontStyle.Bold, TextAnchor.MiddleCenter, Text);

        private static GUIStyle Make(int size, FontStyle style, TextAnchor anchor, Color color)
        {
            var s = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = style, alignment = anchor, wordWrap = false, clipping = TextClipping.Clip };
            s.normal.textColor = color;
            return s;
        }

        public static void Fill(Rect r, Color c)
        {
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        public static void Frame(Rect r, Color c, float thickness = 1f)
        {
            Fill(new Rect(r.x, r.y, r.width, thickness), c);
            Fill(new Rect(r.x, r.yMax - thickness, r.width, thickness), c);
            Fill(new Rect(r.x, r.y, thickness, r.height), c);
            Fill(new Rect(r.xMax - thickness, r.y, thickness, r.height), c);
        }

        public static void PanelBox(Rect r)
        {
            Fill(r, Panel);
            Frame(r, Border);
        }

        public static void Line(Vector2 a, Vector2 b, Color c, float width = 1.5f)
        {
            Vector2 d = b - a;
            float length = d.magnitude;
            if (length < 0.5f) return;
            Matrix4x4 saved = GUI.matrix;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, a);
            Fill(new Rect(a.x, a.y - width * 0.5f, length, width), c);
            GUI.matrix = saved;
        }

        public static void Text_(Rect r, string text, GUIStyle style, Color color)
        {
            Color old = style.normal.textColor;
            style.normal.textColor = color;
            GUI.Label(r, text, style);
            style.normal.textColor = old;
        }

        /// <summary>Flat button. Returns true when clicked (never for Locked/Blocked).</summary>
        public static bool Button(Rect r, string text, ButtonKind kind = ButtonKind.Normal, string subtitle = null)
        {
            bool enabled = kind != ButtonKind.Locked && kind != ButtonKind.Blocked;
            bool hover = enabled && r.Contains(Event.current.mousePosition);

            Color fill = kind == ButtonKind.Primary ? new Color(0.2f, 0.18f, 0.11f, 0.96f)
                : kind == ButtonKind.Selected ? new Color(0.17f, 0.155f, 0.1f, 0.96f) : PanelLight;
            if (hover) fill = Color.Lerp(fill, Accent, 0.16f);
            if (kind == ButtonKind.Locked || kind == ButtonKind.Blocked) fill = new Color(0.06f, 0.063f, 0.066f, 0.9f);
            Fill(r, fill);
            Frame(r, kind == ButtonKind.Primary || kind == ButtonKind.Selected ? Accent : hover ? Accent : Border);

            Color textColor = !enabled ? new Color(0.36f, 0.36f, 0.35f) : kind == ButtonKind.Primary ? Accent : Text;
            var pad = new Rect(r.x + 14f, r.y, r.width - 28f, subtitle == null ? r.height : r.height * 0.62f);
            Text_(pad, text, Center == null ? Label : LabelLeftBold, textColor);
            if (subtitle != null)
                Text_(new Rect(r.x + 14f, r.y + r.height * 0.5f, r.width - 28f, r.height * 0.4f), subtitle, Small,
                    !enabled ? new Color(0.4f, 0.32f, 0.3f) : TextDim);
            return enabled && GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        private static GUIStyle _leftBold;
        private static GUIStyle LabelLeftBold => _leftBold ??= Make(17, FontStyle.Bold, TextAnchor.MiddleLeft, Text);
    }
}
