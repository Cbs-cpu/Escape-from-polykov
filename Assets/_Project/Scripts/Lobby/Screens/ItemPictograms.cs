using System;
using System.Collections.Generic;
using Polykov.Inventory;
using Polykov.UI.Framework;

namespace Polykov.Lobby
{
    /// <summary>
    /// Flat low-poly pictograms for items without a 3D model (drawn in cell units over the item's footprint, then
    /// rasterized once). Items with a model get a rendered icon from the host instead (see <see cref="IItemIcons"/>).
    /// </summary>
    public static class ItemPictograms
    {
        public const int PixelsPerCell = 64;
        private static readonly Dictionary<string, UiImage> Cache = new Dictionary<string, UiImage>();

        // Palette (muted, like the lobby).
        private static readonly UiColor MetalDark = UiColor.Hex(0x34383A), Metal = UiColor.Hex(0x5A5F61), MetalLight = UiColor.Hex(0x8C9091);
        private static readonly UiColor Brass = UiColor.Hex(0xA9884A), BrassLight = UiColor.Hex(0xD2B26E), Copper = UiColor.Hex(0x9C6139);
        private static readonly UiColor Cloth = UiColor.Hex(0x4C5638), ClothLight = UiColor.Hex(0x66714B), ClothDark = UiColor.Hex(0x363D28);
        private static readonly UiColor Tan = UiColor.Hex(0x8F7C58), TanLight = UiColor.Hex(0xAE9A73), TanDark = UiColor.Hex(0x6A5B40);
        private static readonly UiColor White = UiColor.Hex(0xCFCBBD), WhiteDim = UiColor.Hex(0xA09C90);
        private static readonly UiColor Red = UiColor.Hex(0xA3342C), Orange = UiColor.Hex(0xC06A2C), Blue = UiColor.Hex(0x3F5E86);
        private static readonly UiColor Black = UiColor.Hex(0x1C1E1F), BlackLight = UiColor.Hex(0x2E3133), Wood = UiColor.Hex(0x7A4E2E), WoodLight = UiColor.Hex(0x9A6A42);
        private static readonly UiColor Water = UiColor.Hex(0x7FA0B4), Green = UiColor.Hex(0x4F7A3E), GreenLight = UiColor.Hex(0x6E9A58);

        public static UiImage Get(ItemDef def, bool rotated)
        {
            string key = def.Id + (rotated ? "/r" : "");
            if (Cache.TryGetValue(key, out UiImage img)) return img;
            var pic = new Pic(def.Width, def.Height, rotated);
            Draw(def, pic);
            img = pic.Raster.ToImage();
            Cache[key] = img;
            return img;
        }

        private sealed class Pic
        {
            public readonly UiRaster Raster;
            private readonly int _w, _h;
            private readonly bool _rot;

            public Pic(int w, int h, bool rotated)
            {
                _w = w; _h = h; _rot = rotated;
                Raster = rotated ? new UiRaster(h * PixelsPerCell, w * PixelsPerCell) : new UiRaster(w * PixelsPerCell, h * PixelsPerCell);
            }

            public float W => _w;
            public float H => _h;

            /// <summary>Polygon in cell units of the upright footprint (x right, y down).</summary>
            public void Poly(UiColor c, params float[] xy)
            {
                var pts = new List<UiVec>(xy.Length / 2);
                for (int i = 0; i + 1 < xy.Length; i += 2)
                {
                    float x = xy[i], y = xy[i + 1];
                    if (_rot) { float nx = _h - y; y = x; x = nx; }
                    pts.Add(new UiVec(x * PixelsPerCell, y * PixelsPerCell));
                }
                Raster.Polygon(pts, c);
            }

            public void Rect(UiColor c, float x, float y, float w, float h) => Poly(c, x, y, x + w, y, x + w, y + h, x, y + h);

            /// <summary>Rectangle with chamfered corners.</summary>
            public void Oct(UiColor c, float x, float y, float w, float h, float ch)
                => Poly(c, x + ch, y, x + w - ch, y, x + w, y + ch, x + w, y + h - ch, x + w - ch, y + h, x + ch, y + h, x, y + h - ch, x, y + ch);

            /// <summary>Thick segment (for wires, straps, key teeth).</summary>
            public void Bar(UiColor c, float x0, float y0, float x1, float y1, float t)
            {
                float dx = x1 - x0, dy = y1 - y0, n = (float)Math.Sqrt(dx * dx + dy * dy);
                if (n < 1e-5f) return;
                float ox = -dy / n * t * 0.5f, oy = dx / n * t * 0.5f;
                Poly(c, x0 + ox, y0 + oy, x1 + ox, y1 + oy, x1 - ox, y1 - oy, x0 - ox, y0 - oy);
            }
        }

        private static void Draw(ItemDef def, Pic p)
        {
            switch (def.Id)
            {
                case "ammo_45_fmj": Bullets(p, Copper); return;
                case "ammo_45_hp": Bullets(p, UiColor.Hex(0x6F7274)); return;
                case "bandage":
                    p.Oct(WhiteDim, 0.22f, 0.3f, 0.56f, 0.42f, 0.08f);
                    p.Oct(White, 0.26f, 0.3f, 0.48f, 0.34f, 0.07f);
                    p.Rect(WhiteDim, 0.44f, 0.3f, 0.05f, 0.34f);
                    p.Poly(White, 0.74f, 0.52f, 0.9f, 0.62f, 0.86f, 0.72f, 0.72f, 0.64f);
                    return;
                case "ai2":
                    p.Oct(UiColor.Hex(0x9A4F22), 0.2f, 0.2f, 0.6f, 0.62f, 0.06f);
                    p.Oct(Orange, 0.2f, 0.2f, 0.6f, 0.52f, 0.06f);
                    p.Rect(White, 0.46f, 0.3f, 0.08f, 0.3f);
                    p.Rect(White, 0.35f, 0.41f, 0.3f, 0.08f);
                    return;
                case "carkit":
                    p.Oct(UiColor.Hex(0x7A2520), 0.14f, 0.3f, 0.72f, 1.5f, 0.08f);
                    p.Oct(Red, 0.14f, 0.3f, 0.72f, 1.36f, 0.08f);
                    p.Rect(White, 0.42f, 0.72f, 0.16f, 0.5f);
                    p.Rect(White, 0.25f, 0.89f, 0.5f, 0.16f);
                    p.Rect(BlackLight, 0.3f, 0.16f, 0.4f, 0.14f);
                    return;
                case "splint":
                    p.Oct(TanDark, 0.36f, 0.1f, 0.28f, 0.8f, 0.06f);
                    p.Oct(TanLight, 0.38f, 0.1f, 0.2f, 0.78f, 0.06f);
                    p.Rect(White, 0.32f, 0.3f, 0.36f, 0.06f);
                    p.Rect(White, 0.32f, 0.62f, 0.36f, 0.06f);
                    return;
                case "water":
                    p.Rect(Blue, 0.4f, 0.1f, 0.2f, 0.12f);
                    p.Poly(Water.WithAlpha(0.85f), 0.42f, 0.22f, 0.58f, 0.22f, 0.72f, 0.42f, 0.72f, 1.82f, 0.66f, 1.9f, 0.34f, 1.9f, 0.28f, 1.82f, 0.28f, 0.42f);
                    p.Rect(UiColor.Hex(0xB9CCD6), 0.33f, 0.5f, 0.08f, 1.2f);
                    p.Rect(UiColor.Hex(0x3E6A8C), 0.28f, 0.9f, 0.44f, 0.4f);
                    return;
                case "tushonka":
                    p.Oct(MetalLight, 0.2f, 0.22f, 0.6f, 0.58f, 0.08f);
                    p.Rect(UiColor.Hex(0x7E2B24), 0.2f, 0.36f, 0.6f, 0.3f);
                    p.Rect(BrassLight, 0.3f, 0.44f, 0.4f, 0.12f);
                    return;
                case "crackers":
                    p.Oct(TanDark, 0.16f, 0.22f, 0.68f, 0.56f, 0.05f);
                    p.Oct(Tan, 0.16f, 0.22f, 0.68f, 0.48f, 0.05f);
                    for (int i = 0; i < 3; i++) p.Rect(TanLight, 0.24f + i * 0.2f, 0.3f, 0.12f, 0.3f);
                    return;
                case "bolts":
                    Nut(p, 0.36f, 0.4f, 0.2f); Nut(p, 0.62f, 0.56f, 0.18f); Nut(p, 0.4f, 0.7f, 0.15f);
                    return;
                case "wires":
                    for (int i = 0; i < 4; i++)
                    {
                        float y = 0.3f + i * 0.12f;
                        p.Bar(i % 2 == 0 ? Red : Blue, 0.18f, y, 0.82f, y + 0.08f, 0.05f);
                    }
                    p.Bar(Copper, 0.8f, 0.34f, 0.9f, 0.3f, 0.04f);
                    return;
                case "gunpowder":
                    p.Oct(UiColor.Hex(0x3C4A2C), 0.2f, 0.15f, 1.6f, 0.7f, 0.1f);
                    p.Oct(GreenLight, 0.2f, 0.15f, 1.6f, 0.6f, 0.1f);
                    p.Poly(BrassLight, 0.8f, 0.3f, 1.0f, 0.42f, 1.2f, 0.3f, 1.1f, 0.55f, 0.9f, 0.55f);
                    p.Rect(Black, 1.55f, 0.3f, 0.12f, 0.34f);
                    return;
                case "cigs":
                    p.Oct(UiColor.Hex(0x7D2320), 0.28f, 0.2f, 0.44f, 0.62f, 0.04f);
                    p.Rect(White, 0.28f, 0.2f, 0.44f, 0.2f);
                    p.Poly(BrassLight, 0.44f, 0.5f, 0.5f, 0.44f, 0.56f, 0.5f, 0.5f, 0.56f);
                    return;
                case "key_dorm":
                    p.Oct(BrassLight, 0.18f, 0.2f, 0.34f, 0.34f, 0.1f);
                    p.Oct(Black, 0.28f, 0.3f, 0.14f, 0.14f, 0.04f);
                    p.Bar(Brass, 0.46f, 0.46f, 0.84f, 0.84f, 0.1f);
                    p.Bar(Brass, 0.72f, 0.72f, 0.64f, 0.8f, 0.08f);
                    p.Bar(Brass, 0.8f, 0.8f, 0.72f, 0.88f, 0.08f);
                    return;
                case "roubles":
                    p.Rect(UiColor.Hex(0x5C6B4A), 0.14f, 0.36f, 0.72f, 0.38f);
                    p.Rect(UiColor.Hex(0x7B8C63), 0.18f, 0.3f, 0.72f, 0.38f);
                    p.Rect(UiColor.Hex(0x9AAB7E), 0.22f, 0.26f, 0.64f, 0.34f);
                    p.Oct(UiColor.Hex(0x5C6B4A), 0.46f, 0.32f, 0.18f, 0.22f, 0.05f);
                    return;
                case "knife":
                    p.Poly(MetalLight, 0.5f, 0.1f, 0.62f, 0.22f, 0.62f, 1.1f, 0.4f, 1.1f, 0.4f, 0.3f);
                    p.Poly(Metal, 0.5f, 0.1f, 0.5f, 1.1f, 0.4f, 1.1f, 0.4f, 0.3f);
                    p.Rect(BlackLight, 0.3f, 1.1f, 0.42f, 0.08f);
                    p.Oct(Black, 0.39f, 1.18f, 0.24f, 0.66f, 0.05f);
                    return;
                case "cap":
                    p.Poly(ClothDark, 0.3f, 1.2f, 1.9f, 1.2f, 1.9f, 1.34f, 0.2f, 1.34f);
                    p.Poly(Cloth, 0.45f, 1.2f, 0.55f, 0.75f, 0.85f, 0.55f, 1.15f, 0.55f, 1.45f, 0.75f, 1.55f, 1.2f);
                    p.Poly(ClothLight, 0.62f, 0.8f, 0.88f, 0.62f, 1.0f, 0.62f, 1.0f, 1.18f, 0.6f, 1.18f);
                    return;
                case "helmet":
                    p.Poly(ClothDark, 0.25f, 1.4f, 1.75f, 1.4f, 1.8f, 1.55f, 0.2f, 1.55f);
                    p.Poly(Cloth, 0.25f, 1.42f, 0.3f, 0.85f, 0.65f, 0.45f, 1.35f, 0.45f, 1.7f, 0.85f, 1.75f, 1.42f);
                    p.Poly(ClothLight, 0.5f, 0.85f, 0.75f, 0.55f, 1.1f, 0.55f, 1.1f, 1.3f, 0.45f, 1.3f);
                    p.Rect(Black, 0.85f, 0.95f, 0.3f, 0.12f);
                    return;
                case "headset":
                    p.Bar(Black, 0.5f, 0.9f, 0.7f, 0.45f, 0.14f);
                    p.Bar(Black, 0.7f, 0.45f, 1.3f, 0.45f, 0.14f);
                    p.Bar(Black, 1.3f, 0.45f, 1.5f, 0.9f, 0.14f);
                    p.Oct(ClothDark, 0.3f, 0.85f, 0.42f, 0.66f, 0.1f);
                    p.Oct(ClothDark, 1.28f, 0.85f, 0.42f, 0.66f, 0.1f);
                    p.Oct(Cloth, 0.34f, 0.89f, 0.3f, 0.5f, 0.08f);
                    p.Oct(Cloth, 1.36f, 0.89f, 0.3f, 0.5f, 0.08f);
                    return;
                case "glasses":
                    p.Poly(Black, 0.15f, 0.3f, 1.85f, 0.3f, 1.8f, 0.42f, 0.2f, 0.42f);
                    p.Poly(UiColor.Hex(0x3C4A55), 0.22f, 0.4f, 0.92f, 0.4f, 0.86f, 0.74f, 0.3f, 0.7f);
                    p.Poly(UiColor.Hex(0x3C4A55), 1.08f, 0.4f, 1.78f, 0.4f, 1.7f, 0.7f, 1.14f, 0.74f);
                    p.Poly(UiColor.Hex(0x6E8494), 0.3f, 0.44f, 0.6f, 0.44f, 0.4f, 0.6f);
                    return;
                case "balaclava":
                    p.Poly(Black, 0.55f, 1.8f, 0.45f, 0.8f, 0.7f, 0.3f, 1.3f, 0.3f, 1.55f, 0.8f, 1.45f, 1.8f);
                    p.Poly(BlackLight, 0.7f, 0.4f, 1.0f, 0.4f, 1.0f, 1.7f, 0.6f, 1.7f, 0.56f, 0.8f);
                    p.Rect(UiColor.Hex(0x0D0E0E), 0.68f, 0.82f, 0.64f, 0.16f);
                    return;
                case "armband":
                    p.Oct(Blue, 0.16f, 0.3f, 0.68f, 0.4f, 0.08f);
                    p.Rect(UiColor.Hex(0x2B4260), 0.16f, 0.46f, 0.68f, 0.08f);
                    return;
                case "armor":
                    p.Poly(ClothDark, 0.7f, 0.3f, 1.1f, 0.3f, 1.5f, 0.6f, 1.9f, 0.3f, 2.3f, 0.3f, 2.5f, 0.7f, 2.5f, 2.7f, 0.5f, 2.7f, 0.5f, 0.7f);
                    p.Poly(Cloth, 0.62f, 0.8f, 1.2f, 0.62f, 1.5f, 0.85f, 1.8f, 0.62f, 2.38f, 0.8f, 2.38f, 2.58f, 0.62f, 2.58f);
                    p.Rect(ClothLight, 0.8f, 1.2f, 1.4f, 0.12f);
                    p.Rect(ClothLight, 0.8f, 1.5f, 1.4f, 0.12f);
                    p.Rect(ClothLight, 0.8f, 1.8f, 1.4f, 0.12f);
                    return;
                case "rig":
                    p.Poly(ClothDark, 0.35f, 0.25f, 0.65f, 0.25f, 1.0f, 0.6f, 1.35f, 0.25f, 1.65f, 0.25f, 1.8f, 0.8f, 1.8f, 2.7f, 0.2f, 2.7f, 0.2f, 0.8f);
                    for (int i = 0; i < 4; i++) p.Oct(Cloth, 0.3f + i * 0.36f, 1.2f, 0.3f, 0.8f, 0.06f);
                    for (int i = 0; i < 4; i++) p.Rect(ClothLight, 0.3f + i * 0.36f, 1.2f, 0.3f, 0.14f);
                    p.Oct(Cloth, 0.35f, 2.1f, 0.55f, 0.45f, 0.06f);
                    p.Oct(Cloth, 1.1f, 2.1f, 0.55f, 0.45f, 0.06f);
                    return;
                case "backpack":
                    p.Oct(TanDark, 0.4f, 0.4f, 3.2f, 4.3f, 0.35f);
                    p.Oct(Tan, 0.5f, 0.45f, 3.0f, 3.9f, 0.3f);
                    p.Oct(TanLight, 0.6f, 0.5f, 2.8f, 1.1f, 0.25f);
                    p.Oct(TanDark, 0.9f, 2.4f, 2.2f, 1.6f, 0.2f);
                    p.Oct(Tan, 1.0f, 2.5f, 2.0f, 1.35f, 0.18f);
                    p.Rect(TanDark, 1.0f, 1.85f, 2.0f, 0.14f);
                    p.Rect(Black, 1.9f, 0.2f, 0.2f, 0.4f);
                    return;
                case "sling":
                    p.Bar(ClothDark, 0.3f, 0.9f, 1.0f, 0.15f, 0.12f);
                    p.Bar(ClothDark, 1.0f, 0.15f, 1.7f, 0.9f, 0.12f);
                    p.Oct(Cloth, 0.3f, 0.8f, 1.4f, 1.0f, 0.18f);
                    p.Rect(ClothLight, 0.4f, 0.85f, 1.2f, 0.3f);
                    return;
                case "alpha":
                    p.Oct(Black, 0.2f, 0.4f, 1.6f, 1.3f, 0.12f);
                    p.Oct(BlackLight, 0.28f, 0.48f, 1.44f, 1.1f, 0.1f);
                    p.Rect(Metal, 0.7f, 0.2f, 0.6f, 0.12f);
                    p.Rect(Metal, 0.66f, 0.2f, 0.08f, 0.22f);
                    p.Rect(Metal, 1.26f, 0.2f, 0.08f, 0.22f);
                    p.Rect(MetalLight, 0.9f, 0.9f, 0.2f, 0.14f);
                    return;
                case "ammo_case":
                    p.Oct(ClothDark, 0.2f, 0.45f, 1.6f, 1.3f, 0.08f);
                    p.Oct(Cloth, 0.2f, 0.45f, 1.6f, 1.15f, 0.08f);
                    p.Rect(ClothLight, 0.2f, 0.7f, 1.6f, 0.1f);
                    p.Rect(Black, 0.75f, 0.3f, 0.5f, 0.1f);
                    p.Rect(BrassLight, 0.3f, 1.0f, 0.5f, 0.14f);
                    return;
                case "magazine_7":
                    p.Poly(MetalDark, 0.38f, 0.1f, 0.62f, 0.1f, 0.66f, 0.9f, 0.34f, 0.9f);
                    p.Poly(Metal, 0.42f, 0.1f, 0.58f, 0.1f, 0.6f, 0.86f, 0.42f, 0.86f);
                    p.Poly(BrassLight, 0.43f, 0.04f, 0.58f, 0.04f, 0.58f, 0.12f, 0.43f, 0.12f);
                    p.Rect(Black, 0.32f, 0.86f, 0.36f, 0.08f);
                    return;
                case "barrel_standard":
                case "barrel_threaded":
                    p.Rect(MetalDark, 0.2f, 0.36f, 1.5f, 0.26f);
                    p.Rect(MetalLight, 0.2f, 0.38f, 1.5f, 0.08f);
                    p.Oct(MetalDark, 0.12f, 0.3f, 0.34f, 0.4f, 0.06f);
                    if (def.Id == "barrel_threaded")
                        for (int i = 0; i < 5; i++) p.Rect(MetalLight, 1.7f + i * 0.04f, 0.36f, 0.02f, 0.26f);
                    return;
                case "grips_wood":
                    p.Oct(Wood, 0.3f, 0.12f, 0.4f, 0.76f, 0.08f);
                    p.Oct(WoodLight, 0.33f, 0.14f, 0.26f, 0.7f, 0.06f);
                    p.Oct(BrassLight, 0.44f, 0.24f, 0.1f, 0.1f, 0.03f);
                    p.Oct(BrassLight, 0.44f, 0.66f, 0.1f, 0.1f, 0.03f);
                    return;
                case "suppressor_45":
                    p.Oct(Black, 0.1f, 0.32f, 1.8f, 0.36f, 0.08f);
                    p.Oct(BlackLight, 0.14f, 0.34f, 1.72f, 0.14f, 0.05f);
                    p.Rect(MetalDark, 0.1f, 0.38f, 0.18f, 0.24f);
                    return;
                case "m1911a1":
                    p.Poly(MetalDark, 0.1f, 0.22f, 1.9f, 0.22f, 1.9f, 0.46f, 0.1f, 0.46f);
                    p.Poly(Metal, 0.12f, 0.24f, 1.88f, 0.24f, 1.88f, 0.3f, 0.12f, 0.3f);
                    p.Poly(MetalDark, 0.2f, 0.46f, 0.66f, 0.46f, 0.58f, 0.92f, 0.3f, 0.92f, 0.14f, 0.56f);
                    p.Poly(Wood, 0.26f, 0.5f, 0.56f, 0.5f, 0.5f, 0.86f, 0.34f, 0.86f);
                    p.Poly(MetalDark, 0.66f, 0.46f, 0.96f, 0.46f, 0.9f, 0.62f, 0.7f, 0.62f);
                    return;
            }
            // Generic crate by category.
            p.Oct(MetalDark, 0.15f, 0.2f, p.W - 0.3f, p.H - 0.4f, 0.08f);
            p.Oct(Metal, 0.15f, 0.2f, p.W - 0.3f, (p.H - 0.4f) * 0.8f, 0.08f);
        }

        private static void Bullets(Pic p, UiColor tip)
        {
            for (int i = 0; i < 3; i++)
            {
                float x = 0.2f + i * 0.21f;
                p.Rect(Brass, x, 0.42f, 0.16f, 0.4f);
                p.Rect(BrassLight, x + 0.02f, 0.42f, 0.05f, 0.4f);
                p.Poly(tip, x, 0.42f, x + 0.16f, 0.42f, x + 0.16f, 0.3f, x + 0.11f, 0.2f, x + 0.05f, 0.2f, x, 0.3f);
            }
        }

        private static void Nut(Pic p, float cx, float cy, float r)
        {
            var pts = new float[12];
            for (int i = 0; i < 6; i++)
            {
                double a = Math.PI / 3 * i + Math.PI / 6;
                pts[i * 2] = cx + (float)Math.Cos(a) * r;
                pts[i * 2 + 1] = cy + (float)Math.Sin(a) * r;
            }
            p.Poly(Metal, pts);
            p.Oct(MetalDark, cx - r * 0.4f, cy - r * 0.4f, r * 0.8f, r * 0.8f, r * 0.2f);
        }
    }
}
