using Polykov.Player;
using UnityEngine;

namespace Polykov.Weapons
{
    /// <summary>
    /// Particle and debris side of shooting, driven by <see cref="PlayerWeapon"/> events: muzzle flash, impacts per
    /// surface (<see cref="SurfaceMaterial"/>), bullet holes, ejected casings and the magazine dropped on reloads.
    /// Everything is pooled (no Instantiate while shooting). Local presentation only.
    /// </summary>
    [DefaultExecutionOrder(120)]
    public sealed class WeaponEffects : MonoBehaviour
    {
        [SerializeField] private PlayerWeapon weapon;
        [SerializeField] private PlayerMotor motor;
        [Tooltip("Effect prefabs and ejection settings (VD_M1911).")]
        [SerializeField] private WeaponViewData view;
        [SerializeField] private GameObject casingPrefab;
        [SerializeField] private GameObject droppedMagazinePrefab;
        [SerializeField, Range(4, 64)] private int casingPoolSize = 24;
        [SerializeField, Range(1, 8)] private int magazinePoolSize = 4;

        private EffectPool _effects;
        private BloodEffects _blood;
        private DebrisPool _casings;
        private DebrisPool _magazines;
        private bool _magazineDropped;

        private void Awake()
        {
            _effects = new GameObject("WeaponEffectPool").AddComponent<EffectPool>();
            if (view != null)
            {
                _effects.Prewarm(view.MuzzleFlash);
                _effects.Prewarm(view.ImpactConcrete);
                _effects.Prewarm(view.ImpactMetal);
                _effects.Prewarm(view.BulletHole);
                _blood = new BloodEffects(_effects, view, weapon.HitMask, _effects.transform);
            }
            if (casingPrefab != null)
            {
                _casings = new GameObject("CasingPool").AddComponent<DebrisPool>();
                _casings.Initialize(casingPrefab, casingPoolSize, 10f);
            }
            if (droppedMagazinePrefab != null)
            {
                _magazines = new GameObject("DroppedMagazinePool").AddComponent<DebrisPool>();
                _magazines.Initialize(droppedMagazinePrefab, magazinePoolSize, 15f);
            }
        }

        private void OnEnable()
        {
            weapon.Fired += OnFired;
            weapon.HitSurface += OnHitSurface;
            weapon.ReloadStarted += OnReloadStarted;
            weapon.SimpleImpactMarkers = view == null;
        }

        private void OnDisable()
        {
            weapon.Fired -= OnFired;
            weapon.HitSurface -= OnHitSurface;
            weapon.ReloadStarted -= OnReloadStarted;
            weapon.SimpleImpactMarkers = true;
        }

        private void OnDestroy()
        {
            if (_effects != null) Destroy(_effects.gameObject);
            if (_casings != null) Destroy(_casings.gameObject);
            if (_magazines != null) Destroy(_magazines.gameObject);
        }

        private void OnFired(WeaponState state)
        {
            WeaponModel model = weapon.Model;
            Transform root = model.transform;
            Transform muzzle = weapon.MuzzlePoint;
            if (view != null && view.MuzzleFlash != null && muzzle != null)
            {
                PooledEffect flash = _effects.Spawn(view.MuzzleFlash, muzzle.position, root.rotation, root);
                if (flash != null) ConfigureFlash(flash, weapon.Effective.MuzzleFlash);
            }

            if (_casings != null)
            {
                Transform port = model.EjectionPort != null ? model.EjectionPort : model.Slide;
                if (port == null) return;
                Vector3 local = view != null ? view.EjectVelocity : new Vector3(1.9f, 1.6f, -0.35f);
                float jitter = view != null ? view.EjectRandomness : 0.35f;
                local += new Vector3(Random.Range(-jitter, jitter), Random.Range(-jitter, jitter), Random.Range(-jitter, jitter));
                Vector3 velocity = root.TransformDirection(local) + motor.State.Velocity;
                float spin = view != null ? view.EjectSpin : 30f;
                _casings.Launch(port.position, root.rotation * Quaternion.Euler(0f, 90f, 0f), velocity,
                    Random.insideUnitSphere * spin);
            }
        }

        /// <summary>
        /// Full flash at 1; below that only a small smoke puff survives (petals/sparks off, root flash hidden, scaled down).
        /// </summary>
        private static void ConfigureFlash(PooledEffect flash, float amount)
        {
            bool reduced = amount < 0.999f;
            Transform root = flash.transform;
            root.localScale = Vector3.one * (reduced ? Mathf.Lerp(0.3f, 1f, amount) : 1f);
            var rootRenderer = flash.GetComponent<ParticleSystemRenderer>();
            if (rootRenderer != null) rootRenderer.enabled = !reduced;
            bool restarted = false;
            foreach (ParticleSystem ps in flash.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (ps.transform == root) continue;
                bool keep = !reduced || ps.name.Contains("Smoke");
                if (ps.gameObject.activeSelf != keep)
                {
                    ps.gameObject.SetActive(keep);
                    restarted |= keep;
                }
            }
            if (restarted) flash.Play();
        }

        private void OnHitSurface(RaycastHit hit, Vector3 direction)
        {
            if (view == null) return;
            var surface = hit.collider.GetComponentInParent<SurfaceMaterial>();
            SurfaceType type = surface != null ? surface.Type : SurfaceType.Concrete;

            if (type == SurfaceType.Flesh) _blood?.Hit(hit, direction, weapon.Definition.Damage);

            GameObject impact = type == SurfaceType.Metal ? view.ImpactMetal : view.ImpactConcrete;
            if (type != SurfaceType.Flesh && impact != null)
                _effects.Spawn(impact, hit.point + hit.normal * 0.005f, Quaternion.LookRotation(hit.normal));

            // Holes only on static geometry: moving bodies would leave them floating.
            if (type != SurfaceType.Flesh && view.BulletHole != null && hit.rigidbody == null)
            {
                Quaternion rotation = Quaternion.LookRotation(-hit.normal) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
                PooledEffect hole = _effects.Spawn(view.BulletHole, hit.point + hit.normal * 0.002f, rotation);
                if (hole != null) hole.transform.localScale = Vector3.one * view.BulletHoleSize;
            }
        }

        private void OnReloadStarted() => _magazineDropped = false;

        private void Update()
        {
            // Drop the old magazine when the presenter pulls it out (same point of the reload timeline).
            WeaponState state = weapon.State;
            if (!state.IsReloading || _magazineDropped || _magazines == null) return;
            float t = state.ReloadElapsed / WeaponMotor.ReloadDuration(state, weapon.Definition.Stats);
            if (t < 0.3f) return;
            _magazineDropped = true;
            WeaponModel model = weapon.Model;
            if (model.Magazine == null) return;
            Vector3 down = model.transform.TransformDirection(Quaternion.Euler(WeaponModel.GripRake, 0f, 0f) * Vector3.down);
            _magazines.Launch(model.Magazine.position, model.Magazine.rotation, down * 1.2f + motor.State.Velocity,
                Random.insideUnitSphere * 3f);
        }
    }
}
