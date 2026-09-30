using System;
using UnityEngine;

namespace Polykov.Weapons
{
    public enum FireMode : byte
    {
        Semi = 0,
        Auto = 1,
    }

    /// <summary>Gameplay-relevant weapon numbers. Plain data so the server and tests can use it without assets.</summary>
    [Serializable]
    public struct WeaponTuning
    {
        [Header("Fire")]
        public FireMode FireMode;
        [Tooltip("Maximum cyclic rate (rounds per minute). For semi-auto this caps how fast you can pull.")]
        [Min(1f)] public float RoundsPerMinute;
        [Min(0f)] public float Damage;
        [Min(1f)] public float Range;

        [Header("Ammunition")]
        [Min(1)] public int MagazineCapacity;
        [Min(0)] public int StartingReserve;

        [Header("Handling (seconds)")]
        [Min(0.01f)] public float EquipTime;
        [Min(0.01f)] public float HolsterTime;
        [Min(0.01f)] public float AimTime;
        [Tooltip("Delay after sprinting before the weapon can fire.")]
        [Min(0f)] public float SprintToFireTime;
        [Min(0.01f)] public float TacticalReloadTime;
        [Min(0.01f)] public float EmptyReloadTime;

        [Header("Accuracy (degrees, cone half-angle)")]
        public float HipSpread;
        public float AimSpread;
        [Tooltip("Added at full run speed.")]
        public float MovementSpread;
        public float AirborneSpread;
        [Tooltip("Added per shot, decays over time.")]
        public float ShotBloom;
        public float MaxBloom;
        [Tooltip("Bloom decay per second.")]
        public float BloomRecovery;

        public float FireInterval => 60f / RoundsPerMinute;

        public static WeaponTuning M1911 => new WeaponTuning
        {
            FireMode = FireMode.Semi,
            RoundsPerMinute = 450f,
            Damage = 55f,
            Range = 150f,
            MagazineCapacity = 7,
            StartingReserve = 35,
            EquipTime = 0.5f,
            HolsterTime = 0.35f,
            AimTime = 0.16f,
            SprintToFireTime = 0.18f,
            TacticalReloadTime = 1.75f,
            EmptyReloadTime = 2.2f,
            HipSpread = 1.6f,
            AimSpread = 0.15f,
            MovementSpread = 1.4f,
            AirborneSpread = 5f,
            ShotBloom = 0.6f,
            MaxBloom = 2.4f,
            BloomRecovery = 4f,
        };
    }
}
