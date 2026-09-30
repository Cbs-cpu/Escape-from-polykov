using Polykov.Core;
using UnityEngine;

namespace Polykov.Weapons
{
    /// <summary>Shared unit quad (front face towards -Z, like Unity's Quad) for decals.</summary>
    internal static class DecalMesh
    {
        private static Mesh _quad;

        public static Mesh Quad
        {
            get
            {
                if (_quad != null) return _quad;
                _quad = new Mesh { name = "DecalQuad" };
                _quad.vertices = new[]
                {
                    new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                    new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f),
                };
                _quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
                _quad.normals = new[] { -Vector3.forward, -Vector3.forward, -Vector3.forward, -Vector3.forward };
                _quad.triangles = new[] { 0, 2, 1, 2, 3, 1 };
                _quad.RecalculateBounds();
                return _quad;
            }
        }

        public static Transform Create(string name, Material material, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = Quad;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            go.SetActive(false);
            return go.transform;
        }
    }

    /// <summary>
    /// Blood on flesh hits (local presentation): entry spray, exit spray on the far side when the bullet
    /// would pass through, fine mist, a wound decal on the body and pooled splats on nearby surfaces.
    /// Amount follows <see cref="UserSettings.Gore"/>. Pooled: no allocations after warm-up.
    /// </summary>
    public sealed class BloodEffects
    {
        private const int SplatPoolSize = 40;

        private readonly EffectPool _effects;
        private readonly WeaponViewData _view;
        private readonly LayerMask _mask;
        private readonly Transform _root;
        private Transform[] _splats;
        private int _nextSplat;

        public BloodEffects(EffectPool effects, WeaponViewData view, LayerMask mask, Transform poolParent)
        {
            _effects = effects;
            _view = view;
            _mask = mask;
            _root = poolParent;
            if (view == null) return;
            _effects.Prewarm(view.BloodEntry);
            _effects.Prewarm(view.BloodExit);
            _effects.Prewarm(view.BloodMist);
        }

        public void Hit(RaycastHit hit, Vector3 direction, float damage)
        {
            GoreLevel gore = UserSettings.Gore;
            if (gore == GoreLevel.Off || _view == null) return;
            bool full = gore == GoreLevel.Full;
            float scale = full ? 1f : 0.55f;

            // Entry: mostly back towards the shooter, bent by the surface normal.
            Vector3 back = Vector3.Slerp(-direction, hit.normal, 0.4f);
            Spawn(_view.BloodEntry, hit.point, back, scale);
            if (full) Spawn(_view.BloodMist, hit.point, hit.normal, 1f);

            var decals = hit.collider.GetComponentInParent<WoundDecals>();
            if (decals != null && _view.WoundMaterial != null)
                decals.Add(hit.point, hit.normal, hit.collider.transform, _view.WoundMaterial,
                    _view.WoundSize * (full ? 1f : 0.7f));

            // Exit: the bullet leaves through the far side when the body there is thin enough.
            Vector3 splatFrom = hit.point;
            float thickness = _view.MaxExitThickness;
            if (Physics.Raycast(hit.point + direction * thickness, -direction, out RaycastHit exit, thickness, _mask,
                    QueryTriggerInteraction.Ignore) &&
                exit.collider.GetComponentInParent<SurfaceMaterial>() is SurfaceMaterial mat && mat.Type == SurfaceType.Flesh &&
                exit.collider.transform.root == hit.collider.transform.root)
            {
                splatFrom = exit.point;
                if (full)
                {
                    Spawn(_view.BloodExit, exit.point, direction, 1f);
                    if (decals != null && _view.WoundMaterial != null)
                        decals.Add(exit.point, exit.normal, exit.collider.transform, _view.WoundMaterial, _view.WoundSize * 1.5f);
                }
            }

            // Spatter on whatever is behind the body (walls, floor).
            if (Physics.Raycast(splatFrom + direction * 0.05f, direction, out RaycastHit wall, _view.SplatRange, _mask,
                    QueryTriggerInteraction.Ignore) && wall.collider.GetComponentInParent<SurfaceMaterial>() is not { Type: SurfaceType.Flesh })
                Splat(wall.point, wall.normal, (full ? 1f : 0.6f) * Random.Range(0.8f, 1.5f));

            // A few drops on the ground under the wound.
            if ((full || Random.value < 0.5f) && Physics.Raycast(hit.point, Vector3.down, out RaycastHit floor, 3f, _mask,
                    QueryTriggerInteraction.Ignore) && floor.collider.GetComponentInParent<SurfaceMaterial>() is not { Type: SurfaceType.Flesh })
                Splat(floor.point, floor.normal, Random.Range(0.5f, 1f) * (full ? 1f : 0.6f));
        }

        /// <summary>A burst of blood from an open wound (severed limb stump): spray along <paramref name="direction"/>.</summary>
        public void Spurt(Vector3 point, Vector3 direction, float amount = 1f)
        {
            GoreLevel gore = UserSettings.Gore;
            if (gore == GoreLevel.Off || _view == null || direction.sqrMagnitude < 1e-6f) return;
            float scale = (gore == GoreLevel.Full ? 1.6f : 0.9f) * amount;
            Spawn(_view.BloodExit, point, direction, scale);
            if (gore == GoreLevel.Full) Spawn(_view.BloodMist, point, direction, 1.4f);
            if (Physics.Raycast(point, Vector3.down, out RaycastHit floor, 3f, _mask, QueryTriggerInteraction.Ignore) &&
                floor.collider.GetComponentInParent<SurfaceMaterial>() is not { Type: SurfaceType.Flesh })
                Splat(floor.point, floor.normal, Random.Range(1f, 1.8f) * amount);
        }

        private void Spawn(GameObject prefab, Vector3 point, Vector3 direction, float scale)
        {
            if (prefab == null || direction.sqrMagnitude < 1e-6f) return;
            PooledEffect fx = _effects.Spawn(prefab, point, Quaternion.LookRotation(direction));
            if (fx != null) fx.transform.localScale = Vector3.one * scale;
        }

        private void Splat(Vector3 point, Vector3 normal, float sizeScale)
        {
            if (_view.SplatMaterial == null) return;
            if (_splats == null || _splats[0] == null)
            {
                _splats = new Transform[SplatPoolSize];
                for (int i = 0; i < SplatPoolSize; i++) _splats[i] = DecalMesh.Create("BloodSplat", _view.SplatMaterial, _root);
                _nextSplat = 0;
            }
            Transform t = _splats[_nextSplat];
            _nextSplat = (_nextSplat + 1) % SplatPoolSize;
            Quaternion rotation = Quaternion.LookRotation(-normal) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            t.SetPositionAndRotation(point + normal * 0.004f, rotation);
            t.localScale = Vector3.one * (_view.SplatSize * sizeScale);
            t.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// Pooled wound decals stuck to a character's bones (moving with the animation and the ragdoll).
    /// Capped per character: the oldest one is reused.
    /// </summary>
    public sealed class WoundDecals : MonoBehaviour
    {
        public const int Capacity = 12;

        private Transform[] _decals;
        private int _next;

        public void Add(Vector3 point, Vector3 normal, Transform bone, Material material, float size)
        {
            if (_decals == null) _decals = new Transform[Capacity];
            Transform t = _decals[_next];
            if (t == null)
            {
                t = DecalMesh.Create("Wound", material, transform);
                _decals[_next] = t;
            }
            _next = (_next + 1) % Capacity;
            t.SetParent(bone, false);
            t.SetPositionAndRotation(point + normal * 0.004f,
                Quaternion.LookRotation(-normal) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)));
            Vector3 parentScale = bone.lossyScale;
            float avg = Mathf.Max(1e-4f, (Mathf.Abs(parentScale.x) + Mathf.Abs(parentScale.y) + Mathf.Abs(parentScale.z)) / 3f);
            t.localScale = Vector3.one * (size / avg);
            t.gameObject.SetActive(true);
        }

        public void Clear()
        {
            if (_decals == null) return;
            for (int i = 0; i < _decals.Length; i++)
            {
                if (_decals[i] == null) continue;
                _decals[i].SetParent(transform, false);
                _decals[i].gameObject.SetActive(false);
            }
            _next = 0;
        }

        public int ActiveCount
        {
            get
            {
                int n = 0;
                if (_decals != null)
                    for (int i = 0; i < _decals.Length; i++)
                        if (_decals[i] != null && _decals[i].gameObject.activeSelf) n++;
                return n;
            }
        }
    }
}
