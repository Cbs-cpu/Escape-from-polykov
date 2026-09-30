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
                var go = new GameObject("MuzzleFlash");
                go.transform.SetParent(_model.Muzzle, false);
                go.transform.localPosition = new Vector3(0f, 0f, 0.03f);
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

            // Sway: the weapon lags camera turns a little.
            float yawRate = Mathf.DeltaAngle(_lastYaw, look.Yaw) / dt;
            float pitchRate = (look.Pitch - _lastPitch) / dt;
            _lastYaw = look.Yaw;
            _lastPitch = look.Pitch;
            var swayTarget = new Vector2(-pitchRate, -yawRate) * (def.SwayAmount * 0.02f * steadiness);
            swayTarget = Vector2.ClampMagnitude(swayTarget, def.MaxSway);
            _sway = Vector2.Lerp(_sway, swayTarget, 1f - Mathf.Exp(-def.SwaySharpness * dt));

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

            // Recoil spring (critically-damped-ish).
            Spring(ref _kickPos, ref _kickPosVelocity, def.KickSpring, def.KickDamping, dt);
            Spring(ref _kickRot, ref _kickRotVelocity, def.KickSpring, def.KickDamping, dt);
            position += rotation * new Vector3(0f, 0f, _kickPos);
            rotation *= Quaternion.Euler(_kickRot + _sway.x, _sway.y, _sway.y * 0.6f);

            _model.transform.SetPositionAndRotation(_eye.position + _eye.rotation * position, _eye.rotation * rotation);

            AnimateParts(def, state, dt);
            PlaceHands(def, state, dt);

            cameraRig.ZoomFov = def.AdsFovReduction * aim;
            look.SensitivityScale = Mathf.Lerp(1f, def.AdsSensitivity, aim);

            if (_flash != null)
            {
                _flashTimer -= dt;
                _flash.enabled = _flashTimer > 0f;
            }
        }

        /// <summary>Weapon-root pose (camera space) that puts the rear sight on the eye's line of sight.</summary>
        private Vector3 AdsPosition(WeaponDefinition def, out Quaternion rotation)
        {
            Transform sight = _model.Sight;
            Vector3 sightLocal = sight != null ? _model.transform.InverseTransformPoint(sight.position) : Vector3.up * 0.04f;
            Quaternion sightRotation = sight != null ? Quaternion.Inverse(_model.transform.rotation) * sight.rotation : Quaternion.identity;
            rotation = Quaternion.Inverse(sightRotation);
            return new Vector3(0f, 0f, def.AdsSightDistance) - rotation * sightLocal;
        }

        private void AnimateParts(WeaponDefinition def, WeaponState state, float dt)
        {
            // Slide: blowback after each shot, locked back while nothing is chambered.
            _slideKick = Mathf.Max(0f, _slideKick - dt / 0.07f);
            float slide = state.Chambered ? Mathf.Sin(_slideKick * Mathf.PI) : 1f;

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
            if (_leftToMagazine > 0.001f)
            {
                Transform grab = _model.MagazineGrab;
                _model.LeftHand.SetPositionAndRotation(
                    Vector3.Lerp(_model.LeftHand.position, grab.position, _leftToMagazine),
                    Quaternion.Slerp(_model.LeftHand.rotation, grab.rotation, _leftToMagazine));
            }

            // Sprinting with a pistol: one hand, the other arm swings with the animation.
            float sprintOneHanded = Mathf.SmoothStep(0f, 1f, _lowered) * (1f - _obstructed);
            hands.LeftWeight = 1f - sprintOneHanded;
            hands.Weight = 1f;
            hands.TriggerPull = _triggerPull;
            Transform weaponRoot = _model.transform;
            hands.RightThumbDirection = weaponRoot.TransformDirection(def.RightThumbForward);
            // While the support hand holds the magazine its thumb is left to the curl.
            hands.LeftThumbDirection = weaponRoot.TransformDirection(def.LeftThumbForward) * (1f - _leftToMagazine);
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

        private static float Damp(float current, float target, float sharpness, float dt)
            => Mathf.Lerp(current, target, 1f - Mathf.Exp(-sharpness * dt));
    }
}
