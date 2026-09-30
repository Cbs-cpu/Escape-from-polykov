using UnityEngine;

namespace Polykov.Weapons
{
    /// <summary>Data of a bullet hit. Server-side in Phase 2 (damage is decided there).</summary>
    public readonly struct ShotHit
    {
        public readonly Vector3 Point;
        public readonly Vector3 Normal;
        public readonly Vector3 Direction;
        public readonly float Damage;

        public ShotHit(Vector3 point, Vector3 normal, Vector3 direction, float damage)
        {
            Point = point;
            Normal = normal;
            Direction = direction;
            Damage = damage;
        }
    }

    /// <summary>Anything that reacts to being shot (targets now; health components later).</summary>
    public interface IShotReceiver
    {
        void OnShot(in ShotHit hit);
    }
}
