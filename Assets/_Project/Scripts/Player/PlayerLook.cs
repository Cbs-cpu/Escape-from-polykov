using Polykov.Core;
using Polykov.Input;
using UnityEngine;

namespace Polykov.Player
{
    /// <summary>
    /// Owns view angles. Yaw rotates the body every frame (zero latency); pitch is read by the camera rig.
    /// Yaw is also sampled by the motor each tick and will be sent to the server as part of the input.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class PlayerLook : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader input;

        [Header("Sensitivity")]
        [Tooltip("Degrees per second at full stick deflection (x = yaw, y = pitch). Mouse sensitivity and " +
                 "invert Y are player preferences (UserSettings, pause menu).")]
        [SerializeField] private Vector2 stickSensitivity = new Vector2(220f, 150f);

        [Header("Limits")]
        [SerializeField, Range(-89f, 0f)] private float minPitch = -85f;
        [SerializeField, Range(0f, 89f)] private float maxPitch = 85f;

        /// <summary>World yaw in degrees [0, 360).</summary>
        public float Yaw { get; private set; }
        /// <summary>Pitch in degrees, positive looks down (Unity convention).</summary>
        public float Pitch { get; private set; }
        /// <summary>Look rotation applied this frame (degrees, x = yaw, y = pitch up). Drives weapon sway.</summary>
        public Vector2 LastDelta { get; private set; }

        /// <summary>Permanent aim change from recoil (degrees; positive pitchUp raises the view).</summary>
        public void AddRecoil(float pitchUp, float yaw)
        {
            Pitch = Mathf.Clamp(Pitch - pitchUp, minPitch, maxPitch);
            Yaw = Mathf.Repeat(Yaw + yaw, 360f);
        }

        /// <summary>Multiplier applied to look input (e.g. lower while aiming down sights). Set by other systems.</summary>
        public float SensitivityScale { get; set; } = 1f;

        /// <summary>Adds rotation not caused by input (recoil). Positive pitch looks down.</summary>
        public void AddRotation(float yawDegrees, float pitchDegrees)
        {
            Yaw = Mathf.Repeat(Yaw + yawDegrees, 360f);
            Pitch = Mathf.Clamp(Pitch + pitchDegrees, minPitch, maxPitch);
        }

        private void Awake()
        {
            Yaw = transform.eulerAngles.y;
        }

        private void Update()
        {
            Vector2 delta = input.ReadLookDelta(UserSettings.MouseSensitivity, stickSensitivity, Time.deltaTime)
                            * SensitivityScale;
            LastDelta = delta;
            Yaw = Mathf.Repeat(Yaw + delta.x, 360f);
            Pitch = Mathf.Clamp(Pitch - delta.y * (UserSettings.InvertY ? -1f : 1f), minPitch, maxPitch);
            transform.rotation = Quaternion.Euler(0f, Yaw, 0f);
        }
    }
}
