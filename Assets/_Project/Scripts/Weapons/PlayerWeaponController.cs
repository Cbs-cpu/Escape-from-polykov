using System;
using Polykov.Input;
using Polykov.Movement;
using Polykov.Player;
using UnityEngine;

namespace Polykov.Weapons
{
    /// <summary>Result of one shot, for presentation (effects, recoil).</summary>
    public readonly struct ShotInfo
    {
        public readonly Vector3 Origin;
        public readonly Vector3 Direction;
        public readonly bool Hit;
        public readonly RaycastHit HitInfo;
        public readonly SurfaceType Surface;

        public ShotInfo(Vector3 origin, Vector3 direction, bool hit, RaycastHit hitInfo, SurfaceType surface)
        {
            Origin = origin;
            Direction = direction;
            Hit = hit;
            HitInfo = hitInfo;
            Surface = surface;
        }
    }

    /// <summary>
    /// Owns the local player's weapon: builds commands from input, steps the pure <see cref="WeaponSimulation"/>,
    /// resolves hits (server-side in Phase 2), and raises events for presentation.
    /// </summary>
    [DefaultExecutionOrder(10)]
    public sealed class PlayerWeaponController : MonoBehaviour
    {
        [SerializeField] private WeaponData data;
        [SerializeField] private WeaponViewData view;
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private PlayerLook look;
        [Tooltip("Camera transform used as the shot origin.")]
        [SerializeField] private Transform eye;
        [SerializeField] private WeaponCameraEffects cameraEffects;
        [SerializeField] private EffectPool effects;
        [SerializeField] private LayerMask hitMask = ~0;
        [SerializeField] private bool startEquipped = true;
        [Tooltip("A click during sprint/recovery is kept this long and fires as soon as possible.")]
        [SerializeField, Range(0f, 0.5f)] private float fireBufferTime = 0.25f;

        private WeaponState _state;
        private float _fireBuffer;
        private bool _fireWasHeld;
        private Transform[] _bulletHoles;
        private int _nextHole;
        private bool _reloadRequested;

        public WeaponState State => _state;
        public WeaponData Data => data;
        public WeaponViewData View => view;
        public bool TriggerHeld { get; private set; }
        public float CurrentSpread { get; private set; }

        /// <summary>All events produced by the simulation this frame.</summary>
        public event Action<WeaponEvents> EventsRaised;
        public event Action<ShotInfo> Fired;

        /// <summary>Testing / AI hook: queue a trigger pull as if the player clicked.</summary>
        public void RequestFire() => _fireBuffer = fireBufferTime;
        /// <summary>Testing / AI hook: request a reload on the next step.</summary>
        public void RequestReload() => _reloadRequested = true;
        /// <summary>Testing / AI hook: hold aim regardless of input.</summary>
        public bool ForceAim { get; set; }

        private void Awake()
        {
            _state = WeaponState.CreateLoaded(data.Tuning, startEquipped);
            if (startEquipped)
            {
                _state.Action = WeaponAction.Equipping;
                _state.ActionProgress = 0f;
            }
            effects.Prewarm(view.MuzzleFlash);
            effects.Prewarm(view.ImpactConcrete);
            effects.Prewarm(view.ImpactMetal);
            CreateBulletHoles(48);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            MovementState ms = motor.State;
            bool sprinting = ms.Locomotion == LocomotionState.Sprint;

            bool fireHeld = input.FireHeld;
            bool freshPress = fireHeld && !_fireWasHeld;
            _fireWasHeld = fireHeld;
            if (freshPress) _fireBuffer = fireBufferTime;
            else _fireBuffer = Mathf.Max(0f, _fireBuffer - dt);

            bool canPull = WeaponSimulation.CanPull(_state, sprinting);
            bool firePressed = _fireBuffer > 0f && canPull;
            if (firePressed) _fireBuffer = 0f;

            float moveFactor = ms.PlanarSpeed / Mathf.Max(motor.Tuning.RunSpeed, 0.01f);
            bool aimHeld = input.AimHeld || ForceAim;
            bool reload = input.ConsumeReload() || _reloadRequested;
            _reloadRequested = false;
            var cmd = new WeaponCommand(fireHeld, firePressed, aimHeld, reload,
                input.ConsumeToggleWeapon(), sprinting, ms.Grounded, moveFactor);

            _state = WeaponSimulation.Step(_state, cmd, data.Tuning, dt, out WeaponEvents events);
            CurrentSpread = WeaponSimulation.Spread(_state, cmd, data.Tuning);
            TriggerHeld = fireHeld && _state.Action == WeaponAction.Ready;

            // Weapon intent drives movement: aiming slows you, shooting/reloading stops sprint.
            bool ready = _state.Action == WeaponAction.Ready;
            motor.Aiming = ready && aimHeld;
            motor.SprintBlocked = (fireHeld && ready) || _fireBuffer > 0f || _state.Action == WeaponAction.Reloading;

            if ((events & WeaponEvents.Fired) != 0) Shoot(cmd);
            if (events != WeaponEvents.None) EventsRaised?.Invoke(events);
        }

        private void Shoot(in WeaponCommand cmd)
        {
            Vector2 recoil = cameraEffects != null ? cameraEffects.AimOffset : Vector2.zero;
            Vector2 spread = WeaponSimulation.SpreadOffset(_state.ShotIndex, CurrentSpread);
            Quaternion aim = Quaternion.Euler(look.Pitch - recoil.y - spread.y, look.Yaw + recoil.x + spread.x, 0f);
            Vector3 origin = eye.position;
            Vector3 direction = aim * Vector3.forward;

            bool hit = Physics.Raycast(origin, direction, out RaycastHit info, data.Tuning.Range, hitMask,
                QueryTriggerInteraction.Ignore);
            SurfaceType surface = SurfaceType.Concrete;
            if (hit)
            {
                if (info.collider.TryGetComponent(out SurfaceMaterial material)) surface = material.Type;
                var damage = new DamageInfo(data.Tuning.Damage, info.point, info.normal, direction, view.HitForce);
                IDamageable target = info.collider.GetComponentInParent<IDamageable>();
                target?.ApplyDamage(damage);
                Rigidbody body = info.rigidbody;
                if (body != null && !body.isKinematic) body.AddForceAtPosition(direction * view.HitForce, info.point, ForceMode.Impulse);
                SpawnImpact(info, surface, direction);
            }
            Fired?.Invoke(new ShotInfo(origin, direction, hit, info, surface));
        }

        private void SpawnImpact(in RaycastHit info, SurfaceType surface, Vector3 direction)
        {
            GameObject prefab = surface == SurfaceType.Metal ? view.ImpactMetal : view.ImpactConcrete;
            // Impact effects use +Z = surface normal, mixed a bit toward the reflection for a natural spray.
            Vector3 reflect = Vector3.Reflect(direction, info.normal);
            Vector3 forward = Vector3.Slerp(info.normal, reflect, 0.35f);
            effects.Spawn(prefab, info.point + info.normal * 0.005f, Quaternion.LookRotation(forward));

            Transform hole = _bulletHoles[_nextHole];
            _nextHole = (_nextHole + 1) % _bulletHoles.Length;
            hole.gameObject.SetActive(true);
            hole.SetParent(info.rigidbody != null ? info.collider.transform : effects.transform, true);
            // Bullet hole prefab: +Z = surface normal. Random roll hides repetition.
            hole.SetPositionAndRotation(info.point + info.normal * 0.002f,
                Quaternion.LookRotation(info.normal) * Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f)));
            hole.localScale = Vector3.one * view.BulletHoleSize;
            if (hole.parent != effects.transform)
            {
                Vector3 s = hole.parent.lossyScale;
                hole.localScale = new Vector3(view.BulletHoleSize / s.x, view.BulletHoleSize / s.y, view.BulletHoleSize / s.z);
            }
        }

        private void CreateBulletHoles(int count)
        {
            _bulletHoles = new Transform[count];
            for (int i = 0; i < count; i++)
            {
                GameObject go = view.BulletHole != null ? Instantiate(view.BulletHole, effects.transform) : new GameObject("BulletHole");
                go.SetActive(false);
                _bulletHoles[i] = go.transform;
            }
        }
    }
}
