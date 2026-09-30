using UnityEngine;

namespace Polykov.Weapons
{
    /// <summary>What a surface is made of, for impact effects (and later penetration and sounds).</summary>
    public enum SurfaceType : byte
    {
        Concrete = 0,
        Metal = 1,
        Wood = 2,
        Dirt = 3,
        Flesh = 4,
    }

    /// <summary>Tags a collider with the surface it represents (impact effects). Missing = Concrete.</summary>
    public sealed class SurfaceMaterial : MonoBehaviour
    {
        public SurfaceType Type = SurfaceType.Concrete;
    }
}
