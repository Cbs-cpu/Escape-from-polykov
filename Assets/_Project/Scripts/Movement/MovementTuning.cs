using System;
using UnityEngine;

namespace Polykov.Movement
{
    /// <summary>All game-feel parameters of the motor. Plain data so tests and the server can use it without assets.</summary>
    [Serializable]
    public struct MovementTuning
    {
        [Header("Speeds (m/s)")]
        [Min(0f)] public float WalkSpeed;
        [Min(0f)] public float RunSpeed;
        [Min(0f)] public float SprintSpeed;

        [Header("Direction")]
        [Tooltip("Speed multiplier when moving purely sideways.")]
        [Range(0f, 1f)] public float StrafeMultiplier;
        [Tooltip("Speed multiplier when moving purely backwards.")]
        [Range(0f, 1f)] public float BackwardMultiplier;
        [Tooltip("Max angle from forward (degrees) at which sprint is allowed.")]
        [Range(0f, 90f)] public float SprintMaxAngle;

        [Header("Acceleration (seconds)")]
        [Tooltip("Time to go from standstill to run speed.")]
        [Min(0.01f)] public float AccelerationTime;
        [Tooltip("Time to go from run speed to sprint speed. Longer = more momentum.")]
        [Min(0.01f)] public float SprintAccelerationTime;
        [Tooltip("Time to brake from run speed to standstill.")]
        [Min(0.01f)] public float DecelerationTime;
        [Tooltip("Fraction of ground acceleration available in the air.")]
        [Range(0f, 1f)] public float AirControl;

        [Header("Slopes")]
        [Tooltip("Speed multiplier when walking straight up the steepest walkable slope.")]
        [Range(0f, 1f)] public float UphillMinMultiplier;
        [Range(1f, 89f)] public float MaxWalkableSlope;

        [Header("Gravity")]
        [Min(0f)] public float Gravity;
        [Min(0f)] public float TerminalFallSpeed;
        [Tooltip("Small downward speed while grounded to keep contact.")]
        [Min(0f)] public float GroundStickSpeed;

        [Header("State")]
        [Min(0f)] public float IdleSpeedThreshold;

        public static MovementTuning Default => new MovementTuning
        {
            WalkSpeed = 1.8f,
            RunSpeed = 3.6f,
            SprintSpeed = 5.8f,
            StrafeMultiplier = 0.85f,
            BackwardMultiplier = 0.7f,
            SprintMaxAngle = 50f,
            AccelerationTime = 0.18f,
            SprintAccelerationTime = 0.45f,
            DecelerationTime = 0.12f,
            AirControl = 0.1f,
            UphillMinMultiplier = 0.8f,
            MaxWalkableSlope = 45f,
            Gravity = 20f,
            TerminalFallSpeed = 50f,
            GroundStickSpeed = 2f,
            IdleSpeedThreshold = 0.15f,
        };
    }
}
