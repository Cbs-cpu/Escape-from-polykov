using System;
using System.Collections.Generic;

namespace Polykov.UI.Framework
{
    /// <summary>
    /// Tiny engine-free polygon rasterizer (even-odd fill, 4x4 supersampling, straight alpha). Used to build item
    /// pictograms once, identically in Unity and in the offline preview renderer.
    /// </summary>
    public sealed class UiRaster
    {
        public readonly int Width, Height;
        private readonly float[] _rgba;   // premultiplied while drawing
        private const int Samples = 4;

        public UiRaster(int width, int height)
        {
            Width = width;
            Height = height;
            _rgba = new float[width * height * 4];
        }

        /// <summary>Fills a polygon given in pixel coordinates (y down).</summary>
        public void Polygon(IReadOnlyList<UiVec> pts, UiColor c)
        {
            if (pts.Count < 3) return;
            float minY = float.MaxValue, maxY = float.MinValue;
            foreach (UiVec p in pts) { minY = Math.Min(minY, p.Y); maxY = Math.Max(maxY, p.Y); }
            int y0 = Math.Max(0, (int)Math.Floor(minY)), y1 = Math.Min(Height - 1, (int)Math.Ceiling(maxY));
            var xs = new List<float>(8);
            var coverage = new float[Width];
            for (int y = y0; y <= y1; y++)
            {
                Array.Clear(coverage, 0, Width);
                bool any = false;
                for (int s = 0; s < Samples; s++)
                {
                    float sy = y + (s + 0.5f) / Samples;
                    xs.Clear();
                    for (int i = 0, j = pts.Count - 1; i < pts.Count; j = i++)
                    {
                        UiVec a = pts[i], b = pts[j];
                        if ((a.Y > sy) == (b.Y > sy)) continue;
                        xs.Add(a.X + (sy - a.Y) * (b.X - a.X) / (b.Y - a.Y));
                    }
                    xs.Sort();
                    for (int k = 0; k + 1 < xs.Count; k += 2)
                    {
                        any = true;
                        // Horizontal coverage of [xa, xb) with Samples sub-columns per pixel.
                        for (int sx = 0; sx < Samples; sx++)
                        {
                            float off = (sx + 0.5f) / Samples;
                            int from = (int)Math.Ceiling(xs[k] - off), to = (int)Math.Ceiling(xs[k + 1] - off) - 1;
                            from = Math.Max(0, from);
                            to = Math.Min(Width - 1, to);
                            for (int x = from; x <= to; x++) coverage[x] += 1f / (Samples * Samples);
                        }
                    }
                }
                if (!any) continue;
                for (int x = 0; x < Width; x++)
                {
                    float a = coverage[x] * c.A;
                    if (a <= 0f) continue;
                    int o = (y * Width + x) * 4;
                    float keep = 1f - a;
                    _rgba[o] = c.R * a + _rgba[o] * keep;
                    _rgba[o + 1] = c.G * a + _rgba[o + 1] * keep;
                    _rgba[o + 2] = c.B * a + _rgba[o + 2] * keep;
                    _rgba[o + 3] = a + _rgba[o + 3] * keep;
                }
            }
        }

        public UiImage ToImage()
        {
            var bytes = new byte[Width * Height * 4];
            for (int i = 0; i < Width * Height; i++)
            {
                float a = _rgba[i * 4 + 3];
                for (int k = 0; k < 3; k++)
                    bytes[i * 4 + k] = (byte)Math.Round(Math.Min(1f, a > 0f ? _rgba[i * 4 + k] / a : 0f) * 255f);
                bytes[i * 4 + 3] = (byte)Math.Round(Math.Min(1f, a) * 255f);
            }
            return new UiImage(Width, Height, bytes);
        }
    }
}
