using UnityEngine;

namespace Polykov.Weapons
{
    /// <summary>Pooled bullet-hole markers (placeholder until VFX/decals). Zero allocations after warm-up.</summary>
    public static class ImpactEffects
    {
        private const int PoolSize = 64;

        private static Transform[] _pool;
        private static int _next;

        public static Material Material { get; set; }

        public static void Spawn(Vector3 point, Vector3 normal)
        {
            if (_pool == null || _pool[0] == null) CreatePool();
            Transform t = _pool[_next];
            _next = (_next + 1) % PoolSize;
            t.SetPositionAndRotation(point + normal * 0.002f, Quaternion.LookRotation(normal));
            t.gameObject.SetActive(true);
        }

        private static void CreatePool()
        {
            var root = new GameObject("ImpactMarkers").transform;
            Mesh cube = PrimitiveMeshes.Cube;
            _pool = new Transform[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject("Impact");
                go.transform.SetParent(root, false);
                go.transform.localScale = new Vector3(0.022f, 0.022f, 0.003f);
                go.AddComponent<MeshFilter>().sharedMesh = cube;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = Material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                go.SetActive(false);
                _pool[i] = go.transform;
            }
            _next = 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _pool = null;
            _next = 0;
        }
    }
}
