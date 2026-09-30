using System.Collections.Generic;
using Polykov.Inventory;
using Polykov.UI.Framework;
using Polykov.Weapons;
using UnityEngine;

namespace Polykov.Lobby
{
    /// <summary>
    /// Item icons rendered at runtime from the game's own models (Tarkov-style side view with the black outline):
    /// the pistol with its current build and every attachment that has a prefab. Items without a model use
    /// <see cref="ItemPictograms"/>. Rendered once per (item type, build, rotation) and cached.
    /// </summary>
    public sealed class UnityItemIcons : IItemIcons
    {
        private const int IconLayer = 31;
        private static readonly Vector3 StudioOrigin = new Vector3(0f, -500f, 0f);

        private readonly WeaponDefinition _weapon;
        private readonly GameObject _weaponPrefab;
        private readonly Material _weaponMaterial, _outline;
        private readonly Dictionary<string, UiImage> _cache = new Dictionary<string, UiImage>();
        private Camera _camera;
        private Light _light;

        private readonly GameObject _akPrefab;
        private readonly Material _akMaterial;

        public UnityItemIcons(WeaponDefinition weapon, GameObject weaponPrefab, Material weaponMaterial, Material outline,
            GameObject akPrefab = null, Material akMaterial = null)
        {
            _akPrefab = akPrefab;
            _akMaterial = akMaterial;
            _weapon = weapon;
            _weaponPrefab = weaponPrefab;
            _weaponMaterial = weaponMaterial;
            _outline = outline;
        }

        public UiImage Get(Item item, bool rotated)
        {
            ItemDef def = item.Def;
            string key = def.Id + "|" + (def.Category == ItemCategory.Weapon ? item.Build : "") + (rotated ? "|r" : "");
            if (_cache.TryGetValue(key, out UiImage img)) return img;
            img = Render(item, rotated) ?? BakedItemIcons.Baked(def, rotated) ?? ItemPictograms.Get(def, rotated);
            _cache[key] = img;
            return img;
        }

        private UiImage Render(Item item, bool rotated)
        {
            GameObject subject = null;
            try
            {
                subject = BuildSubject(item);
                if (subject == null) return null;
                return Shoot(subject, item.Def.Width, item.Def.Height, rotated);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Icon render failed for " + item.Def.Id + ": " + e.Message);
                return null;
            }
            finally
            {
                if (subject != null) Object.DestroyImmediate(subject);
            }
        }

        /// <summary>The model to photograph, in weapon space (+z = muzzle, +y = up), or null if the item has none.</summary>
        private GameObject BuildSubject(Item item)
        {
            if (item.Def.Category == ItemCategory.Weapon && item.Def.WeaponId == "ak74n" && _akPrefab != null)
            {
                var holder = new GameObject("IconSubject");
                ModularWeaponView view = ModularWeaponView.Create(_akPrefab, holder.transform, IconLayer, _akMaterial);
                WeaponFamily family = WeaponFamilies.ById("ak74n");
                view.Apply(WeaponBuild.ParseOr(item.Build, family.FactoryBuild), family.Catalog);
                return holder;
            }
            if (item.Def.Category == ItemCategory.Weapon)
            {
                // Only the weapon family the host has a model for gets a rendered icon; the others use pictograms.
                if (_weaponPrefab == null || (item.Def.WeaponId != null && item.Def.WeaponId != _weapon.FamilyId)) return null;
                var holder = new GameObject("IconSubject");
                WeaponModel model = WeaponModel.CreateFromModel(_weaponPrefab, holder.transform, IconLayer, default, _weaponMaterial, _outline);
                if (model == null) { Object.DestroyImmediate(holder); return null; }
                WeaponBuild build = WeaponBuild.ParseOr(item.Build, _weapon.DefaultBuild);
                var parts = new List<AttachmentDefinition>();
                foreach (AttachmentSlot slot in new[] { AttachmentSlot.Barrel, AttachmentSlot.Muzzle, AttachmentSlot.Grips, AttachmentSlot.Magazine })
                {
                    AttachmentDefinition a = Attachment(build.Get(slot));
                    if (a != null) parts.Add(a);
                }
                model.MountAttachments(parts, IconLayer, _outline);
                return holder;
            }
            // Only muzzle devices are whole objects; barrel/grip prefabs are partial pieces meant to sit on the gun.
            AttachmentDefinition part = Attachment(item.Def.AttachmentId);
            if (part == null || part.Prefab == null || part.Slot != AttachmentSlot.Muzzle) return null;
            GameObject go = Object.Instantiate(part.Prefab);
            go.name = "IconSubject";
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                Material[] slots = r.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    bool outline = slots[i] != null && slots[i].name.Contains("Outline");
                    if (outline) { if (_outline != null) slots[i] = _outline; }
                    else if (part.BodyMaterial != null) slots[i] = part.BodyMaterial;
                }
                r.sharedMaterials = slots;
            }
            return go;
        }

        private AttachmentDefinition Attachment(string id)
        {
            if (string.IsNullOrEmpty(id) || _weapon.Attachments == null) return null;
            foreach (AttachmentDefinition a in _weapon.Attachments)
                if (a != null && a.Id == id) return a;
            return null;
        }

        private UiImage Shoot(GameObject subject, int cellsW, int cellsH, bool rotated)
        {
            subject.transform.SetPositionAndRotation(StudioOrigin, Quaternion.identity);
            foreach (Transform t in subject.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = IconLayer;
            Bounds b = default;
            bool first = true;
            foreach (Renderer r in subject.GetComponentsInChildren<Renderer>())
            {
                if (first) { b = r.bounds; first = false; }
                else b.Encapsulate(r.bounds);
            }
            if (first) return null;

            EnsureStudio();
            int pxW = cellsW * ItemPictograms.PixelsPerCell * 2, pxH = cellsH * ItemPictograms.PixelsPerCell * 2;
            // Side view from the weapon's right: screen right = +z (muzzle), up = +y. Rotated icons roll the camera.
            float margin = 1.12f;
            float aspect = (float)pxW / pxH;
            float halfH = Mathf.Max(b.extents.y, b.extents.z / aspect) * margin;
            _camera.orthographicSize = halfH;
            _camera.transform.position = b.center + Vector3.right * (b.extents.x + 2f);
            _camera.transform.rotation = Quaternion.LookRotation(Vector3.left, Vector3.up) * (rotated ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.identity);
            _camera.farClipPlane = b.size.x + 4f;
            _light.transform.rotation = Quaternion.LookRotation(new Vector3(-1f, -0.6f, 0.4f));

            int w = rotated ? pxH : pxW, h = rotated ? pxW : pxH;
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            _camera.targetTexture = rt;
            _camera.aspect = (float)w / h;
            _camera.enabled = true;
            _light.enabled = true;
            _camera.Render();
            _camera.enabled = false;
            _light.enabled = false;

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "ItemIcon", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply(true, true);
            RenderTexture.active = previous;
            _camera.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);
            return new UiImage(tex, w, h);
        }

        private void EnsureStudio()
        {
            if (_camera != null) return;
            var go = new GameObject("ItemIconStudio") { hideFlags = HideFlags.HideAndDontSave };
            _camera = go.AddComponent<Camera>();
            _camera.enabled = false;
            _camera.orthographic = true;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _camera.cullingMask = 1 << IconLayer;
            _camera.nearClipPlane = 0.01f;
            _camera.allowHDR = false;
            _camera.allowMSAA = true;
            var lightGo = new GameObject("ItemIconLight") { hideFlags = HideFlags.HideAndDontSave };
            lightGo.transform.SetParent(go.transform, false);
            _light = lightGo.AddComponent<Light>();
            _light.type = LightType.Directional;
            _light.intensity = 1.6f;
            _light.color = new Color(1f, 0.97f, 0.92f);
            _light.cullingMask = 1 << IconLayer;
            _light.shadows = LightShadows.None;
            _light.enabled = false;
        }

        public void Dispose()
        {
            if (_camera != null) Object.Destroy(_camera.gameObject);
            foreach (UiImage img in _cache.Values)
                if (img.Native is Texture t && t.name == "ItemIcon") Object.Destroy(t);
            _cache.Clear();
        }
    }
}
