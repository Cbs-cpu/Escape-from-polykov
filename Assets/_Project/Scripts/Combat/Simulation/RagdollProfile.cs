using UnityEngine;

namespace Polykov.Combat
{
    /// <summary>The 11 simulated bodies of the ragdoll.</summary>
    public enum RagdollBone : byte
    {
        Hips = 0,
        Spine = 1,
        Head = 2,
        LeftUpperArm = 3,
        LeftLowerArm = 4,
        RightUpperArm = 5,
        RightLowerArm = 6,
        LeftUpperLeg = 7,
        LeftLowerLeg = 8,
        RightUpperLeg = 9,
        RightLowerLeg = 10,
    }

    /// <summary>Anatomical joint limits in degrees (CharacterJoint semantics).</summary>
    public readonly struct JointLimits
    {
        public readonly float TwistLow, TwistHigh, Swing1, Swing2;

        public JointLimits(float twistLow, float twistHigh, float swing1, float swing2)
        {
            TwistLow = twistLow;
            TwistHigh = twistHigh;
            Swing1 = swing1;
            Swing2 = swing2;
        }
    }

    /// <summary>Masses, hierarchy and limits of the ragdoll (a ~75 kg adult). Plain data, testable.</summary>
    public static class RagdollProfile
    {
        public const int Count = 11;

        public static float Mass(RagdollBone bone) => bone switch
        {
            RagdollBone.Hips => 12f,
            RagdollBone.Spine => 23f,
            RagdollBone.Head => 5f,
            RagdollBone.LeftUpperArm => 2.3f,
            RagdollBone.RightUpperArm => 2.3f,
            RagdollBone.LeftLowerArm => 1.7f,
            RagdollBone.RightLowerArm => 1.7f,
            RagdollBone.LeftUpperLeg => 9.3f,
            RagdollBone.RightUpperLeg => 9.3f,
            _ => 4.3f,
        };

        public static float TotalMass
        {
            get
            {
                float total = 0f;
                for (int i = 0; i < Count; i++) total += Mass((RagdollBone)i);
                return total;
            }
        }

        /// <summary>Body this one is jointed to (Hips has none, returns Hips).</summary>
        public static RagdollBone Parent(RagdollBone bone) => bone switch
        {
            RagdollBone.Spine => RagdollBone.Hips,
            RagdollBone.Head => RagdollBone.Spine,
            RagdollBone.LeftUpperArm => RagdollBone.Spine,
            RagdollBone.RightUpperArm => RagdollBone.Spine,
            RagdollBone.LeftLowerArm => RagdollBone.LeftUpperArm,
            RagdollBone.RightLowerArm => RagdollBone.RightUpperArm,
            RagdollBone.LeftUpperLeg => RagdollBone.Hips,
            RagdollBone.RightUpperLeg => RagdollBone.Hips,
            RagdollBone.LeftLowerLeg => RagdollBone.LeftUpperLeg,
            RagdollBone.RightLowerLeg => RagdollBone.RightUpperLeg,
            _ => RagdollBone.Hips,
        };

        public static bool IsHinge(RagdollBone bone) => bone == RagdollBone.LeftLowerArm ||
            bone == RagdollBone.RightLowerArm || bone == RagdollBone.LeftLowerLeg || bone == RagdollBone.RightLowerLeg;

        public static JointLimits Limits(RagdollBone bone) => bone switch
        {
            RagdollBone.Spine => new JointLimits(-20f, 20f, 25f, 15f),
            RagdollBone.Head => new JointLimits(-40f, 40f, 35f, 25f),
            RagdollBone.LeftUpperArm => new JointLimits(-60f, 60f, 85f, 60f),
            RagdollBone.RightUpperArm => new JointLimits(-60f, 60f, 85f, 60f),
            RagdollBone.LeftLowerArm => new JointLimits(-25f, 135f, 4f, 4f),
            RagdollBone.RightLowerArm => new JointLimits(-25f, 135f, 4f, 4f),
            RagdollBone.LeftUpperLeg => new JointLimits(-25f, 25f, 75f, 30f),
            RagdollBone.RightUpperLeg => new JointLimits(-25f, 25f, 75f, 30f),
            RagdollBone.LeftLowerLeg => new JointLimits(-15f, 140f, 4f, 4f),
            RagdollBone.RightLowerLeg => new JointLimits(-15f, 140f, 4f, 4f),
            _ => new JointLimits(0f, 0f, 0f, 0f),
        };

        /// <summary>The ragdoll body a hit zone belongs to when the hit bone has no body of its own.</summary>
        public static RagdollBone ForPart(BodyPart part, bool lower) => part switch
        {
            BodyPart.Head => RagdollBone.Head,
            BodyPart.Thorax => RagdollBone.Spine,
            BodyPart.Stomach => RagdollBone.Hips,
            BodyPart.LeftArm => lower ? RagdollBone.LeftLowerArm : RagdollBone.LeftUpperArm,
            BodyPart.RightArm => lower ? RagdollBone.RightLowerArm : RagdollBone.RightUpperArm,
            BodyPart.LeftLeg => lower ? RagdollBone.LeftLowerLeg : RagdollBone.LeftUpperLeg,
            _ => lower ? RagdollBone.RightLowerLeg : RagdollBone.RightUpperLeg,
        };

        /// <summary>Impulse (N*s) of the killing shot (exaggerated: a real bullet barely moves a body).</summary>
        public static float LethalImpulse(float damage, BodyPart part) =>
            Mathf.Clamp(damage * 1.6f, 30f, 160f) * (part == BodyPart.Head ? 0.7f : 1f);

        /// <summary>Extra impulse when shooting a body that is already dead.</summary>
        public static float CorpseImpulse(float damage) => Mathf.Clamp(damage * 0.9f, 15f, 90f);
    }

    /// <summary>
    /// Keeps at most <c>limit</c> ragdolls simulating: registering one more returns the oldest to freeze.
    /// </summary>
    public sealed class RagdollBudget
    {
        private readonly int[] _ids;
        private int _head;
        private int _count;

        public RagdollBudget(int limit)
        {
            _ids = new int[Mathf.Max(1, limit)];
        }

        public int Limit => _ids.Length;
        public int Count => _count;

        /// <summary>Adds a ragdoll; returns the id that must be frozen, or -1.</summary>
        public int Register(int id)
        {
            int evicted = -1;
            if (_count == _ids.Length)
            {
                evicted = _ids[_head];
                _ids[_head] = id;
                _head = (_head + 1) % _ids.Length;
            }
            else
            {
                _ids[(_head + _count) % _ids.Length] = id;
                _count++;
            }
            return evicted;
        }

        /// <summary>Removes a ragdoll that ended by itself (revived); keeps the order of the rest.</summary>
        public bool Remove(int id)
        {
            int found = -1;
            for (int i = 0; i < _count; i++)
                if (_ids[(_head + i) % _ids.Length] == id) { found = i; break; }
            if (found < 0) return false;
            for (int i = found; i < _count - 1; i++)
                _ids[(_head + i) % _ids.Length] = _ids[(_head + i + 1) % _ids.Length];
            _count--;
            return true;
        }

        public bool Contains(int id)
        {
            for (int i = 0; i < _count; i++)
                if (_ids[(_head + i) % _ids.Length] == id) return true;
            return false;
        }
    }
}
