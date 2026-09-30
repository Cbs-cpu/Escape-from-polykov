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

        [Header("Jump")]
        [Tooltip("Apex height of a standing jump (meters).")]
        [Min(0f)] public float JumpHeight;
        [Tooltip("A jump pressed this long before landing still triggers.")]
        [Min(0f)] public float JumpBufferTime;
        [Tooltip("A jump is still allowed this long after walking off a ledge.")]
        [Min(0f)] public float CoyoteTime;
        [Tooltip("Planar speed kept when landing from a real fall.")]
        [Range(0f, 1f)] public float LandingMomentum;
        [Tooltip("Falls slower than this (m/s) don't cost momentum.")]
        [Min(0f)] public float HardLandingSpeed;

        [Header("Lean (peek)")]
        [Tooltip("Seconds to go from upright to full lean.")]
        [Min(0.01f)] public float LeanTime;
        [Tooltip("Speed multiplier at full lean.")]
        [Range(0f, 1f)] public float LeanMoveMultiplier;

        [Header("Crouch")]
        [Tooltip("Max speed while fully crouched (m/s).")]
        [Min(0f)] public float CrouchSpeed;
        [Tooltip("Seconds to go from standing to fully crouched (and back).")]
        [Min(0.01f)] public float CrouchTime;

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
            JumpHeight = 0.5f,
            JumpBufferTime = 0.12f,
            CoyoteTime = 0.1f,
            LandingMomentum = 0.8f,
            // Above a standing jump's landing speed (~4.5 m/s): only real drops cost momentum.
            HardLandingSpeed = 5f,
            LeanTime = 0.18f,
            LeanMoveMultiplier = 0.7f,
            CrouchSpeed = 1.5f,
            CrouchTime = 0.22f,
            IdleSpeedThreshold = 0.15f,
        };
    }
}
