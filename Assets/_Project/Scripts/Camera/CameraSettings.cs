using UnityEngine;

namespace Polykov.CameraSystem
{
    /// <summary>First-person camera feel. All values are local-only presentation, never replicated.</summary>
    [CreateAssetMenu(menuName = "Polykov/Camera Settings", fileName = "CameraSettings")]
    public sealed class CameraSettings : ScriptableObject
    {
        [Header("Head")]
        [Tooltip("Neck pivot height above the character's feet.")]
        public float NeckHeight = 1.52f;
        [Tooltip("Eye offset from the neck pivot (up, forward). Looking down moves the eyes forward like a real head.")]
        public Vector2 EyeOffset = new Vector2(0.11f, 0.09f);

        [Header("Field of view")]
        [Range(60f, 110f)] public float BaseFov = 78f;
        [Range(0f, 15f)] public float SprintFovBoost = 4f;
        [Min(0.1f)] public float FovSharpness = 6f;
        [Range(0.01f, 0.3f)] public float NearClip = 0.05f;

        [Header("Head bob")]
        [Tooltip("Global multiplier (accessibility). 0 disables bob and tilt.")]
        [Range(0f, 1f)] public float Intensity = 1f;
        [Tooltip("Meters travelled per footstep.")]
        [Min(0.1f)] public float StrideLength = 0.75f;
        [Tooltip("Vertical bob amplitude at run speed (meters).")]
        [Range(0f, 0.08f)] public float VerticalAmplitude = 0.018f;
        [Tooltip("Lateral sway amplitude at run speed (meters).")]
        [Range(0f, 0.08f)] public float LateralAmplitude = 0.012f;
        [Tooltip("Roll sway at run speed (degrees).")]
        [Range(0f, 3f)] public float RollAmplitude = 0.35f;
        [Min(0.1f)] public float BobWeightSharpness = 8f;

        [Header("Strafe tilt")]
        [Range(0f, 4f)] public float StrafeTilt = 0.8f;
        [Min(0.1f)] public float TiltSharpness = 7f;
    }
}
