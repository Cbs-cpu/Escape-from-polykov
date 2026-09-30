using UnityEngine;

namespace Polykov.Weapons
{
    /// <summary>Static definition of a weapon (identity + gameplay numbers + recoil). Never holds runtime state.</summary>
    [CreateAssetMenu(menuName = "Polykov/Weapon Data", fileName = "WeaponData")]
    public sealed class WeaponData : ScriptableObject
    {
        public string Id = "m1911";
        public string DisplayName = "M1911";
        public WeaponTuning Tuning = WeaponTuning.M1911;
        public RecoilTuning Recoil = RecoilTuning.M1911;
    }

    /// <summary>Recoil profile: what each shot does to the view and to the weapon model.</summary>
    [System.Serializable]
    public struct RecoilTuning
    {
        [Header("Camera (degrees)")]
        public float CameraPitch;
        public float CameraPitchVariance;
        public float CameraYaw;
        public float CameraRoll;
        [Tooltip("Fraction of the vertical kick that permanently moves your aim (the rest springs back).")]
        [Range(0f, 1f)] public float PermanentFraction;
        [Tooltip("Multiplier on all recoil while aiming down sights.")]
        [Range(0f, 1f)] public float AimMultiplier;
        [Min(1f)] public float CameraStiffness;
        [Range(0.1f, 1.5f)] public float CameraDamping;
        [Tooltip("Quick FOV punch per shot (degrees).")]
        public float FovPunch;

        [Header("Weapon model")]
        [Tooltip("Backward kick (meters).")]
        public float KickBack;
        public float KickUp;
        [Tooltip("Muzzle flip (degrees).")]
        public float KickPitch;
        public float KickYaw;
        public float KickRoll;
        [Min(1f)] public float WeaponStiffness;
        [Range(0.1f, 1.5f)] public float WeaponDamping;

        public static RecoilTuning M1911 => new RecoilTuning
        {
            CameraPitch = 2.6f,
            CameraPitchVariance = 0.5f,
            CameraYaw = 0.7f,
            CameraRoll = 1.2f,
            PermanentFraction = 0.3f,
            AimMultiplier = 0.75f,
            CameraStiffness = 220f,
            CameraDamping = 0.7f,
            FovPunch = 1.4f,
            KickBack = 0.045f,
            KickUp = 0.012f,
            KickPitch = 11f,
            KickYaw = 2.5f,
            KickRoll = 5f,
            WeaponStiffness = 420f,
            WeaponDamping = 0.55f,
        };
    }
}
