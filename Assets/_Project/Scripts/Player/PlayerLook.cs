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
        [Tooltip("Degrees per mouse pixel.")]
        [SerializeField, Range(0.005f, 0.5f)] private float mouseSensitivity = 0.08f;
        [Tooltip("Degrees per second at full stick deflection (x = yaw, y = pitch).")]
        [SerializeField] private Vector2 stickSensitivity = new Vector2(220f, 150f);
        [SerializeField] private bool invertY;

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

        private void Awake()
        {
            Yaw = transform.eulerAngles.y;
        }

        private void Update()
        {
            Vector2 delta = input.ReadLookDelta(mouseSensitivity, stickSensitivity, Time.deltaTime);
            LastDelta = delta;
            Yaw = Mathf.Repeat(Yaw + delta.x, 360f);
            Pitch = Mathf.Clamp(Pitch - delta.y * (invertY ? -1f : 1f), minPitch, maxPitch);
            transform.rotation = Quaternion.Euler(0f, Yaw, 0f);
        }
    }
}
