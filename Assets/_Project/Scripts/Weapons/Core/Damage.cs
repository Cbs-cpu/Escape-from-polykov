using UnityEngine;

namespace Polykov.Weapons
{
    public enum SurfaceType : byte
    {
        Concrete = 0,
        Metal = 1,
        Wood = 2,
        Dirt = 3,
        Flesh = 4,
    }

    public readonly struct DamageInfo
    {
        public readonly float Amount;
        public readonly Vector3 Point;
        public readonly Vector3 Normal;
        public readonly Vector3 Direction;
        public readonly float Force;

        public DamageInfo(float amount, Vector3 point, Vector3 normal, Vector3 direction, float force)
        {
            Amount = amount;
            Point = point;
            Normal = normal;
            Direction = direction;
            Force = force;
        }
    }

    /// <summary>Anything that reacts to being shot. In multiplayer only the server calls this.</summary>
    public interface IDamageable
    {
        void ApplyDamage(in DamageInfo info);
    }
}
