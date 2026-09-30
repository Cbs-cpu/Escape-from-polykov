using Polykov.Player;
using UnityEngine;

namespace Polykov.Weapons
{
    /// <summary>
    /// Moves the modular M1911 parts from weapon events/state: slide cycle and lock-back, barrel, hammer fall and
    /// re-cock, trigger, slide stop, magazine drop/insert. Spawns the muzzle flash and ejects casings.
    /// Axes are derived from the model's own sockets, so it does not depend on import orientation.
    /// </summary>
    [DefaultExecutionOrder(105)]
    public sealed class WeaponMechanics : MonoBehaviour
    {
        [SerializeField] private PlayerWeaponController weapon;
        [SerializeField] private WeaponPose pose;
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private EffectPool effects;
        [SerializeField] private DebrisPool casings;
        [SerializeField] private DebrisPool droppedMagazines;
        [Tooltip("Flip if the hammer falls the wrong way.")]
        [SerializeField] private float hammerDirection = 1f;
        [SerializeField] private float slideStopDirection = -1f;

        private Transform _slide, _barrel, _hammer, _trigger, _magazine, _slideStop;
        private Vector3 _slideRest, _barrelRest, _triggerRest, _magRest;
        private Quaternion _hammerRest, _slideStopRest;
        private Vector3 _forward, _right, _magDown;
        // Axes expressed in each part's parent space (robust to intermediate import nodes).
        private Vector3 _slideAxis, _barrelAxis, _triggerAxis, _magAxis, _hammerAxis, _stopAxis;
        private Renderer[] _magRenderers;

        private float _sinceShot = 10f;
        private float _sinceRelease = 10f;
        private bool _locked;
        private bool _hammerDown;
        private float _hammerAngle;
        private float _trigger01;
        private float _magInsert = -1f;

        private void Awake()
        {
            _slide = Part("M1911_Slide", out _slideRest);
            _barrel = Part("M1911_Barrel", out _barrelRest);
            _trigger = Part("M1911_Trigger", out _triggerRest);
            _magazine = Part("M1911_Magazine", out _magRest);
            _hammer = Part("M1911_Hammer", out _);
            _slideStop = Part("M1911_SlideStop", out _);
            _hammerRest = _hammer.localRotation;
            _slideStopRest = _slideStop.localRotation;
            _magRenderers = _magazine.GetComponentsInChildren<Renderer>(true);

            // Weapon axes in this (model root) space, from the sockets.
            Vector3 rear = transform.InverseTransformPoint(WeaponPose.FindRecursive(transform, "Socket_RearSight").position);
            Vector3 front = transform.InverseTransformPoint(WeaponPose.FindRecursive(transform, "Socket_FrontSight").position);
            Vector3 grip = transform.InverseTransformPoint(WeaponPose.FindRecursive(transform, "Socket_RightHand").position);
            Vector3 well = transform.InverseTransformPoint(WeaponPose.FindRecursive(transform, "Socket_MagWell").position);
            _forward = (front - rear).normalized;
            Vector3 up = Vector3.ProjectOnPlane(rear - grip, _forward).normalized;
            _right = Vector3.Cross(up, _forward).normalized;
            Vector3 magPivot = transform.InverseTransformPoint(_magazine.position);
            _magDown = (well - magPivot).normalized;

            _slideAxis = InParent(_slide, _forward);
            _barrelAxis = InParent(_barrel, _forward);
            _triggerAxis = InParent(_trigger, _forward);
            _magAxis = InParent(_magazine, _magDown);
            _hammerAxis = InParent(_hammer, _right);
            _stopAxis = InParent(_slideStop, _right);
        }

        private Vector3 InParent(Transform part, Vector3 dirInRoot)
        {
            Vector3 world = transform.TransformDirection(dirInRoot);
            return part.parent.InverseTransformDirection(world).normalized;
        }

        private void OnEnable() => weapon.EventsRaised += OnEvents;
        private void OnDisable() => weapon.EventsRaised -= OnEvents;

        private void OnEvents(WeaponEvents events)
        {
            if ((events & WeaponEvents.Fired) != 0)
            {
                _sinceShot = 0f;
                _hammerDown = true;
                SpawnMuzzleFlash();
                EjectCasing();
            }
            if ((events & WeaponEvents.SlideLockedBack) != 0) _locked = true;
            if ((events & WeaponEvents.SlideReleased) != 0)
            {
                _locked = false;
                _sinceRelease = 0f;
            }
            if ((events & WeaponEvents.MagazineOut) != 0) DropMagazine();
            if ((events & WeaponEvents.MagazineIn) != 0) _magInsert = 0f;
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            WeaponViewData v = weapon.View;
            _sinceShot += dt;
            _sinceRelease += dt;

            // ---- slide
            float travel = v.SlideTravel;
            float offset;
            if (_sinceShot < v.SlideBackTime) offset = travel * (_sinceShot / v.SlideBackTime);
            else if (_locked) offset = travel;
            else if (_sinceRelease < 0.04f) offset = travel * (1f - _sinceRelease / 0.04f);
            else if (_sinceShot < v.SlideBackTime + v.SlideReturnTime)
                offset = travel * (1f - EaseOut((_sinceShot - v.SlideBackTime) / v.SlideReturnTime));
            else offset = 0f;
            _slide.localPosition = _slideRest - _slideAxis * offset;
            _barrel.localPosition = _barrelRest - _barrelAxis * (offset * v.BarrelTravelRatio);

            // ---- hammer: falls on the shot, re-cocked by the slide going back
            if (_hammerDown && offset > travel * 0.45f) _hammerDown = false;
            float hammerTarget = _hammerDown ? v.HammerFallDegrees : 0f;
            _hammerAngle = Mathf.MoveTowards(_hammerAngle, hammerTarget, 4000f * dt);
            _hammer.localRotation = Quaternion.AngleAxis(_hammerAngle * hammerDirection, _hammerAxis) * _hammerRest;

            // ---- trigger
            _trigger01 = Mathf.Lerp(_trigger01, weapon.TriggerHeld ? 1f : 0f, 1f - Mathf.Exp(-45f * dt));
            _trigger.localPosition = _triggerRest - _triggerAxis * (v.TriggerTravel * _trigger01);

            // ---- slide stop
            float stop = _locked ? v.SlideStopLockedDegrees * slideStopDirection : 0f;
            _slideStop.localRotation = Quaternion.AngleAxis(stop, _stopAxis) * _slideStopRest;

            // ---- magazine
            bool hasMag = weapon.State.HasMagazine;
            if (_magInsert >= 0f)
            {
                _magInsert += dt / v.MagazineInsertTime;
                float t = Mathf.Clamp01(_magInsert);
                _magazine.localPosition = _magRest + _magAxis * (v.MagazineInsertDistance * (1f - EaseOut(t)));
                SetMagVisible(true);
                if (_magInsert >= 1f) _magInsert = -1f;
            }
            else
            {
                _magazine.localPosition = _magRest;
                SetMagVisible(hasMag);
            }
        }

        private void SpawnMuzzleFlash()
        {
            Transform muzzle = pose.MuzzleSocket;
            if (muzzle == null) return;
            Vector3 fwd = transform.TransformDirection(_forward);
            effects.Spawn(weapon.View.MuzzleFlash, muzzle.position, Quaternion.LookRotation(fwd, transform.TransformDirection(Vector3.Cross(_forward, _right))), muzzle);
        }

        private void EjectCasing()
        {
            Transform port = pose.EjectionSocket;
            if (casings == null || port == null) return;
            WeaponViewData v = weapon.View;
            Vector3 right = transform.TransformDirection(_right);
            Vector3 fwd = transform.TransformDirection(_forward);
            Vector3 up = Vector3.Cross(fwd, right);
            Vector3 local = v.EjectVelocity + Random.insideUnitSphere * v.EjectRandomness;
            Vector3 velocity = right * local.x + up * local.y + fwd * local.z + motor.State.PlanarVelocity;
            Vector3 spin = Random.onUnitSphere * v.EjectSpin;
            casings.Launch(port.position, Quaternion.LookRotation(right, up), velocity, spin);
        }

        private void DropMagazine()
        {
            if (droppedMagazines == null) return;
            Vector3 down = transform.TransformDirection(_magDown);
            Vector3 velocity = down * 1.2f + motor.State.PlanarVelocity;
            droppedMagazines.Launch(_magazine.position, _magazine.rotation, velocity, Random.onUnitSphere * 4f);
            SetMagVisible(false);
        }

        private void SetMagVisible(bool visible)
        {
            for (int i = 0; i < _magRenderers.Length; i++)
                if (_magRenderers[i].enabled != visible) _magRenderers[i].enabled = visible;
        }

        private Transform Part(string partName, out Vector3 restPosition)
        {
            Transform t = WeaponPose.FindRecursive(transform, partName);
            restPosition = t.localPosition;
            return t;
        }

        private static float EaseOut(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - (1f - t) * (1f - t);
        }
    }
}
