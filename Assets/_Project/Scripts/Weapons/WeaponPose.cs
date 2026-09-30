using Polykov.Movement;
using Polykov.Player;
using UnityEngine;

namespace Polykov.Weapons
{
    /// <summary>
    /// Places the first-person weapon every frame by layering: hip/ADS (auto-aligned to the model's sights),
    /// sprint, equip, reload, wall-block, mouse sway, movement bob, breathing, landing and recoil springs.
    /// Lives on the weapon holder, a child of the camera. Runs after the camera, before arm IK.
    /// </summary>
    [DefaultExecutionOrder(110)]
    public sealed class WeaponPose : MonoBehaviour
    {
        [SerializeField] private PlayerWeaponController weapon;
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private PlayerLook look;
        [Tooltip("Instance of the weapon model (child of this transform, identity local pose).")]
        [SerializeField] private Transform model;
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private LayerMask blockMask = ~0;

        private Transform _rearSight, _frontSight;
        private Vector3 _rearLocal, _frontLocal;
        private SpringVector3 _kickPos, _kickRot, _sway, _landing;
        private float _sprint, _blocked, _bobPhase, _bobWeight, _breath;
        private Renderer[] _renderers;
        private bool _visible = true;

        public Transform RightHandSocket { get; private set; }
        public Transform LeftHandSocket { get; private set; }
        public Transform MuzzleSocket { get; private set; }
        public Transform EjectionSocket { get; private set; }
        public Transform MagWellSocket { get; private set; }
        /// <summary>0 = holstered (hands follow animation), 1 = weapon fully in hands.</summary>
        public float HandsWeight { get; private set; }
        public float AimBlend { get; private set; }

        private void Awake()
        {
            _rearSight = Find("Socket_RearSight");
            _frontSight = Find("Socket_FrontSight");
            RightHandSocket = Find("Socket_RightHand");
            LeftHandSocket = Find("Socket_LeftHand");
            MuzzleSocket = Find("Socket_Muzzle");
            EjectionSocket = Find("Socket_EjectionPort");
            MagWellSocket = Find("Socket_MagWell");
            _rearLocal = transform.InverseTransformPoint(_rearSight.position);
            _frontLocal = transform.InverseTransformPoint(_frontSight.position);
            _renderers = model.GetComponentsInChildren<Renderer>(true);
        }

        private void OnEnable()
        {
            weapon.Fired += OnFired;
            weapon.EventsRaised += OnEvents;
            motor.Landed += OnLanded;
        }

        private void OnDisable()
        {
            weapon.Fired -= OnFired;
            weapon.EventsRaised -= OnEvents;
            motor.Landed -= OnLanded;
        }

        private void OnFired(ShotInfo shot)
        {
            RecoilTuning r = weapon.Data.Recoil;
            float mult = Mathf.Lerp(1f, r.AimMultiplier, weapon.State.Aim);
            _kickPos.AddImpulse(SpringVector3.ImpulseFor(new Vector3(0f, r.KickUp, -r.KickBack) * mult, r.WeaponStiffness, r.WeaponDamping));
            var rot = new Vector3(-r.KickPitch, Random.Range(-r.KickYaw, r.KickYaw), Random.Range(-r.KickRoll, r.KickRoll));
            _kickRot.AddImpulse(SpringVector3.ImpulseFor(rot * mult, r.WeaponStiffness, r.WeaponDamping));
        }

        private void OnEvents(WeaponEvents events)
        {
            RecoilTuning r = weapon.Data.Recoil;
            if ((events & WeaponEvents.MagazineIn) != 0)
                _kickPos.AddImpulse(SpringVector3.ImpulseFor(new Vector3(0f, 0.012f, 0f), r.WeaponStiffness, r.WeaponDamping));
            if ((events & WeaponEvents.SlideReleased) != 0)
                _kickRot.AddImpulse(SpringVector3.ImpulseFor(new Vector3(-4f, 0f, 3f), r.WeaponStiffness, r.WeaponDamping));
        }

        private void OnLanded(float impact)
        {
            float drop = Mathf.Min(impact * 0.006f, 0.035f);
            _landing.AddImpulse(SpringVector3.ImpulseFor(new Vector3(0f, -drop, 0f), 150f, 0.5f));
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            WeaponViewData v = weapon.View;
            WeaponState s = weapon.State;
            MovementState ms = motor.State;

            // ---- base pose: hip <-> sights
            Quaternion aimRot = Quaternion.FromToRotation(_frontLocal - _rearLocal, Vector3.forward);
            Vector3 aimPos = new Vector3(0f, 0f, v.AimDistance) - aimRot * _rearLocal;
            float a = Smooth(s.Aim);
            AimBlend = a;
            Vector3 pos = Vector3.Lerp(v.HipPosition, aimPos, a);
            Quaternion rot = Quaternion.Slerp(Quaternion.Euler(v.HipEuler), aimRot, a);

            // ---- sprint
            bool sprinting = ms.Locomotion == LocomotionState.Sprint && s.Action != WeaponAction.Reloading;
            _sprint = Damp(_sprint, sprinting ? 1f : 0f, v.SprintBlendSharpness, dt);
            pos = Vector3.Lerp(pos, v.SprintPosition, _sprint);
            rot = Quaternion.Slerp(rot, Quaternion.Euler(v.SprintEuler), _sprint);

            // ---- reload
            if (s.Action == WeaponAction.Reloading)
            {
                float r = ReloadCurve(s.ActionProgress);
                pos += v.ReloadOffset * r;
                rot *= Quaternion.Euler(v.ReloadEuler * r);
            }

            // ---- pushed back by walls
            float block = 0f;
            if (cameraTransform != null && Physics.SphereCast(cameraTransform.position, 0.04f, cameraTransform.forward,
                    out RaycastHit hit, v.BlockCheckDistance, blockMask, QueryTriggerInteraction.Ignore))
            {
                block = 1f - Mathf.Clamp01((hit.distance - 0.22f) / Mathf.Max(v.BlockCheckDistance - 0.22f, 0.01f));
            }
            _blocked = Damp(_blocked, block, v.BlockedBlendSharpness, dt);
            pos = Vector3.Lerp(pos, v.BlockedPosition, _blocked);
            rot = Quaternion.Slerp(rot, Quaternion.Euler(v.BlockedEuler), _blocked);

            // ---- equip / holster
            float e = EquipBlend(s);
            HandsWeight = Mathf.Clamp01(e);
            pos = Vector3.LerpUnclamped(v.EquipPosition, pos, e);
            rot = Quaternion.SlerpUnclamped(Quaternion.Euler(v.EquipEuler), rot, e);
            SetVisible(s.Action != WeaponAction.Holstered);

            // ---- mouse sway (angular velocity based, frame-rate independent)
            Vector2 rate = dt > 0f ? look.LastDelta / dt : Vector2.zero;
            float swayMult = Mathf.Lerp(1f, v.AimSwayMultiplier, a) * v.SwayRotation * 0.01f;
            _sway.Target = new Vector3(
                Mathf.Clamp(rate.y * swayMult, -v.SwayMaxDegrees, v.SwayMaxDegrees),
                Mathf.Clamp(-rate.x * swayMult, -v.SwayMaxDegrees, v.SwayMaxDegrees),
                Mathf.Clamp(-rate.x * swayMult * 0.6f, -v.SwayMaxDegrees, v.SwayMaxDegrees));
            _sway.Step(v.SwayStiffness, v.SwayDamping, dt);
            Vector3 swayPos = new Vector3(_sway.Value.y, -_sway.Value.x, 0f) * v.SwayPosition;

            // ---- movement bob (figure eight) + breathing
            float runSpeed = Mathf.Max(motor.Tuning.RunSpeed, 0.01f);
            float speed = ms.PlanarSpeed;
            _bobWeight = Damp(_bobWeight, ms.Grounded ? Mathf.Clamp(speed / runSpeed, 0f, 1.6f) : 0f, 10f, dt);
            _bobPhase = Mathf.Repeat(_bobPhase + speed * dt / v.StrideLength * Mathf.PI, Mathf.PI * 2f);
            float bobMult = _bobWeight * Mathf.Lerp(1f, v.AimBobMultiplier, a) * Mathf.Lerp(1f, v.SprintBobMultiplier, _sprint);
            Vector3 bob = new Vector3(Mathf.Sin(_bobPhase) * v.BobPosition.x, Mathf.Sin(_bobPhase * 2f) * v.BobPosition.y, 0f) * bobMult;
            float bobRoll = Mathf.Sin(_bobPhase) * v.BobRoll * bobMult;
            _breath = Mathf.Repeat(_breath + dt * v.BreathRate * Mathf.PI * 2f, Mathf.PI * 2f);
            Vector3 breath = new Vector3(0f, Mathf.Sin(_breath) * v.BreathAmplitude * (1f - 0.6f * a), 0f);

            // ---- air + landing
            Vector3 air = ms.Grounded ? Vector3.zero : new Vector3(0f, Mathf.Clamp(-ms.VerticalSpeed * 0.003f, -0.02f, 0.02f), 0f);
            _landing.Step(150f, 0.5f, dt);

            // ---- recoil
            RecoilTuning recoil = weapon.Data.Recoil;
            _kickPos.Step(recoil.WeaponStiffness, recoil.WeaponDamping, dt);
            _kickRot.Step(recoil.WeaponStiffness, recoil.WeaponDamping, dt);

            transform.localPosition = pos + swayPos + bob + breath + air + _landing.Value + _kickPos.Value;
            transform.localRotation = rot * Quaternion.Euler(_sway.Value) * Quaternion.Euler(0f, 0f, bobRoll)
                                      * Quaternion.Euler(_kickRot.Value);
        }

        private static float EquipBlend(in WeaponState s)
        {
            switch (s.Action)
            {
                case WeaponAction.Holstered: return 0f;
                case WeaponAction.Equipping: return EaseOutBack(s.ActionProgress);
                case WeaponAction.Holstering: return 1f - s.ActionProgress * s.ActionProgress;
                default: return 1f;
            }
        }

        private static float ReloadCurve(float t)
        {
            if (t < 0.12f) return Smooth(t / 0.12f);
            if (t > 0.86f) return Smooth((1f - t) / 0.14f);
            return 1f;
        }

        private void SetVisible(bool visible)
        {
            if (visible == _visible) return;
            _visible = visible;
            for (int i = 0; i < _renderers.Length; i++) _renderers[i].enabled = visible;
        }

        private Transform Find(string socketName)
        {
            Transform t = FindRecursive(model, socketName);
            if (t == null) Debug.LogError($"[WeaponPose] Socket '{socketName}' not found under {model.name}", this);
            return t;
        }

        internal static Transform FindRecursive(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindRecursive(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.4f;
            const float c3 = c1 + 1f;
            float x = t - 1f;
            return 1f + c3 * x * x * x + c1 * x * x;
        }

        private static float Damp(float current, float target, float sharpness, float dt)
            => Mathf.Lerp(current, target, 1f - Mathf.Exp(-sharpness * dt));
    }
}
