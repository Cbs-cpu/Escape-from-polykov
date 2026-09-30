using System;

namespace Polykov.UI.Framework
{
    /// <summary>Engine-free 2D point (UI pixels, y down, 1080p reference height).</summary>
    public struct UiVec
    {
        public float X, Y;
        public UiVec(float x, float y) { X = x; Y = y; }
        public static UiVec operator +(UiVec a, UiVec b) => new UiVec(a.X + b.X, a.Y + b.Y);
        public static UiVec operator -(UiVec a, UiVec b) => new UiVec(a.X - b.X, a.Y - b.Y);
        public static UiVec operator *(UiVec a, float s) => new UiVec(a.X * s, a.Y * s);
        public float Length => (float)Math.Sqrt(X * X + Y * Y);
    }

    /// <summary>Engine-free rectangle (x, y = top-left).</summary>
    public struct UiRect
    {
        public float X, Y, W, H;
        public UiRect(float x, float y, float w, float h) { X = x; Y = y; W = w; H = h; }
        public float XMax => X + W;
        public float YMax => Y + H;
        public UiVec Center => new UiVec(X + W * 0.5f, Y + H * 0.5f);
        public bool Contains(UiVec p) => p.X >= X && p.X < X + W && p.Y >= Y && p.Y < Y + H;
        public bool Overlaps(UiRect o) => X < o.XMax && o.X < XMax && Y < o.YMax && o.Y < YMax;
        public UiRect Shrink(float d) => new UiRect(X + d, Y + d, W - 2 * d, H - 2 * d);
        public UiRect Shrink(float dx, float dy) => new UiRect(X + dx, Y + dy, W - 2 * dx, H - 2 * dy);
        public UiRect Move(float dx, float dy) => new UiRect(X + dx, Y + dy, W, H);
        public UiRect TakeTop(float h) => new UiRect(X, Y, W, h);
        public UiRect TakeBottom(float h) => new UiRect(X, YMax - h, W, h);
        public UiRect TakeLeft(float w) => new UiRect(X, Y, w, H);
        public UiRect TakeRight(float w) => new UiRect(XMax - w, Y, w, H);
        public UiRect CutTop(float h) => new UiRect(X, Y + h, W, H - h);
        public UiRect CutLeft(float w) => new UiRect(X + w, Y, W - w, H);
        public static UiRect Around(UiVec c, float w, float h) => new UiRect(c.X - w * 0.5f, c.Y - h * 0.5f, w, h);
        public override string ToString() => $"({X:0},{Y:0},{W:0},{H:0})";
    }

    /// <summary>Engine-free colour, sRGB components 0..1.</summary>
    public struct UiColor
    {
        public float R, G, B, A;
        public UiColor(float r, float g, float b, float a = 1f) { R = r; G = g; B = b; A = a; }

        /// <summary>From 0xRRGGBB.</summary>
        public static UiColor Hex(uint rgb, float a = 1f)
            => new UiColor(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, a);

        public UiColor WithAlpha(float a) => new UiColor(R, G, B, a);
        public static UiColor Lerp(UiColor a, UiColor b, float t)
            => new UiColor(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t, a.A + (b.A - a.A) * t);
    }

    public enum UiFont : byte { Regular, Bold }

    public enum UiAlign : byte { Left, Center, Right }

    public enum UiKey : byte { Escape, R, Delete, Enter, Tab, Space, I, E }

    /// <summary>
    /// A bitmap the backends can draw: raw RGBA (top row first) produced by engine-free code (e.g. pictograms),
    /// and/or a backend-native texture handle (Unity Texture2D, Skia image) cached in <see cref="Native"/>.
    /// </summary>
    public sealed class UiImage
    {
        public int Width, Height;
        public byte[] Rgba;
        public object Native;
        public UiImage(int width, int height, byte[] rgba = null) { Width = width; Height = height; Rgba = rgba; }
        public UiImage(object native, int width, int height) { Native = native; Width = width; Height = height; }
    }
}
