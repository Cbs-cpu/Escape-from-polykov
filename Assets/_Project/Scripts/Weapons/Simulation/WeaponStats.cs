using System;
using UnityEngine;

namespace Polykov.Weapons
{
    public enum FireMode : byte
    {
        /// <summary>One shot per trigger press.</summary>
        Semi = 0,
        /// <summary>Fires while the trigger is held.</summary>
        Auto = 1,
    }

    /// <summary>Gameplay numbers of a weapon. Plain data: shared by client prediction, server and tests.</summary>
    [Serializable]
    public struct WeaponStats
    {
        [Header("Fire")]
        public FireMode FireMode;
        [Tooltip("Mechanical cyclic rate cap (rounds per minute).")]
        [Min(1f)] public float RoundsPerMinute;
        [Min(1)] public int MagazineCapacity;
        [Tooltip("Can hold a round in the chamber on top of a full magazine (closed-bolt / pistol).")]
        public bool ChambersRound;

        [Header("Reload (seconds)")]
        [Tooltip("Magazine swap with a round still chambered.")]
        [Min(0.05f)] public float TacticalReloadTime;
        [Tooltip("Magazine swap plus releasing the slide/bolt on an empty weapon.")]
        [Min(0.05f)] public float EmptyReloadTime;
        [Tooltip("Fraction of the reload after which the new magazine is seated (ammo counts change).")]
        [Range(0.1f, 1f)] public float MagazineInsertPoint;

        [Header("Handling (seconds)")]
        [Tooltip("Seconds from hip to fully aimed down sights.")]
        [Min(0.01f)] public float AimTime;
        [Tooltip("Seconds to lower/raise the weapon (sprint, obstruction). Can't fire until it is raised.")]
        [Min(0.01f)] public float ReadyTime;

        [Header("Accuracy (degrees)")]
        [Tooltip("Random cone half-angle when firing from the hip.")]
        [Min(0f)] public float HipSpread;
        [Tooltip("Random cone half-angle when fully aimed.")]
        [Min(0f)] public float AimSpread;

        [Header("Recoil (degrees per shot)")]
        [Tooltip("Camera pitch kick per shot (up).")]
        [Min(0f)] public float VerticalRecoil;
        [Tooltip("Max camera yaw kick per shot (random left/right).")]
        [Min(0f)] public float HorizontalRecoil;
        [Tooltip("Random variation of the vertical kick (0..1).")]
        [Range(0f, 1f)] public float RecoilVariance;
        [Tooltip("Multiplier of recoil while aiming down sights.")]
        [Range(0f, 1f)] public float AimRecoilMultiplier;

        public float FireInterval => 60f / RoundsPerMinute;

        /// <summary>Colt M1911A1 (.45 ACP): 7-round magazine, one in the chamber.</summary>
        public static WeaponStats M1911 => new WeaponStats
        {
            FireMode = FireMode.Semi,
            RoundsPerMinute = 450f,
            MagazineCapacity = 7,
            ChambersRound = true,
            TacticalReloadTime = 1.7f,
            EmptyReloadTime = 2.2f,
            MagazineInsertPoint = 0.55f,
            AimTime = 0.2f,
            ReadyTime = 0.2f,
            HipSpread = 1.6f,
            AimSpread = 0.15f,
            VerticalRecoil = 2.6f,
            HorizontalRecoil = 0.9f,
            RecoilVariance = 0.3f,
            AimRecoilMultiplier = 0.75f,
        };
    }
}
