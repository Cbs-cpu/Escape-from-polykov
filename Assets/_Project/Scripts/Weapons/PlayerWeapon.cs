using Polykov.Input;
using Polykov.Movement;
using Polykov.Player;
using UnityEngine;

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
        [Tooltip("Imported weapon model (FBX from ArtSource/Tools/build_m1911.py). Empty = procedural placeholder.")]
        [SerializeField] private GameObject modelPrefab;
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

        public WeaponDefinition Definition => definition;
        public WeaponState State => _state;
        public WeaponModel Model => _model;
        public bool Obstructed => _obstructed;
        public bool TriggerHeld => input.FireHeld;

        /// <summary>Raised on the tick a round is fired (after the hitscan).</summary>
        public event System.Action<WeaponState> Fired;
        public event System.Action DryFired;
        public event System.Action ReloadStarted;
        public event System.Action MagazineInserted;
        /// <summary>Raised when a reload completes; true if it was an empty reload (slide released).</summary>
        public event System.Action<bool> ReloadFinished;

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
            if (modelPrefab != null) _model = WeaponModel.CreateFromModel(modelPrefab, transform, gameObject.layer, materials);
            if (_model == null) _model = M1911Builder.Build(transform, steelMaterial, gripMaterial, gameObject.layer);
            ImpactEffects.Material = steelMaterial;
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
            var tickInput = new WeaponInput(input.FireHeld, input.ConsumeFirePressed(), input.AimHeld, input.ConsumeReload());
            _state = WeaponMotor.Step(_state, tickInput, context, definition.Stats, dt);

            if (_state.JustFired)
            {
                Shoot(_state.SpreadOffset);
                Fired?.Invoke(_state);
            }
            if (_state.JustDryFired) DryFired?.Invoke();
            if (_state.JustStartedReload)
            {
                _lastReloadWasEmpty = _state.Reload == ReloadKind.Empty;
                ReloadStarted?.Invoke();
            }
            if (_state.JustInsertedMagazine) MagazineInserted?.Invoke();
            if (_state.JustFinishedReload) ReloadFinished?.Invoke(_lastReloadWasEmpty);
        }

        private void ProbeObstruction()
        {
            // Hysteresis so the weapon doesn't flicker at the threshold.
            float length = definition.Length * (_obstructed ? 1.08f : 1f);
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
            receiver?.OnShot(new ShotHit(hit.point, hit.normal, direction, definition.Damage));
            ImpactEffects.Spawn(hit.point, hit.normal);
        }
    }
}
