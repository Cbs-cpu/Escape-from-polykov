using System.Collections.Generic;
using UnityEngine;

namespace Polykov.Weapons
{
    /// <summary>
    /// Pools particle-effect prefabs (no Instantiate/Destroy while shooting). Effects must have their root
    /// ParticleSystem stop action set to Disable; <see cref="PooledEffect"/> returns them on disable.
    /// </summary>
    public sealed class EffectPool : MonoBehaviour
    {
        [SerializeField] private int prewarmPerPrefab = 6;

        private readonly Dictionary<GameObject, Stack<PooledEffect>> _free = new Dictionary<GameObject, Stack<PooledEffect>>();

        // Pools may live inside the player prefab; detach so world-space effects don't follow the player.
        private void Awake() => transform.SetParent(null, true);

        public void Prewarm(GameObject prefab)
        {
            if (prefab == null) return;
            Stack<PooledEffect> stack = GetStack(prefab);
            for (int i = stack.Count; i < prewarmPerPrefab; i++) stack.Push(Create(prefab));
        }

        public PooledEffect Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (prefab == null) return null;
            Stack<PooledEffect> stack = GetStack(prefab);
            PooledEffect fx = stack.Count > 0 ? stack.Pop() : Create(prefab);
            Transform t = fx.transform;
            t.SetParent(parent != null ? parent : transform, false);
            t.SetPositionAndRotation(position, rotation);
            fx.gameObject.SetActive(true);
            fx.Play();
            return fx;
        }

        internal void Return(PooledEffect fx)
        {
            // Re-parenting is not allowed while the object is being deactivated; Spawn re-parents on reuse.
            if (fx.Prefab == null) return;
            GetStack(fx.Prefab).Push(fx);
        }

        private Stack<PooledEffect> GetStack(GameObject prefab)
        {
            if (!_free.TryGetValue(prefab, out Stack<PooledEffect> stack))
            {
                stack = new Stack<PooledEffect>(prewarmPerPrefab);
                _free.Add(prefab, stack);
            }
            return stack;
        }

        private PooledEffect Create(GameObject prefab)
        {
            GameObject go = Instantiate(prefab, transform);
            go.SetActive(false);
            if (!go.TryGetComponent(out PooledEffect fx)) fx = go.AddComponent<PooledEffect>();
            fx.Init(this, prefab);
            return fx;
        }
    }
}
