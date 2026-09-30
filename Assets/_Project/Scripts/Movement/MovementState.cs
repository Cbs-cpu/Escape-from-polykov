using System;
using UnityEngine;

namespace Polykov.Movement
{
    /// <summary>Authoritative simulation state of a character. Runtime only — never stored in a ScriptableObject.</summary>
    [Serializable]
    public struct MovementState
    {
        /// <summary>Horizontal velocity (y is always 0). Kept separate from slope alignment so speed never drifts on ramps.</summary>
        public Vector3 PlanarVelocity;
        public float VerticalSpeed;
        /// <summary>Final world-space velocity for this tick (slope aligned + vertical). Apply this to the collider.</summary>
        public Vector3 Velocity;
        public LocomotionState Locomotion;
        public bool Grounded;

        /// <summary>Current lean, -1 (left) .. +1 (right). Replicated: drives remote body and camera.</summary>
        public float Lean;
        /// <summary>Seconds left in which a buffered jump press is still honored.</summary>
        public float JumpBuffer;
        /// <summary>Seconds left in which a jump is still allowed after leaving the ground.</summary>
        public float CoyoteTime;
        /// <summary>True only on the tick the character left the ground by jumping.</summary>
        public bool JustJumped;
        /// <summary>True only on the tick the character touched the ground after being airborne.</summary>
        public bool JustLanded;
        /// <summary>Downward speed (m/s, positive) at the moment of the last landing.</summary>
        public float LandingImpact;

        /// <summary>Crouch amount, 0 = standing, 1 = fully crouched. Drives capsule height, camera and body.</summary>
        public float Crouch;
        /// <summary>Crouch intent after rules (ceiling keeps you down even if the key is released).</summary>
        public bool Crouching;

        /// <summary>Stamina spent (0 = full). Stored as "used" so a default state starts rested.</summary>
        public float StaminaUsed;
        /// <summary>Seconds left before stamina starts recovering.</summary>
        public float StaminaRegenDelay;
        /// <summary>Ran out of stamina: no sprint until it recovers past the threshold.</summary>
        public bool Exhausted;

        /// <summary>Remaining stamina 0..1 for UI and presentation (1 when stamina is disabled).</summary>
        public float StaminaFraction(in MovementTuning tuning)
            => tuning.MaxStamina > 0f ? 1f - StaminaUsed / tuning.MaxStamina : 1f;

        public float PlanarSpeed => PlanarVelocity.magnitude;
    }
}
