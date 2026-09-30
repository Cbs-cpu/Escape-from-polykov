using Polykov.Input;
using Polykov.Movement;
using Polykov.Player;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Polykov.Weapons
{
    /// <summary>
    /// Runs the pure <see cref="WeaponMotor"/> on the movement tick, resolves shots with a hitscan from the eye
    /// and probes for walls in front of the muzzle. Owns the weapon state; presentation reads it.
    /// </summary>
    public sealed class PlayerWeapon : MonoBehaviour
    {
        [SerializeField] private WeaponDefinition definition;
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private PlayerInputReader input;
        [Tooltip("The first-person camera: shots and the obstruction probe start at the eye.")]
        [SerializeField] private Transform eye;
        [SerializeField] private LayerMask hitMask = ~(1 << 8);

        /// <summary>Layers bullets can hit (also used by hit effects that probe the scene).</summary>
        public LayerMask HitMask => hitMask;
        [Tooltip("Imported weapon model (FBX from ArtSource/Tools/build_m1911.py). Empty = procedural placeholder.")]
        [SerializeField] private GameObject modelPrefab;
        [Tooltip("Material for every part of the imported model (textured atlas). Empty = remap by material name.")]
        [SerializeField] private Material modelMaterial;
        [Tooltip("Black unlit material for the model's inverted-hull outline (slots named M_Outline).")]
        [SerializeField] private Material outlineMaterial;
        [SerializeField] private Material steelMaterial;
        [SerializeField] private Material gripMaterial;
        [SerializeField] private Material steelDarkMaterial;
        [SerializeField] private Material gripDarkMaterial;
        [SerializeField] private Material brassMaterial;
        [SerializeField] private uint seed = 1;

        private WeaponState _state;
        private WeaponModel _model;
        private bool _obstructed;
        private bool _lastReloadWasEmpty;
        private WeaponAction _lastAction;

        private WeaponBuild _build;
        private EffectiveWeaponStats _effective;
        private WeaponStats _stats;
        private AttachmentCatalog _catalog;
        private float _lengthExtra;
        private Transform _muzzle;

        /// <summary>The mounted build (attachment ids per slot).</summary>
        public WeaponBuild Build => _build;
        /// <summary>Stats with the build's modifiers applied (recoil, ergonomics-driven aim time).</summary>
        public EffectiveWeaponStats Effective => _effective;
        public WeaponStats Stats => _stats;
        /// <summary>Muzzle recoil multiplier of the build (1 = factory).</summary>
        public float RecoilMultiplier { get; private set; } = 1f;
        /// <summary>Flash / shot origin transform: the suppressor's front socket when one is mounted.</summary>
        public Transform MuzzlePoint => _muzzle != null ? _muzzle : _model.Muzzle;
        public bool Suppressed => _effective.MuzzleFlash < 0.999f;
        /// <summary>1 = unsuppressed gunshot. Hook for suppressed audio.</summary>
        public float Loudness => _effective.Loudness;
        /// <summary>Raised after attachments are rebuilt (presentation rebinds flash light / muzzle).</summary>
        public event System.Action<WeaponBuild> BuildChanged;
        /// <summary>Raised on every shot with its loudness (1 = unsuppressed), for audio.</summary>
        public event System.Action<float> ShotHeard;

        public string BuildPrefsKey
        {
            get
            {
                string n = definition.name;
                if (n.StartsWith("WD_")) n = n.Substring(3);
                return "weaponBuild." + n.ToLowerInvariant();
            }
        }

        public WeaponDefinition Definition => definition;
        public WeaponState State => _state;
        public WeaponModel Model => _model;
        public bool Obstructed => _obstructed;
        /// <summary>Test multiplier for dismemberment odds (range tool). 1 = the weapon's real calibre.</summary>
        public float CalibreBoost { get; set; } = 1f;
        public bool TriggerHeld => input.FireHeld;

        /// <summary>Raised on the tick a round is fired (after the hitscan).</summary>
        public event System.Action<WeaponState> Fired;
        public event System.Action DryFired;
        /// <summary>Raised for every bullet that hits something (effects, decals). Direction is the shot direction.</summary>
        public event System.Action<RaycastHit, Vector3> HitSurface;
        /// <summary>When false the placeholder impact cubes are skipped (a VFX component handles impacts).</summary>
        public bool SimpleImpactMarkers { get; set; } = true;
        public event System.Action ReloadStarted;
        public event System.Action MagazineInserted;
        /// <summary>Raised when a reload completes; true if it was an empty reload (slide released).</summary>
        public event System.Action<bool> ReloadFinished;
        /// <summary>Raised when the safety flips; argument: now on.</summary>
        public event System.Action<bool> SafetyToggled;
        public event System.Action<WeaponAction> ActionStarted;
        /// <summary>Raised when an action completes; for a chamber check, the state holds the result.</summary>
        public event System.Action<WeaponAction, WeaponState> ActionCompleted;

        private void Awake()
        {
            _state = WeaponState.Loaded(definition.Stats, definition.StartingReserve, seed);
            var materials = new WeaponMaterials
            {
                Steel = steelMaterial,
                SteelDark = steelDarkMaterial != null ? steelDarkMaterial : steelMaterial,
                Grip = gripMaterial,
                GripDark = gripDarkMaterial != null ? gripDarkMaterial : gripMaterial,
                Brass = brassMaterial != null ? brassMaterial : steelMaterial,
            };
            if (modelPrefab != null) _model = WeaponModel.CreateFromModel(modelPrefab, transform, gameObject.layer, materials, modelMaterial, outlineMaterial);
            if (_model == null) _model = M1911Builder.Build(transform, steelMaterial, gripMaterial, gameObject.layer);
            ImpactEffects.Material = steelMaterial;

            _catalog = definition.BuildCatalog();
            WeaponBuild saved = WeaponBuild.ParseOr(PlayerPrefs.GetString(BuildPrefsKey, null), definition.DefaultBuild);
            ApplyBuild(saved, false);
        }

        /// <summary>
        /// Mounts <paramref name="build"/> (invalid builds fall back to the factory build): rebuilds the attachment
        /// models, refreshes effective stats and muzzle, and persists the choice. Safe to call at runtime.
        /// </summary>
        public void ApplyBuild(WeaponBuild build, bool persist = true)
        {
            if (_model == null || _catalog == null || definition == null) return;
            if (!LoadoutRules.Validate(build, _catalog).IsValid) build = definition.DefaultBuild;
            _build = build;
            _effective = LoadoutRules.EffectiveStats(definition.Stats, build, _catalog);
            _stats = _effective.Stats;
            float ergonomics = Mathf.Max(_effective.Ergonomics, 10f);
            _stats.AimTime = definition.Stats.AimTime * WeaponBaseline.M1911.Ergonomics / ergonomics;
            float baseRecoil = definition.Stats.VerticalRecoil;
            RecoilMultiplier = baseRecoil > 0f ? _stats.VerticalRecoil / baseRecoil : 1f;
            _lengthExtra = Mathf.Max(0f, _effective.LengthM - WeaponBaseline.M1911.LengthM);

            var ordered = new List<AttachmentDefinition>();
            foreach (AttachmentSlot slot in new[] { AttachmentSlot.Barrel, AttachmentSlot.Muzzle, AttachmentSlot.Grips, AttachmentSlot.Magazine })
            {
                string id = build.Get(slot);
                if (id == null || definition.Attachments == null) continue;
                foreach (AttachmentDefinition a in definition.Attachments)
                    if (a != null && a.Id == id) { ordered.Add(a); break; }
            }
            _muzzle = _model.MountAttachments(ordered, gameObject.layer, outlineMaterial);
            if (persist)
            {
                PlayerPrefs.SetString(BuildPrefsKey, build.Serialize());
                PlayerPrefs.Save();
            }
            BuildChanged?.Invoke(build);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void Update()
        {
            // Debug shortcut: F9 toggles the suppressor (the lobby armory is the real UI).
            if (Keyboard.current == null || !Keyboard.current.f9Key.wasPressedThisFrame) return;
            ToggleSuppressor();
        }
#endif

        public void ToggleSuppressor()
        {
            ApplyBuild(_build.Has("suppressor_45")
                ? LoadoutRules.Remove(_build, AttachmentSlot.Muzzle, _catalog)
                : LoadoutRules.Equip(_build, "suppressor_45", _catalog));
        }

        private void OnEnable() => motor.Ticked += OnTick;
        private void OnDisable() => motor.Ticked -= OnTick;

        /// <summary>Refills ammo (debug / test range).</summary>
        public void Resupply()
        {
            _state.Reserve = definition.StartingReserve;
        }

        private void OnTick(float dt)
        {
            ProbeObstruction();
            var context = new WeaponContext(motor.State.Locomotion == LocomotionState.Sprint, _obstructed);
            input.ConsumeWeaponActions(out bool safety, out bool inspect, out bool chamberCheck);
            var tickInput = new WeaponInput(input.FireHeld, input.ConsumeFirePressed(), input.AimHeld, input.ConsumeReload(),
                safety, inspect, chamberCheck);
            _state = WeaponMotor.Step(_state, tickInput, context, _stats, dt);
            // Aiming slows the body and blocks sprint (applied by the motor from the next tick).
            motor.Aiming = tickInput.AimHeld && _state.Action == WeaponAction.None;

            if (_state.JustFired)
            {
                Shoot(_state.SpreadOffset);
                Fired?.Invoke(_state);
                ShotHeard?.Invoke(_effective.Loudness);
            }
            if (_state.JustDryFired) DryFired?.Invoke();
            if (_state.JustStartedReload)
            {
                _lastReloadWasEmpty = _state.Reload == ReloadKind.Empty;
                ReloadStarted?.Invoke();
            }
            if (_state.JustInsertedMagazine) MagazineInserted?.Invoke();
            if (_state.JustFinishedReload) ReloadFinished?.Invoke(_lastReloadWasEmpty);
            if (_state.JustToggledSafety) SafetyToggled?.Invoke(_state.SafetyOn);
            if (_state.JustStartedAction)
            {
                _lastAction = _state.Action;
                ActionStarted?.Invoke(_state.Action);
            }
            if (_state.JustCompletedAction) ActionCompleted?.Invoke(_lastAction, _state);
        }

        private void ProbeObstruction()
        {
            // Hysteresis so the weapon doesn't flicker at the threshold.
            float length = (definition.Length + _lengthExtra) * (_obstructed ? 1.08f : 1f);
            _obstructed = Physics.SphereCast(eye.position, 0.045f, eye.forward, out _, length, hitMask,
                QueryTriggerInteraction.Ignore);
        }

        private void Shoot(Vector2 spread)
        {
            Vector3 direction = eye.rotation * Quaternion.Euler(-spread.y, spread.x, 0f) * Vector3.forward;
            if (!Physics.Raycast(eye.position, direction, out RaycastHit hit, definition.Range, hitMask,
                    QueryTriggerInteraction.Ignore))
                return;

            if (hit.rigidbody != null)
                hit.rigidbody.AddForceAtPosition(direction * definition.ImpactForce, hit.point, ForceMode.Impulse);

            var receiver = hit.collider.GetComponentInParent<IShotReceiver>();
            receiver?.OnShot(new ShotHit(hit.point, hit.normal, direction, definition.Damage, definition.ImpactForce,
                definition.CalibreMultiplier * CalibreBoost, eye.position));
            if (SimpleImpactMarkers) ImpactEffects.Spawn(hit.point, hit.normal);
            HitSurface?.Invoke(hit, direction);
        }
    }
}
