using UnityEngine;

namespace Polykov.Weapons
{
    /// <summary>Tags a collider with the surface it represents (impact effects). Missing = Concrete.</summary>
    public sealed class SurfaceMaterial : MonoBehaviour
    {
        public SurfaceType Type = SurfaceType.Concrete;
    }
}
