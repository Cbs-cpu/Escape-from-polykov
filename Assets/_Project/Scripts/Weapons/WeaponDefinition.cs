using UnityEngine;

namespace Polykov.Weapons
{
    /// <summary>
    /// Static data of a weapon: gameplay stats (shared with the simulation) plus how it is held and how it feels.
    /// Poses are relative to the first-person camera (x right, y up, z forward, meters / degrees).
    /// Edit in Play Mode to tune live.
    /// </summary>
    [CreateAssetMenu(menuName = "Polykov/Weapon Definition", fileName = "WeaponDefinition")]
    public sealed class WeaponDefinition : ScriptableObject
    {
        public string DisplayName = "M1911A1";
        public WeaponStats Stats = WeaponStats.M1911;
        [Min(0)] public int StartingReserve = 28;

        [Header("Hit")]
        [Min(1f)] public float Range = 150f;
        [Tooltip("Impulse applied to rigidbodies that are hit.")]
        [Min(0f)] public float ImpactForce = 4f;
        [Min(0f)] public float Damage = 35f;

        [Header("Poses (camera space)")]
        public Vector3 HipPosition = new Vector3(0.12f, -0.19f, 0.36f);
        public Vector3 HipEuler = new Vector3(0f, -2f, 0f);
        [Tooltip("Distance from the eye to the rear sight when aiming down sights.")]
        [Range(0.15f, 0.7f)] public float AdsSightDistance = 0.4f;
        [Tooltip("Sprinting: weapon lowered and turned in.")]
        public Vector3 LoweredPosition = new Vector3(0.1f, -0.36f, 0.26f);
        public Vector3 LoweredEuler = new Vector3(45f, -25f, -10f);
        [Tooltip("Muzzle blocked by a wall: weapon pulled back, muzzle up.")]
        public Vector3 ObstructedPosition = new Vector3(0.1f, -0.13f, 0.2f);
        public Vector3 ObstructedEuler = new Vector3(-55f, -10f, 0f);
        [Tooltip("Reloading: weapon brought in and canted to show the magazine well.")]
        public Vector3 ReloadPosition = new Vector3(0.05f, -0.23f, 0.3f);
        public Vector3 ReloadEuler = new Vector3(-12f, -28f, 38f);

        [Header("Hands (weapon space; wrist position + wrist->knuckles / little->index directions)")]
        [Tooltip("Verified on the Operator rig with ArtSource/Tools/grip_preview.py (same IK math).")]
        public Vector3 RightHandPosition = new Vector3(0.032f, -0.056f, -0.118f);
        public Vector3 RightHandForward = new Vector3(-0.12f, 0.05f, 1f);
        public Vector3 RightHandUp = new Vector3(0f, 0.95f, 0.31f);
        public Vector3 LeftHandPosition = new Vector3(-0.036f, -0.08f, -0.07f);
        public Vector3 LeftHandForward = new Vector3(0.45f, 0.3f, 1f);
        public Vector3 LeftHandUp = new Vector3(0.1f, 1f, 0.25f);
        [Tooltip("Thumbs-forward grip: direction each thumb points along (weapon space).")]
        public Vector3 RightThumbForward = new Vector3(0f, -0.05f, 1f);
        public Vector3 LeftThumbForward = new Vector3(0f, -0.15f, 1f);

        [Header("Feel")]
        [Tooltip("Weapon rotation lag per degree of camera turn.")]
        [Range(0f, 1f)] public float SwayAmount = 0.35f;
        [Range(0f, 15f)] public float MaxSway = 6f;
        [Min(1f)] public float SwaySharpness = 10f;
        [Tooltip("Positional bob while moving (m at run speed).")]
        [Range(0f, 0.05f)] public float BobAmount = 0.012f;
        [Tooltip("Sway and bob kept while aiming down sights.")]
        [Range(0f, 1f)] public float AdsSwayMultiplier = 0.25f;
        [Tooltip("Obstruction probe length from the eye (m).")]
        [Range(0.2f, 1.2f)] public float Length = 0.58f;

        [Header("Recoil (presentation)")]
        [Tooltip("Weapon kick back per shot (m/s impulse).")]
        [Range(0f, 3f)] public float KickBack = 1.1f;
        [Tooltip("Weapon muzzle rise per shot (deg/s impulse).")]
        [Range(0f, 900f)] public float KickRotation = 420f;
        [Min(1f)] public float KickSpring = 260f;
        [Range(0.1f, 1.5f)] public float KickDamping = 0.8f;
        [Tooltip("Fraction of the camera recoil that returns to the point of aim.")]
        [Range(0f, 1f)] public float CameraRecoilReturn = 0.7f;
        [Tooltip("Seconds for the camera kick to be applied.")]
        [Range(0.01f, 0.2f)] public float CameraKickTime = 0.05f;
        [Tooltip("Speed of the camera return (1/s).")]
        [Min(0.1f)] public float CameraReturnSpeed = 7f;

        [Header("Aim down sights")]
        [Range(0f, 30f)] public float AdsFovReduction = 10f;
        [Range(0.2f, 1f)] public float AdsSensitivity = 0.8f;

        public Quaternion RightHandRotation => Quaternion.LookRotation(RightHandForward, RightHandUp);
        public Quaternion LeftHandRotation => Quaternion.LookRotation(LeftHandForward, LeftHandUp);
    }
}
