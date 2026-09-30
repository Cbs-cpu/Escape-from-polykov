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
        /// <summary>Momentum the bullet transfers (N·s): drives hit reactions and ragdoll impulses.</summary>
        public readonly float Impulse;
        /// <summary>Scales dismemberment odds (bigger calibre / heavier load = more).</summary>
        public readonly float CalibreMultiplier;
        /// <summary>Where the shot came from (AI awareness, exit wounds).</summary>
        public readonly Vector3 Origin;

        public ShotHit(Vector3 point, Vector3 normal, Vector3 direction, float damage)
            : this(point, normal, direction, damage, 4f, 1f, point - direction)
        {
        }

        public ShotHit(Vector3 point, Vector3 normal, Vector3 direction, float damage, float impulse,
            float calibreMultiplier, Vector3 origin)
        {
            Point = point;
            Normal = normal;
            Direction = direction;
            Damage = damage;
            Impulse = impulse;
            CalibreMultiplier = calibreMultiplier;
            Origin = origin;
        }
    }

    /// <summary>Anything that reacts to being shot (targets now; health components later).</summary>
    public interface IShotReceiver
    {
        void OnShot(in ShotHit hit);
    }
}
