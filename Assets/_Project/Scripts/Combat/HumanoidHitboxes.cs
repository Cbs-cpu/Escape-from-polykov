using UnityEngine;

namespace Polykov.Combat
{
    /// <summary>
    /// Builds capsule hitboxes on a humanoid's bones (head, thorax, stomach, arms, legs). Colliders are children
    /// of the bones, so they follow the animation. Sizes fit the Operator; they only need to be roughly right.
    /// </summary>
    public static class HumanoidHitboxes
    {
        public static int Build(Animator animator, HealthComponent owner, int layer)
        {
            int count = 0;
            Transform root = animator.transform;
            Vector3 up = root.up;

            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (head != null) count += Segment(head, head.position, head.position + up * 0.23f, 0.1f, BodyPart.Head, owner, layer);

            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            Transform chest = animator.GetBoneTransform(HumanBodyBones.Chest) ?? animator.GetBoneTransform(HumanBodyBones.Spine);
            Transform neck = animator.GetBoneTransform(HumanBodyBones.Neck) ?? head;
            if (hips != null && chest != null)
                count += Segment(hips, hips.position, chest.position, 0.15f, BodyPart.Stomach, owner, layer);
            if (chest != null && neck != null)
                count += Segment(chest, chest.position, neck.position, 0.16f, BodyPart.Thorax, owner, layer);

            count += Limb(animator, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
                HumanBodyBones.LeftMiddleProximal, 0.055f, BodyPart.LeftArm, owner, layer);
            count += Limb(animator, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
                HumanBodyBones.RightMiddleProximal, 0.055f, BodyPart.RightArm, owner, layer);
            count += Limb(animator, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot,
                HumanBodyBones.LeftToes, 0.08f, BodyPart.LeftLeg, owner, layer);
            count += Limb(animator, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot,
                HumanBodyBones.RightToes, 0.08f, BodyPart.RightLeg, owner, layer);
            return count;
        }

        private static int Limb(Animator a, HumanBodyBones upper, HumanBodyBones lower, HumanBodyBones end,
            HumanBodyBones tip, float radius, BodyPart part, HealthComponent owner, int layer)
        {
            Transform u = a.GetBoneTransform(upper), l = a.GetBoneTransform(lower), e = a.GetBoneTransform(end);
            Transform t = a.GetBoneTransform(tip);
            int n = 0;
            if (u != null && l != null) n += Segment(u, u.position, l.position, radius, part, owner, layer);
            if (l != null && e != null) n += Segment(l, l.position, e.position, radius * 0.8f, part, owner, layer);
            if (e != null && t != null) n += Segment(e, e.position, t.position, radius * 0.7f, part, owner, layer);
            return n;
        }

        private static int Segment(Transform bone, Vector3 from, Vector3 to, float radius, BodyPart part,
            HealthComponent owner, int layer)
        {
            Vector3 axis = to - from;
            float length = axis.magnitude;
            if (length < 1e-3f) return 0;
            var go = new GameObject("Hitbox_" + part) { layer = layer };
            go.transform.SetPositionAndRotation(from, Quaternion.LookRotation(axis / length));
            go.transform.SetParent(bone, true);
            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.direction = 2; // along local Z
            capsule.radius = radius;
            capsule.height = length + radius * 2f;
            capsule.center = new Vector3(0f, 0f, length * 0.5f);
            go.AddComponent<Weapons.SurfaceMaterial>().Type = Weapons.SurfaceType.Flesh;
            var hitbox = go.AddComponent<Hitbox>();
            hitbox.Part = part;
            hitbox.Owner = owner;
            return 1;
        }
    }
}
