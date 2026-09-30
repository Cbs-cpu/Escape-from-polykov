using UnityEngine;

namespace Polykov.Raid
{
    /// <summary>Cheap fire / faulty-lamp flicker (LOCAL ONLY). Smooth value noise on intensity, optional hard dropouts.</summary>
    [RequireComponent(typeof(Light))]
    public sealed class LightFlicker : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float speed = 7f;
        [SerializeField, Range(0f, 1f)] private float amount = 0.35f;
        [Tooltip("Chance per second of a short blackout (faulty fluorescent tube). 0 = fire.")]
        [SerializeField, Min(0f)] private float dropoutsPerSecond;

        private Light _light;
        private float _base, _seed, _dropUntil;

        private void Awake()
        {
            _light = GetComponent<Light>();
            _base = _light.intensity;
            _seed = Random.value * 100f;
        }

        private void Update()
        {
            float n = Mathf.PerlinNoise(_seed, Time.time * speed) * 2f - 1f;
            float k = 1f + n * amount;
            if (dropoutsPerSecond > 0f)
            {
                if (Time.time > _dropUntil && Random.value < dropoutsPerSecond * Time.deltaTime)
                    _dropUntil = Time.time + Random.Range(0.05f, 0.25f);
                if (Time.time < _dropUntil) k = 0.05f;
            }
            _light.intensity = _base * k;
        }
    }
}
