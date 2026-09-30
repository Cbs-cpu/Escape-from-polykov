using UnityEngine;

namespace Polykov.Weapons
{
    /// <summary>
    /// Hanging steel plate for the test range: swings from hits (its Rigidbody gets the hit impulse from the
    /// shooter), flashes briefly and counts hits. Metal surface for spark impacts.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class SteelTarget : MonoBehaviour, IDamageable
    {
        [SerializeField] private Renderer plate;
        [SerializeField] private Color flashColor = new Color(1f, 0.55f, 0.2f);
        [SerializeField] private float flashTime = 0.12f;

        private MaterialPropertyBlock _block;
        private Color _baseColor;
        private float _flash;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public int Hits { get; private set; }

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            if (plate != null) _baseColor = plate.sharedMaterial.GetColor(BaseColorId);
        }

        public void ApplyDamage(in DamageInfo info)
        {
            Hits++;
            _flash = flashTime;
        }

        private void Update()
        {
            if (plate == null || _flash <= 0f) return;
            _flash -= Time.deltaTime;
            float t = Mathf.Clamp01(_flash / flashTime);
            plate.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, Color.Lerp(_baseColor, flashColor, t * t));
            plate.SetPropertyBlock(_block);
        }
    }
}
