using Polykov.Weapons;
using UnityEngine;

namespace Polykov.Combat
{
    /// <summary>A collider that maps bullet hits to a body part of its owner.</summary>
    public sealed class Hitbox : MonoBehaviour, IShotReceiver
    {
        public BodyPart Part;
        public HealthComponent Owner;
        /// <summary>The skeleton bone this collider rides on (reactions and ragdoll impulses go here).</summary>
        public Transform Bone;

        public void OnShot(in ShotHit hit)
        {
            if (Owner != null) Owner.Receive(this, hit);
        }
    }
}
