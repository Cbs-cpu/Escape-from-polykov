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

        public float PlanarSpeed => PlanarVelocity.magnitude;
    }
}
