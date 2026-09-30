using Polykov.Movement;
using Polykov.Player;
using UnityEngine;

namespace Polykov.CameraSystem
{
    /// <summary>
    /// Positions the first-person camera from the interpolated body position and current look angles.
    /// Adds subtle, speed-driven head motion. Runs after movement so it never lags a frame.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class FirstPersonCameraRig : MonoBehaviour
    {
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private PlayerLook look;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private CameraSettings settings;

        private float _bobPhase;
        private float _bobWeight;
        private float _tilt;
        private float _fov;

        private void Start()
        {
            _fov = settings.BaseFov;
            targetCamera.fieldOfView = _fov;
            targetCamera.nearClipPlane = settings.NearClip;
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            MovementState state = motor.State;
            MovementTuning tuning = motor.Tuning;
            float runSpeed = Mathf.Max(tuning.RunSpeed, 0.01f);
            float speed = state.PlanarSpeed;

            // Head bob weight follows speed smoothly; zero in the air.
            float targetWeight = state.Grounded ? Mathf.Clamp(speed / runSpeed, 0f, 1.6f) : 0f;
            _bobWeight = Damp(_bobWeight, targetWeight, settings.BobWeightSharpness, dt);
            _bobPhase += speed * dt / settings.StrideLength * Mathf.PI;
            if (_bobPhase > Mathf.PI * 2f) _bobPhase -= Mathf.PI * 2f;

            float w = _bobWeight * settings.Intensity;
            float vertical = (Mathf.Cos(_bobPhase * 2f) - 1f) * 0.5f * settings.VerticalAmplitude * w;
            float lateral = Mathf.Sin(_bobPhase) * settings.LateralAmplitude * w;
            float roll = Mathf.Sin(_bobPhase) * settings.RollAmplitude * w;

            // Strafe tilt: lean slightly against lateral velocity.
            Quaternion yawRotation = Quaternion.Euler(0f, look.Yaw, 0f);
            float localLateral = Vector3.Dot(state.PlanarVelocity, yawRotation * Vector3.right);
            float targetTilt = -localLateral / runSpeed * settings.StrafeTilt * settings.Intensity;
            _tilt = Damp(_tilt, targetTilt, settings.TiltSharpness, dt);

            Quaternion viewRotation = Quaternion.Euler(look.Pitch, look.Yaw, _tilt + roll);
            Vector3 neck = motor.InterpolatedPosition + Vector3.up * settings.NeckHeight;
            Vector3 eye = neck + viewRotation * new Vector3(0f, settings.EyeOffset.x, settings.EyeOffset.y);
            eye += yawRotation * new Vector3(lateral, vertical, 0f);

            transform.SetPositionAndRotation(eye, viewRotation);

            float targetFov = settings.BaseFov + (state.Locomotion == LocomotionState.Sprint ? settings.SprintFovBoost : 0f);
            _fov = Damp(_fov, targetFov, settings.FovSharpness, dt);
            targetCamera.fieldOfView = _fov;
        }

        /// <summary>Frame-rate independent exponential smoothing.</summary>
        private static float Damp(float current, float target, float sharpness, float dt)
            => Mathf.Lerp(current, target, 1f - Mathf.Exp(-sharpness * dt));
    }
}
