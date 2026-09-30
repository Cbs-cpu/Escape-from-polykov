using UnityEngine;

namespace Polykov.Weapons
{
    /// <summary>
    /// Presentation-only data for a weapon in first person: poses, feel, part motion, hands and effects.
    /// Positions are in camera space (meters), rotations are euler degrees.
    /// </summary>
    [CreateAssetMenu(menuName = "Polykov/Weapon View Data", fileName = "WeaponViewData")]
    public sealed class WeaponViewData : ScriptableObject
    {
        [Header("Poses (camera space)")]
        public Vector3 HipPosition = new Vector3(0.10f, -0.125f, 0.33f);
        public Vector3 HipEuler = new Vector3(0f, -2f, 0f);
        [Tooltip("Distance from the eye to the rear sight when aiming.")]
        public float AimDistance = 0.36f;
        public float AimFov = 64f;
        public Vector3 SprintPosition = new Vector3(0.05f, -0.20f, 0.22f);
        public Vector3 SprintEuler = new Vector3(38f, -28f, 12f);
        public Vector3 EquipPosition = new Vector3(0.10f, -0.42f, 0.14f);
        public Vector3 EquipEuler = new Vector3(70f, -10f, 20f);
        public Vector3 ReloadOffset = new Vector3(-0.025f, 0.035f, -0.07f);
        public Vector3 ReloadEuler = new Vector3(-10f, 12f, -32f);
        public Vector3 BlockedPosition = new Vector3(0.07f, -0.16f, 0.14f);
        public Vector3 BlockedEuler = new Vector3(-12f, -35f, 8f);

        [Header("Sway (mouse)")]
        public float SwayRotation = 1.4f;
        public float SwayMaxDegrees = 6f;
        public float SwayPosition = 0.0018f;
        public float SwayStiffness = 110f;
        [Range(0.1f, 1.5f)] public float SwayDamping = 0.62f;
        [Range(0f, 1f)] public float AimSwayMultiplier = 0.35f;

        [Header("Bob (movement)")]
        public float StrideLength = 0.75f;
        public Vector2 BobPosition = new Vector2(0.007f, 0.006f);
        public float BobRoll = 1.4f;
        [Range(0f, 1f)] public float AimBobMultiplier = 0.22f;
        public float SprintBobMultiplier = 1.8f;
        public float BreathAmplitude = 0.0012f;
        public float BreathRate = 0.28f;

        [Header("Pose smoothing")]
        public float SprintBlendSharpness = 9f;
        public float BlockedBlendSharpness = 14f;
        public float BlockCheckDistance = 0.6f;

        [Header("Mechanics (weapon space)")]
        public float SlideTravel = 0.034f;
        public float SlideBackTime = 0.018f;
        public float SlideReturnTime = 0.055f;
        [Range(0f, 1f)] public float BarrelTravelRatio = 0.3f;
        public float HammerFallDegrees = 55f;
        public float TriggerTravel = 0.0045f;
        public float SlideStopLockedDegrees = 9f;
        public float MagazineInsertDistance = 0.09f;
        public float MagazineInsertTime = 0.12f;

        [Header("Hands (weapon space, relative to the grip socket)")]
        public Vector3 RightWristOffset = new Vector3(0.012f, 0.018f, -0.058f);
        public Vector3 RightFingersDirection = new Vector3(-0.25f, -0.30f, 0.92f);
        public Vector3 RightPalmNormal = new Vector3(-0.95f, 0.10f, -0.10f);
        public Vector3 LeftWristOffset = new Vector3(-0.045f, -0.022f, -0.032f);
        public Vector3 LeftFingersDirection = new Vector3(0.45f, -0.35f, 0.82f);
        public Vector3 LeftPalmNormal = new Vector3(0.80f, 0.45f, 0.10f);
        [Tooltip("Finger curl in degrees (proximal, intermediate, distal).")]
        public Vector3 RightGripCurl = new Vector3(70f, 85f, 55f);
        public Vector3 RightIndexCurl = new Vector3(30f, 45f, 30f);
        public Vector3 LeftGripCurl = new Vector3(60f, 70f, 45f);
        public Vector3 ThumbCurl = new Vector3(10f, 15f, 10f);
        public Vector3 RightElbowHint = new Vector3(0.30f, -0.35f, -0.10f);
        public Vector3 LeftElbowHint = new Vector3(-0.30f, -0.40f, -0.10f);

        [Header("Effects")]
        public GameObject MuzzleFlash;
        public GameObject ImpactConcrete;
        public GameObject ImpactMetal;
        public GameObject BulletHole;
        public float BulletHoleSize = 0.035f;
        [Tooltip("Impulse applied to rigidbodies that get hit.")]
        public float HitForce = 6f;

        [Header("Shell ejection (weapon space velocity, m/s)")]
        public Vector3 EjectVelocity = new Vector3(1.9f, 1.6f, -0.35f);
        public float EjectRandomness = 0.35f;
        public float EjectSpin = 30f;
    }
}
