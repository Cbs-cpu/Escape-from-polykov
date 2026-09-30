using Polykov.Inventory;
using Polykov.UI.Framework;
using UnityEngine;

namespace Polykov.Lobby
{
    /// <summary>Icons from the baked PNGs in Resources/Icons/Items, falling back to pictograms. No scene references needed.</summary>
    public sealed class BakedItemIcons : IItemIcons
    {
        private readonly System.Collections.Generic.Dictionary<string, UiImage> _cache = new System.Collections.Generic.Dictionary<string, UiImage>();

        public UiImage Get(Item item, bool rotated)
        {
            ItemDef def = item.Def;
            string key = def.Id + (rotated ? "|r" : "");
            if (_cache.TryGetValue(key, out UiImage img)) return img;
            img = Baked(def, rotated) ?? ItemPictograms.Get(def, rotated);
            _cache[key] = img;
            return img;
        }

        /// <summary>
        /// Icon rendered offline in Blender (ArtSource/Tools/render_item_icons.py -> Resources/Icons/Items/&lt;id&gt;.png),
        /// aspect-fitted into the item's footprint and turned 90 degrees when the item is rotated. Null if missing.
        /// </summary>
        public static UiImage Baked(ItemDef def, bool rotated)
        {
            var src = Resources.Load<Texture2D>("Icons/Items/" + def.Id);
            if (src == null) return null;
            int pxW = def.Width * ItemPictograms.PixelsPerCell * 2, pxH = def.Height * ItemPictograms.PixelsPerCell * 2;
            int w = rotated ? pxH : pxW, h = rotated ? pxW : pxH;
            // Fit the source (in the unrotated footprint), with a small margin.
            float scale = Mathf.Min(pxW * 0.92f / src.width, pxH * 0.92f / src.height);
            float dw = src.width * scale, dh = src.height * scale;
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(true, true, new Color(0f, 0f, 0f, 0f));
            GL.PushMatrix();
            GL.LoadPixelMatrix(0, w, h, 0);
            if (rotated) GL.MultMatrix(Matrix4x4.TRS(new Vector3(w, 0f, 0f), Quaternion.Euler(0f, 0f, 90f), Vector3.one));
            Graphics.DrawTexture(new Rect((pxW - dw) * 0.5f, (pxH - dh) * 0.5f, dw, dh), src);
            GL.PopMatrix();
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "ItemIcon", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply(true, true);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            return new UiImage(tex, w, h);
        }
    }
}
