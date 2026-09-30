using Polykov.Animation;
using Polykov.Core;
using Polykov.Movement;
using Polykov.Player;
using UnityEngine;

namespace Polykov.CameraSystem
{
    /// <summary>
    /// Positions the first-person camera. With an animated body it rides the head bone (filtered so the
    /// animation bob stays comfortable); without one it uses a neck pivot plus procedural bob.
    /// Adds lean roll and a landing dip. Runs after movement and body posing so it never lags a frame.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class FirstPersonCameraRig : MonoBehaviour
    {
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private PlayerLook look;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private CameraSettings settings;
        [Tooltip("Optional. When set, the camera follows the animated head and applies lean.")]
        [SerializeField] private ProceduralBody body;

        private float _bobPhase;
        private float _bobWeight;
        private float _tilt;
        private float _fov;
        private Vector3 _headLocal;
        private float _headBaseY;
        private bool _headInitialized;
        private float _dip;
        private float _dipVelocity;

        private void OnEnable() => motor.Landed += OnLanded;
        private void OnDisable() => motor.Landed -= OnLanded;

        private void Start()
        {
            _fov = UserSettings.Fov;
            targetCamera.fieldOfView = _fov;
            targetCamera.nearClipPlane = settings.NearClip;
        }

        private void OnLanded(float impact)
        {
            _dipVelocity -= Mathf.Min(impact * settings.LandingDipPerSpeed, settings.MaxLandingDip)
                            * settings.LandingSpringStiffness * 0.08f * UserSettings.CameraShake;
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            MovementState state = motor.State;
            MovementTuning tuning = motor.Tuning;
            float runSpeed = Mathf.Max(tuning.RunSpeed, 0.01f);
            Quaternion yawRotation = Quaternion.Euler(0f, look.Yaw, 0f);

            // Strafe tilt: lean slightly against lateral velocity.
            float localLateral = Vector3.Dot(state.PlanarVelocity, yawRotation * Vector3.right);
            float comfort = settings.Intensity * UserSettings.HeadBob;
            float targetTilt = -localLateral / runSpeed * settings.StrafeTilt * comfort;
            _tilt = Damp(_tilt, targetTilt, settings.TiltSharpness, dt);

            float leanRoll = body != null ? -body.EffectiveLean * settings.LeanRoll : 0f;
            UpdateLandingSpring(dt);

            Vector3 eye;
            float roll;
            Transform head = body != null ? body.Head : null;
            if (head != null)
            {
                Quaternion viewRotation = Quaternion.Euler(look.Pitch, look.Yaw, _tilt + leanRoll);
                Vector3 local = head.position - motor.InterpolatedPosition;
                if (!_headInitialized)
                {
                    _headLocal = local;
                    _headBaseY = local.y;
                    _headInitialized = true;
                }
                _headLocal = Vector3.Lerp(_headLocal, local, 1f - Mathf.Exp(-settings.HeadFollowSharpness * dt));
                // Slow-moving baseline keeps posture changes; fast bob is attenuated.
                _headBaseY = Damp(_headBaseY, _headLocal.y, 3f, dt);
                Vector3 filtered = _headLocal;
                filtered.y = _headBaseY + (_headLocal.y - _headBaseY) * settings.AnimatedBobAmount * UserSettings.HeadBob;
                eye = motor.InterpolatedPosition + filtered
                      + viewRotation * new Vector3(0f, settings.HeadBoneEyeOffset.x, settings.HeadBoneEyeOffset.y);
                roll = _tilt + leanRoll;
            }
            else
            {
                float speed = state.PlanarSpeed;
                float targetWeight = state.Grounded ? Mathf.Clamp(speed / runSpeed, 0f, 1.6f) : 0f;
                _bobWeight = Damp(_bobWeight, targetWeight, settings.BobWeightSharpness, dt);
                _bobPhase += speed * dt / settings.StrideLength * Mathf.PI;
                if (_bobPhase > Mathf.PI * 2f) _bobPhase -= Mathf.PI * 2f;
                float w = _bobWeight * comfort;
                float vertical = (Mathf.Cos(_bobPhase * 2f) - 1f) * 0.5f * settings.VerticalAmplitude * w;
                float lateral = Mathf.Sin(_bobPhase) * settings.LateralAmplitude * w;
                roll = _tilt + leanRoll + Mathf.Sin(_bobPhase) * settings.RollAmplitude * w;

                Quaternion pivotRotation = Quaternion.Euler(look.Pitch, look.Yaw, roll);
                Vector3 neck = motor.InterpolatedPosition + Vector3.up * settings.NeckHeight;
                eye = neck + pivotRotation * new Vector3(0f, settings.EyeOffset.x, settings.EyeOffset.y);
                eye += yawRotation * new Vector3(lateral, vertical, 0f);
            }

            eye += Vector3.up * _dip;
            transform.SetPositionAndRotation(eye, Quaternion.Euler(look.Pitch, look.Yaw, roll));

            float targetFov = UserSettings.Fov + (state.Locomotion == LocomotionState.Sprint ? settings.SprintFovBoost : 0f);
            _fov = Damp(_fov, targetFov, settings.FovSharpness, dt);
            targetCamera.fieldOfView = _fov;
        }

        private void UpdateLandingSpring(float dt)
        {
            float k = settings.LandingSpringStiffness;
            float c = 2f * Mathf.Sqrt(k) * settings.LandingSpringDamping;
            // Semi-implicit Euler, sub-stepped for stability at low frame rates.
            int steps = Mathf.CeilToInt(dt / 0.005f);
            float h = dt / Mathf.Max(steps, 1);
            for (int i = 0; i < steps; i++)
            {
                _dipVelocity += (-k * _dip - c * _dipVelocity) * h;
                _dip += _dipVelocity * h;
            }
            _dip = Mathf.Max(_dip, -settings.MaxLandingDip);
        }

        /// <summary>Frame-rate independent exponential smoothing.</summary>
        private static float Damp(float current, float target, float sharpness, float dt)
            => Mathf.Lerp(current, target, 1f - Mathf.Exp(-sharpness * dt));
    }
}
