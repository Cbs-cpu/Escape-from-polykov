using System.Collections.Generic;
using UnityEngine;

namespace Polykov.Weapons
{
    /// <summary>
    /// Presentation of a weapon whose model already contains every modding variant in place (AK74N.fbx, see
    /// ArtSource/Tools/build_ak74n_variants.py): children named "<prefix><attachmentId>" are shown when the build
    /// mounts that id. Muzzle devices are authored on the standard barrel's tip and are shifted to the tip of the
    /// mounted barrel ("Socket_Tip_<barrelId>"); the effective muzzle is the device's own tip socket.
    /// Pure view: no gameplay state, rebuilt only when the build changes.
    /// </summary>
    public sealed class ModularWeaponView : MonoBehaviour
    {
        [SerializeField] private string prefix = "AK74N_";
        [SerializeField] private string referenceBarrel = "ak_barrel_standard";

        private readonly Dictionary<string, Transform> _parts = new Dictionary<string, Transform>();
        private readonly Dictionary<string, Vector3> _rest = new Dictionary<string, Vector3>();
        private readonly Dictionary<AttachmentSlot, Transform> _mounted = new Dictionary<AttachmentSlot, Transform>();
        private Transform _baseMuzzle;

        /// <summary>Where shots leave the gun with the current build (muzzle device tip, else barrel tip).</summary>
        public Transform EffectiveMuzzle { get; private set; }

        /// <summary>Instantiates <paramref name="prefab"/> under <paramref name="parent"/> (weapon space: +Z muzzle, grip at the origin).</summary>
        public static ModularWeaponView Create(GameObject prefab, Transform parent, int layer, Material material = null)
        {
            if (prefab == null) return null;
            GameObject go = Instantiate(prefab, parent, false);
            go.name = prefab.name;
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
            if (material != null)
                foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] slots = r.sharedMaterials;
                    for (int i = 0; i < slots.Length; i++) slots[i] = material;
                    r.sharedMaterials = slots;
                }
            var view = go.AddComponent<ModularWeaponView>();
            view.Initialize();
            return view;
        }

        public void Initialize()
        {
            _parts.Clear();
            _rest.Clear();
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith(prefix)) continue;
                string id = t.name.Substring(prefix.Length);
                _parts[id] = t;
                _rest[id] = t.localPosition;
            }
            _baseMuzzle = Find(transform, "Socket_Muzzle");
        }

        public bool HasPart(string id) => id != null && _parts.ContainsKey(id);

        /// <summary>Shows exactly the parts of <paramref name="build"/>; a child's slot comes from its rules in <paramref name="catalog"/>.</summary>
        public void Apply(WeaponBuild build, AttachmentCatalog catalog)
        {
            if (_parts.Count == 0) Initialize();
            AttachmentSlot? slotOf(string id) => catalog != null && catalog.TryGet(id, out AttachmentRules r) ? r.Slot : (AttachmentSlot?)null;
            _mounted.Clear();
            foreach (KeyValuePair<string, Transform> kv in _parts)
            {
                AttachmentSlot? slot = slotOf(kv.Key);
                if (slot == null) continue; // fixed part (receiver, gas block...)
                bool on = build.Get(slot.Value) == kv.Key;
                kv.Value.gameObject.SetActive(on);
                if (on) _mounted[slot.Value] = kv.Value;
            }

            Vector3 shift = Vector3.zero;
            Transform barrelTip = null;
            if (_mounted.TryGetValue(AttachmentSlot.Barrel, out Transform barrel))
            {
                barrelTip = Find(barrel, "Socket_Tip_");
                Transform refTip = _parts.TryGetValue(referenceBarrel, out Transform rb) ? Find(rb, "Socket_Tip_") : null;
                if (barrelTip != null && refTip != null)
                    shift = transform.InverseTransformPoint(barrelTip.position) - transform.InverseTransformPoint(refTip.position)
                        - (transform.InverseTransformPoint(barrel.position) - transform.InverseTransformPoint(rb.position));
            }
            EffectiveMuzzle = barrelTip != null ? barrelTip : _baseMuzzle;
            foreach (KeyValuePair<string, Transform> kv in _parts)
            {
                if (slotOf(kv.Key) != AttachmentSlot.Muzzle) continue;
                Transform t = kv.Value;
                t.localPosition = _rest[kv.Key] + (t.parent != null
                    ? t.parent.InverseTransformVector(transform.TransformVector(shift))
                    : shift);
                if (t.gameObject.activeSelf)
                {
                    Transform tip = Find(t, "Socket_Tip_");
                    if (tip != null) EffectiveMuzzle = tip;
                }
            }
        }

        /// <summary>The visible part in <paramref name="slot"/> (null if nothing is mounted there).</summary>
        public Transform Mounted(AttachmentSlot slot) => _mounted.TryGetValue(slot, out Transform t) ? t : null;

        /// <summary>World point of a slot's mounted part (armorer connection lines).</summary>
        public Vector3 AnchorFor(AttachmentSlot slot)
        {
            if (!_mounted.TryGetValue(slot, out Transform part)) return transform.position;
            Renderer r = part.GetComponent<Renderer>();
            return r != null ? r.bounds.center : part.position;
        }

        private static Transform Find(Transform root, string namePrefix)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith(namePrefix)) return t;
            return null;
        }
    }
}
