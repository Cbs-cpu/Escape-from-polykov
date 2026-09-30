using UnityEngine;

namespace Polykov.CameraSystem
{
    /// <summary>First-person camera feel. All values are local-only presentation, never replicated.</summary>
    [CreateAssetMenu(menuName = "Polykov/Camera Settings", fileName = "CameraSettings")]
    public sealed class CameraSettings : ScriptableObject
    {
        [Header("Head (no body fallback)")]
        [Tooltip("Neck pivot height above the character's feet, used when there is no animated head.")]
        public float NeckHeight = 1.52f;
        [Tooltip("Eye offset from the neck pivot (up, forward). Looking down moves the eyes forward like a real head.")]
        public Vector2 EyeOffset = new Vector2(0.11f, 0.09f);

        [Header("Head (animated body)")]
        [Tooltip("Eye offset from the head bone (up, forward), rotated by the view.")]
        public Vector2 HeadBoneEyeOffset = new Vector2(0.105f, 0.07f);
        [Tooltip("How much of the animation's vertical head bob reaches the camera (0 = none, 1 = all).")]
        [Range(0f, 1f)] public float AnimatedBobAmount = 0.45f;
        [Tooltip("Smoothing of the head position (higher = tighter).")]
        [Min(1f)] public float HeadFollowSharpness = 22f;

        [Header("Field of view (base FOV is a player preference: UserSettings.Fov)")]
        [Range(0f, 15f)] public float SprintFovBoost = 4f;
        [Min(0.1f)] public float FovSharpness = 6f;
        [Range(0.01f, 0.3f)] public float NearClip = 0.05f;

        [Header("Procedural head bob (no body fallback)")]
        [Tooltip("Designer multiplier for bob and tilt; the player's comfort slider (UserSettings.HeadBob) multiplies it.")]
        [Range(0f, 1f)] public float Intensity = 1f;
        [Tooltip("Meters travelled per footstep.")]
        [Min(0.1f)] public float StrideLength = 0.75f;
        [Range(0f, 0.08f)] public float VerticalAmplitude = 0.018f;
        [Range(0f, 0.08f)] public float LateralAmplitude = 0.012f;
        [Range(0f, 3f)] public float RollAmplitude = 0.35f;
        [Min(0.1f)] public float BobWeightSharpness = 8f;

        [Header("Strafe tilt")]
        [Range(0f, 4f)] public float StrafeTilt = 0.8f;
        [Min(0.1f)] public float TiltSharpness = 7f;

        [Header("Lean")]
        [Tooltip("Camera roll at full lean (degrees).")]
        [Range(0f, 25f)] public float LeanRoll = 10f;

        [Header("Landing")]
        [Tooltip("Camera dip per m/s of landing speed (meters).")]
        [Range(0f, 0.05f)] public float LandingDipPerSpeed = 0.012f;
        [Range(0f, 0.3f)] public float MaxLandingDip = 0.12f;
        [Min(1f)] public float LandingSpringStiffness = 140f;
        [Range(0.1f, 1.5f)] public float LandingSpringDamping = 0.75f;
    }
}
