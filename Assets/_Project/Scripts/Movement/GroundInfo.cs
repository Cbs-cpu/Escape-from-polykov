using UnityEngine;

namespace Polykov.Movement
{
    /// <summary>Result of the ground probe, fed into the motor as data so the simulation stays deterministic.</summary>
    public readonly struct GroundInfo
    {
        public readonly bool Grounded;
        public readonly Vector3 Normal;

        public GroundInfo(bool grounded, Vector3 normal)
        {
            Grounded = grounded;
            Normal = normal;
        }

        public float SlopeAngle => Vector3.Angle(Normal, Vector3.up);

        public static GroundInfo Flat => new GroundInfo(true, Vector3.up);
        public static GroundInfo Air => new GroundInfo(false, Vector3.up);
    }
}
