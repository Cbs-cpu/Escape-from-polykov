using System;
using System.Collections.Generic;
using Polykov.UI.Framework;
using SkiaSharp;

namespace Polykov.Tools.UiPreview
{
    /// <summary>IUiBackend drawing into a Skia canvas with the game's own TTF fonts.</summary>
    public sealed class SkiaBackend : IUiBackend
    {
        private readonly SKTypeface _regular, _bold;
        public SKCanvas Canvas;

        public SkiaBackend(string fontDir)
        {
            _regular = SKTypeface.FromFile(System.IO.Path.Combine(fontDir, "PolykovGrid-Regular.ttf"));
            _bold = SKTypeface.FromFile(System.IO.Path.Combine(fontDir, "PolykovGrid-Bold.ttf"));
        }

        private static SKColor Col(UiColor c) => new SKColor(B(c.R), B(c.G), B(c.B), B(c.A));
        private static byte B(float v) => (byte)Math.Round(Math.Max(0f, Math.Min(1f, v)) * 255f);

        private SKPaint TextPaint(UiFont font, int size, UiColor c)
            => new SKPaint { Typeface = font == UiFont.Bold ? _bold : _regular, TextSize = size, Color = Col(c), IsAntialias = true, SubpixelText = true };

        public void Fill(UiRect r, UiColor c)
        {
            using var p = new SKPaint { Color = Col(c), IsAntialias = false };
            Canvas.DrawRect(r.X, r.Y, r.W, r.H, p);
        }

        public void Line(UiVec a, UiVec b, UiColor c, float width)
        {
            using var p = new SKPaint { Color = Col(c), StrokeWidth = width, IsAntialias = true, Style = SKPaintStyle.Stroke };
            Canvas.DrawLine(a.X, a.Y, b.X, b.Y, p);
        }

        public float TextWidth(string text, UiFont font, int size)
        {
            using var p = TextPaint(font, size, default);
            return p.MeasureText(text);
        }

        public void Text(UiRect r, string text, UiFont font, int size, UiColor c, UiAlign align, bool wrap = false)
        {
            if (string.IsNullOrEmpty(text)) return;
            using var p = TextPaint(font, size, c);
            SKFontMetrics m = p.FontMetrics;
            float lineH = m.Descent - m.Ascent;
            Canvas.Save();
            Canvas.ClipRect(new SKRect(r.X, r.Y - 2f, r.XMax, r.YMax + 2f));
            var lines = wrap ? Wrap(text, p, r.W) : new List<string> { text };
            float y = wrap ? r.Y - m.Ascent : r.Y + (r.H - lineH) * 0.5f - m.Ascent;
            foreach (string line in lines)
            {
                float w = p.MeasureText(line);
                float x = align == UiAlign.Left ? r.X : align == UiAlign.Right ? r.XMax - w : r.X + (r.W - w) * 0.5f;
                Canvas.DrawText(line, x, y, p);
                y += size * 1.25f;
            }
            Canvas.Restore();
        }

        private static List<string> Wrap(string text, SKPaint p, float width)
        {
            var lines = new List<string>();
            foreach (string para in text.Split('\n'))
            {
                string line = "";
                foreach (string word in para.Split(' '))
                {
                    string next = line.Length == 0 ? word : line + " " + word;
                    if (p.MeasureText(next) > width && line.Length > 0) { lines.Add(line); line = word; }
                    else line = next;
                }
                lines.Add(line);
            }
            return lines;
        }

        public void Image(UiRect r, UiImage image, UiColor tint)
        {
            if (!(image.Native is SKImage img))
            {
                if (image.Rgba == null) return;
                var info = new SKImageInfo(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
                img = SKImage.FromPixelCopy(info, image.Rgba);
                image.Native = img;
            }
            using var p = new SKPaint { FilterQuality = SKFilterQuality.High, IsAntialias = true, Color = new SKColor(255, 255, 255, B(tint.A)) };
            Canvas.DrawImage(img, new SKRect(r.X, r.Y, r.XMax, r.YMax), p);
        }

        public void PushClip(UiRect r)
        {
            Canvas.Save();
            Canvas.ClipRect(new SKRect(r.X, r.Y, r.XMax, r.YMax));
        }

        public void PopClip() => Canvas.Restore();
    }
}
