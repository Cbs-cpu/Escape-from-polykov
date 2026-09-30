using Polykov.Animation;
using Polykov.CameraSystem;
using Polykov.Movement;
using Polykov.Player;
using UnityEngine;

namespace Polykov.Weapons
{
    /// <summary>
    /// Local presentation of the held weapon: places it relative to the eye (hip, aimed, lowered, obstructed,
    /// reloading), adds sway, bob and a recoil spring, animates the slide/magazine/trigger, drives the hand IK
    /// targets, the ADS zoom and the muzzle flash. Runs after the camera rig and before the hand IK.
    /// Never replicated: remote players will derive the same poses from replicated weapon state.
    /// </summary>
    [DefaultExecutionOrder(110)]
    public sealed class WeaponPresenter : MonoBehaviour
    {
        [SerializeField] private PlayerWeapon weapon;
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private PlayerLook look;
        [SerializeField] private FirstPersonCameraRig cameraRig;
        [SerializeField] private HandIK hands;

        [Header("Blending")]
        [SerializeField, Min(1f)] private float stateSharpness = 14f;
        [Tooltip("Meters travelled per weapon bob cycle.")]
        [SerializeField, Min(0.2f)] private float bobStride = 1.5f;

        private Transform _eye;
        private WeaponModel _model;
        private Light _flash;
        private float _flashTimer;

        private float _aim;
        private float _lowered;
        private float _obstructed;
        private float _reload;
        private float _leftToMagazine;
        private float _slideKick;
        private float _triggerPull;

        private Vector2 _sway;
        private Vector2 _swayVelocity;
        private Vector2 _inertia;
        private Vector2 _inertiaVelocity;
        private Vector3 _smoothVelocity;
        private float _breathPhase;
        private float _lastYaw;
        private float _lastPitch;
        private float _bobPhase;
        private float _bobWeight;

        private float _kickPos;
        private float _kickPosVelocity;
        private float _kickRot;
        private float _kickRotVelocity;

        private float _dip;
        private float _dipVelocity;

        private float _action;
        private WeaponAction _actionKind;
        private float _actionT;
        private float _safety;

        private void OnEnable()
        {
            weapon.Fired += OnFired;
            motor.Jumped += OnJumped;
            motor.Landed += OnLanded;
        }

        private void OnDisable()
        {
            weapon.Fired -= OnFired;
            motor.Jumped -= OnJumped;
            motor.Landed -= OnLanded;
        }

        // Inertia: the weapon lags behind vertical body motion.
        private void OnJumped() => _dipVelocity -= 0.35f;
        private void OnLanded(float impact) => _dipVelocity -= Mathf.Min(impact * 0.09f, 0.9f);

        private void Start()
        {
            _eye = cameraRig.Camera != null ? cameraRig.Camera.transform : cameraRig.transform;
            _model = weapon.Model;
            hands.RightTarget = _model.RightHand;
            hands.LeftTarget = _model.LeftHand;
            hands.GripCenter = _model.GripCenter;
            _lastYaw = look.Yaw;
            _lastPitch = look.Pitch;

            if (_model.Muzzle != null)
            {
                // Placed with weapon-space axes: imported empties can carry an axis conversion in their rotation.
                var go = new GameObject("MuzzleFlash");
                go.transform.position = _model.Muzzle.position + _model.transform.forward * 0.03f;
                go.transform.SetParent(_model.Muzzle, true);
                _flash = go.AddComponent<Light>();
                _flash.type = LightType.Point;
                _flash.color = new Color(1f, 0.78f, 0.45f);
                _flash.range = 5f;
                _flash.intensity = 4f;
                _flash.shadows = LightShadows.None;
                _flash.enabled = false;
            }
        }

        private void OnFired(WeaponState state)
        {
            WeaponDefinition def = weapon.Definition;
            float aimed = 1f - 0.4f * _aim;
            _kickPosVelocity -= def.KickBack * aimed;
            _kickRotVelocity -= def.KickRotation * aimed;
            _slideKick = 1f;
            _flashTimer = 0.045f;
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            WeaponDefinition def = weapon.Definition;
            WeaponState state = weapon.State;

            // Smooth the 60 Hz simulation values for high frame rates.
            _aim = Damp(_aim, state.Aim, stateSharpness * 1.5f, dt);
            _lowered = Damp(_lowered, state.Lowered, stateSharpness, dt);
            _obstructed = Damp(_obstructed, weapon.Obstructed ? 1f : 0f, stateSharpness, dt);
            _reload = Damp(_reload, state.IsReloading ? 1f : 0f, 9f, dt);

            float aim = Mathf.SmoothStep(0f, 1f, _aim);
            // Tired hands shake more: sway and bob grow as stamina runs out.
            float fatigue = 1f - motor.State.StaminaFraction(motor.Tuning);
            float steadiness = Mathf.Lerp(1f, def.AdsSwayMultiplier, aim) * (1f + fatigue * 0.8f);

            // Base pose: hip <-> sights.
            Vector3 position = Vector3.Lerp(def.HipPosition, AdsPosition(def, out Quaternion adsRotation), aim);
            Quaternion rotation = Quaternion.Slerp(Quaternion.Euler(def.HipEuler), adsRotation, aim);

            // Lowered: sprint pose or pulled back from a wall.
            Vector3 lowPosition = Vector3.Lerp(def.LoweredPosition, def.ObstructedPosition, _obstructed);
            Quaternion lowRotation = Quaternion.Slerp(Quaternion.Euler(def.LoweredEuler), Quaternion.Euler(def.ObstructedEuler), _obstructed);
            float lowered = Mathf.SmoothStep(0f, 1f, _lowered);
            position = Vector3.Lerp(position, lowPosition, lowered);
            rotation = Quaternion.Slerp(rotation, lowRotation, lowered);

            float reload = Mathf.SmoothStep(0f, 1f, _reload);
            position = Vector3.Lerp(position, def.ReloadPosition, reload);
            rotation = Quaternion.Slerp(rotation, Quaternion.Euler(def.ReloadEuler), reload);

            // Hand actions: keep the last kind and phase while blending out so the pose never pops.
            if (state.Action != WeaponAction.None)
            {
                _actionKind = state.Action;
                float duration = state.Action == WeaponAction.Inspect ? def.Stats.InspectTime : def.Stats.ChamberCheckTime;
                _actionT = Mathf.Clamp01(state.ActionElapsed / duration);
            }
            _action = Damp(_action, state.Action != WeaponAction.None ? 1f : 0f, 8f, dt);
            if (_action > 0.001f)
            {
                ActionPose(def, out Vector3 actionPosition, out Quaternion actionRotation);
                float w = Mathf.SmoothStep(0f, 1f, _action);
                position = Vector3.Lerp(position, actionPosition, w);
                rotation = Quaternion.Slerp(rotation, actionRotation, w);
            }

            // Sway: the weapon lags camera turns a little.
            float yawRate = Mathf.DeltaAngle(_lastYaw, look.Yaw) / dt;
            float pitchRate = (look.Pitch - _lastPitch) / dt;
            _lastYaw = look.Yaw;
            _lastPitch = look.Pitch;
            var swayTarget = new Vector2(-pitchRate, -yawRate) * (def.SwayAmount * 0.02f * steadiness);
            swayTarget = Vector2.ClampMagnitude(swayTarget, def.MaxSway);
            // Underdamped spring: lags the turn, then overshoots a little. The clamp keeps it on screen.
            StepSpring(ref _sway, ref _swayVelocity, swayTarget, def.SwayStiffness, def.SwayDamping, dt);
            _sway = Vector2.ClampMagnitude(_sway, def.MaxSway * 1.3f);

            // Bob while walking.
            MovementState move = motor.State;
            float speed = move.PlanarSpeed;
            _bobPhase = Mathf.Repeat(_bobPhase + speed * dt / bobStride * Mathf.PI * 2f, Mathf.PI * 2f);
            float runSpeed = Mathf.Max(motor.Tuning.RunSpeed, 0.01f);
            _bobWeight = Damp(_bobWeight, move.Grounded ? Mathf.Clamp(speed / runSpeed, 0f, 1.5f) : 0f, 8f, dt);
            float bob = def.BobAmount * _bobWeight * steadiness * (1f - lowered * 0.5f);
            position += new Vector3(Mathf.Sin(_bobPhase) * bob, -Mathf.Abs(Mathf.Cos(_bobPhase)) * bob, 0f);

            Spring(ref _dip, ref _dipVelocity, 90f, 0.55f, dt);
            position.y += _dip * steadiness;

            // Inertia: the weapon trails body acceleration (starting, stopping, strafing).
            Vector3 lastSmooth = _smoothVelocity;
            _smoothVelocity = Vector3.Lerp(_smoothVelocity, move.PlanarVelocity, 1f - Mathf.Exp(-25f * dt));
            Vector3 accel = Quaternion.Euler(0f, -look.Yaw, 0f) * ((_smoothVelocity - lastSmooth) / dt);
            Vector2 inertiaTarget = Vector2.ClampMagnitude(new Vector2(-accel.x, -accel.z) * def.InertiaAmount, def.MaxInertia);
            StepSpring(ref _inertia, ref _inertiaVelocity, inertiaTarget, 90f, 0.6f, dt);
            position += new Vector3(_inertia.x, 0f, _inertia.y) * steadiness;

            // Breathing: slow, subtle rise and fall.
            _breathPhase = Mathf.Repeat(_breathPhase + dt * def.BreathRate * Mathf.PI * 2f, Mathf.PI * 2f);
            float breath = Mathf.Sin(_breathPhase);
            position.y += breath * def.BreathAmplitude * steadiness;

            // Recoil spring (critically-damped-ish).
            Spring(ref _kickPos, ref _kickPosVelocity, def.KickSpring, def.KickDamping, dt);
            Spring(ref _kickRot, ref _kickRotVelocity, def.KickSpring, def.KickDamping, dt);
            position += rotation * new Vector3(0f, 0f, _kickPos);
            rotation *= Quaternion.Euler(_kickRot + _sway.x + breath * 0.12f * steadiness, _sway.y, _sway.y * 0.6f);

            _model.transform.SetPositionAndRotation(_eye.position + _eye.rotation * position, _eye.rotation * rotation);

            AnimateParts(def, state, dt);
            PlaceHands(def, state, dt);

            cameraRig.ZoomFov = def.AdsFovReduction * aim;
            cameraRig.AimAmount = aim;
            look.SensitivityScale = Mathf.Lerp(1f, def.AdsSensitivity, aim);

            if (_flash != null)
            {
                _flashTimer -= dt;
                _flash.enabled = _flashTimer > 0f;
            }
        }

        /// <summary>
        /// Weapon-root pose (camera space) that puts the rear sight on the eye's line of sight. The line of sight is
        /// parallel to the weapon's +Z (sights of equal height); only the sight's position is used, never its
        /// rotation, because imported empties can carry an axis conversion.
        /// </summary>
        private Vector3 AdsPosition(WeaponDefinition def, out Quaternion rotation)
        {
            Transform sight = _model.Sight;
            Vector3 sightLocal = sight != null ? _model.transform.InverseTransformPoint(sight.position) : Vector3.up * 0.04f;
            rotation = Quaternion.identity;
            return new Vector3(0f, 0f, def.AdsSightDistance) - sightLocal;
        }

        /// <summary>Camera-space pose of the current hand action at its phase.</summary>
        private void ActionPose(WeaponDefinition def, out Vector3 position, out Quaternion rotation)
        {
            if (_actionKind == WeaponAction.Inspect)
            {
                // Left side up to 45%, turn over to the right side, hold, hand back.
                float turn = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 0.62f, _actionT));
                position = Vector3.Lerp(def.InspectLeftPosition, def.InspectRightPosition, turn);
                rotation = Quaternion.Slerp(Quaternion.Euler(def.InspectLeftEuler), Quaternion.Euler(def.InspectRightEuler), turn);
            }
            else
            {
                position = def.ChamberCheckPosition;
                rotation = Quaternion.Euler(def.ChamberCheckEuler);
            }
        }

        /// <summary>Slide travel of a press check: back, look, forward.</summary>
        private float ChamberCheckSlide(WeaponDefinition def)
        {
            if (_actionKind != WeaponAction.ChamberCheck || _action < 0.01f) return 0f;
            float open = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, 0.4f, _actionT))
                         * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.7f, 0.82f, _actionT)));
            return open * def.ChamberCheckSlide * _action;
        }

        private void AnimateParts(WeaponDefinition def, WeaponState state, float dt)
        {
            // Slide: blowback after each shot, locked back while nothing is chambered, press check.
            _slideKick = Mathf.Max(0f, _slideKick - dt / 0.07f);
            float slide = state.Chambered ? Mathf.Max(Mathf.Sin(_slideKick * Mathf.PI), ChamberCheckSlide(def)) : 1f;
            _safety = Damp(_safety, state.SafetyOn ? 1f : 0f, 25f, dt);
            _model.PoseSafety(_safety);

            // Magazine: old one drops out, new one comes up and seats at the insert point.
            float magazineOut = 0f;
            bool magazineVisible = true;
            if (state.IsReloading)
            {
                float t = state.ReloadElapsed / WeaponMotor.ReloadDuration(state, def.Stats);
                float insert = def.Stats.MagazineInsertPoint;
                if (t < 0.12f) magazineOut = 0f;
                else if (t < 0.3f) magazineOut = Mathf.InverseLerp(0.12f, 0.3f, t);
                else if (t < 0.42f) { magazineOut = 1f; magazineVisible = false; }
                else if (t < insert) magazineOut = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.42f, insert, t));
            }

            _triggerPull = Damp(_triggerPull, weapon.TriggerHeld && state.Lowered < 0.5f ? 1f : 0f, 30f, dt);
            _model.Pose(slide, magazineOut, magazineVisible, 1f, _triggerPull);
        }

        private void PlaceHands(WeaponDefinition def, WeaponState state, float dt)
        {
            _model.RightHand.SetLocalPositionAndRotation(def.RightHandPosition, def.RightHandRotation);
            _model.LeftHand.SetLocalPositionAndRotation(def.LeftHandPosition, def.LeftHandRotation);

            // During a reload the support hand follows the magazine, then returns to the grip.
            bool toMagazine = false;
            if (state.IsReloading && _model.MagazineGrab != null)
            {
                float t = state.ReloadElapsed / WeaponMotor.ReloadDuration(state, def.Stats);
                toMagazine = t > 0.05f && t < def.Stats.MagazineInsertPoint + 0.05f;
            }
            _leftToMagazine = Damp(_leftToMagazine, toMagazine ? 1f : 0f, 12f, dt);

            // Press check: the support hand pinches the slide serrations.
            float slideGrab = _actionKind == WeaponAction.ChamberCheck
                ? Mathf.SmoothStep(0f, 1f, _action) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.85f, 1f, _actionT)))
                : 0f;
            if (slideGrab > 0.001f)
            {
                Vector3 grabPosition = _model.transform.TransformPoint(def.SlideGrabPosition);
                if (_model.Slide != null) grabPosition += _model.transform.TransformVector(Vector3.back * (0.03f * ChamberCheckSlide(def)));
                _model.LeftHand.SetPositionAndRotation(
                    Vector3.Lerp(_model.LeftHand.position, grabPosition, slideGrab),
                    Quaternion.Slerp(_model.LeftHand.rotation, _model.transform.rotation * def.SlideGrabRotation, slideGrab));
            }
            if (_leftToMagazine > 0.001f)
            {
                // Position from the magazine point (it moves with the magazine); orientation in weapon space.
                Transform grab = _model.MagazineGrab;
                Quaternion grabRotation = _model.transform.rotation * def.MagazineGrabRotation;
                _model.LeftHand.SetPositionAndRotation(
                    Vector3.Lerp(_model.LeftHand.position, grab.position, _leftToMagazine),
                    Quaternion.Slerp(_model.LeftHand.rotation, grabRotation, _leftToMagazine));
            }

            // Sprinting with a pistol: one hand, the other arm swings with the animation.
            float sprintOneHanded = Mathf.SmoothStep(0f, 1f, _lowered) * (1f - _obstructed);
            // Inspecting is one-handed too: the support hand lets go while the weapon is turned over.
            float inspecting = _actionKind == WeaponAction.Inspect ? Mathf.SmoothStep(0f, 1f, _action) : 0f;
            hands.LeftWeight = (1f - sprintOneHanded) * (1f - inspecting);
            hands.Weight = 1f;
            hands.TriggerPull = _triggerPull;
            Transform weaponRoot = _model.transform;
            hands.RightThumbDirection = weaponRoot.TransformDirection(def.RightThumbForward);
            // While the support hand holds the magazine its thumb is left to the curl.
            hands.LeftThumbDirection = weaponRoot.TransformDirection(def.LeftThumbForward) * (1f - _leftToMagazine) * (1f - slideGrab);
        }

        private static void Spring(ref float x, ref float v, float stiffness, float damping, float dt)
        {
            float c = 2f * Mathf.Sqrt(stiffness) * damping;
            int steps = Mathf.CeilToInt(dt / 0.004f);
            float h = dt / Mathf.Max(steps, 1);
            for (int i = 0; i < steps; i++)
            {
                v += (-stiffness * x - c * v) * h;
                x += v * h;
            }
        }

        /// <summary>Spring toward a target (per-axis), sub-stepped for stability at any frame rate.</summary>
        private static void StepSpring(ref Vector2 x, ref Vector2 v, Vector2 target, float stiffness, float damping, float dt)
        {
            float c = 2f * Mathf.Sqrt(stiffness) * damping;
            int steps = Mathf.CeilToInt(dt / 0.004f);
            float h = dt / Mathf.Max(steps, 1);
            for (int i = 0; i < steps; i++)
            {
                v += ((target - x) * stiffness - c * v) * h;
                x += v * h;
            }
        }

        private static float Damp(float current, float target, float sharpness, float dt)
            => Mathf.Lerp(current, target, 1f - Mathf.Exp(-sharpness * dt));
    }
}
