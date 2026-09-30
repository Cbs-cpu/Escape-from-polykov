using Polykov.Weapons;
using UnityEngine;

namespace Polykov.Combat
{
    /// <summary>A collider that maps bullet hits to a body part of its owner.</summary>
    public sealed class Hitbox : MonoBehaviour, IShotReceiver
    {
        public BodyPart Part;
        public HealthComponent Owner;

        public void OnShot(in ShotHit hit)
        {
            if (Owner != null) Owner.TakeHit(Part, hit);
        }
    }
}
