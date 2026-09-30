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

        public float PlanarSpeed => PlanarVelocity.magnitude;
    }
}
