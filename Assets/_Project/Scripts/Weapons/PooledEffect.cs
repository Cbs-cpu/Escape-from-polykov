using UnityEngine;

namespace Polykov.Weapons
{
    /// <summary>Returns a pooled effect to its <see cref="EffectPool"/> when the particle system disables it.</summary>
    public sealed class PooledEffect : MonoBehaviour
    {
        private EffectPool _pool;
        private ParticleSystem _particles;
        private bool _returning;

        public GameObject Prefab { get; private set; }

        internal void Init(EffectPool pool, GameObject prefab)
        {
            _pool = pool;
            Prefab = prefab;
            _particles = GetComponent<ParticleSystem>();
        }

        public void Play()
        {
            _returning = false;
            if (_particles == null) return;
            _particles.Clear(true);
            _particles.Play(true);
        }

        private void OnDisable()
        {
            if (_pool == null || _returning) return;
            _returning = true;
            _pool.Return(this);
        }
    }
}
