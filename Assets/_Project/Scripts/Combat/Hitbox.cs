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
<<<<<<< HEAD
            if (Owner != null) Owner.Receive(this, hit);
=======
            if (Owner != null) Owner.TakeHit(Part, hit, Bone);
>>>>>>> 0c260b25b241536a1c414610faff92f989e73203
        }
    }
}
