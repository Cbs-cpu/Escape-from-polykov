using UnityEngine;

namespace Polykov.Movement
{
    /// <summary>
    /// Result of the environment probes (ground below, headroom above), fed into the motor as data so the
    /// simulation stays deterministic.
    /// </summary>
    public readonly struct GroundInfo
    {
        public readonly bool Grounded;
        public readonly Vector3 Normal;
        /// <summary>True when there is no room to stand up from the current crouch height.</summary>
        public readonly bool CeilingBlocked;

        public GroundInfo(bool grounded, Vector3 normal) : this(grounded, normal, false)
        {
        }

        public GroundInfo(bool grounded, Vector3 normal, bool ceilingBlocked)
        {
            Grounded = grounded;
            Normal = normal;
            CeilingBlocked = ceilingBlocked;
        }

        public GroundInfo WithCeiling(bool blocked) => new GroundInfo(Grounded, Normal, blocked);

        public float SlopeAngle => Vector3.Angle(Normal, Vector3.up);

        public static GroundInfo Flat => new GroundInfo(true, Vector3.up);
        public static GroundInfo Air => new GroundInfo(false, Vector3.up);
    }
}
