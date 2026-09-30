using System;
using UnityEngine;

namespace Polykov.Combat
{
    /// <summary>Hit zones, Tarkov-style. Order is stable: it is the replicated index.</summary>
    public enum BodyPart : byte
    {
        Head = 0,
        Thorax = 1,
        Stomach = 2,
        LeftArm = 3,
        RightArm = 4,
        LeftLeg = 5,
        RightLeg = 6,
    }

    /// <summary>Max HP per part and how damage to a destroyed ("blacked") limb spreads. Plain data.</summary>
    [Serializable]
    public struct HealthTuning
    {
        public const int PartCount = 7;

        [Min(1f)] public float Head;
        [Min(1f)] public float Thorax;
        [Min(1f)] public float Stomach;
        [Min(1f)] public float Arm;
        [Min(1f)] public float Leg;

        [Header("Damage to a destroyed part spreads to the others, scaled by:")]
        [Range(0f, 3f)] public float StomachSpread;
        [Range(0f, 3f)] public float ArmSpread;
        [Range(0f, 3f)] public float LegSpread;

        public float Max(BodyPart part) => part switch
        {
            BodyPart.Head => Head,
            BodyPart.Thorax => Thorax,
            BodyPart.Stomach => Stomach,
            BodyPart.LeftArm => Arm,
            BodyPart.RightArm => Arm,
            _ => Leg,
        };

        public float Spread(BodyPart part) => part switch
        {
            BodyPart.Stomach => StomachSpread,
            BodyPart.LeftArm => ArmSpread,
            BodyPart.RightArm => ArmSpread,
            BodyPart.LeftLeg => LegSpread,
            BodyPart.RightLeg => LegSpread,
            _ => 0f, // head and thorax are lethal, never "blacked"
        };

        public float Total => Head + Thorax + Stomach + 2f * Arm + 2f * Leg;

        public static HealthTuning Default => new HealthTuning
        {
            Head = 35f,
            Thorax = 85f,
            Stomach = 70f,
            Arm = 60f,
            Leg = 65f,
            StomachSpread = 1.5f,
            ArmSpread = 0.7f,
            LegSpread = 1f,
        };
    }

    /// <summary>
    /// HP of every part. Server-authoritative in multiplayer (replicated to the owner in full, to others only
    /// <see cref="Alive"/>). Allocation-free: seven fields behind an indexer.
    /// </summary>
    [Serializable]
    public struct HealthState
    {
        public float Head;
        public float Thorax;
        public float Stomach;
        public float LeftArm;
        public float RightArm;
        public float LeftLeg;
        public float RightLeg;
        public bool Alive;
        /// <summary>The part whose damage killed (valid when !Alive).</summary>
        public BodyPart KillingPart;

        public float this[BodyPart part]
        {
            get => part switch
            {
                BodyPart.Head => Head,
                BodyPart.Thorax => Thorax,
                BodyPart.Stomach => Stomach,
                BodyPart.LeftArm => LeftArm,
                BodyPart.RightArm => RightArm,
                BodyPart.LeftLeg => LeftLeg,
                _ => RightLeg,
            };
            set
            {
                switch (part)
                {
                    case BodyPart.Head: Head = value; break;
                    case BodyPart.Thorax: Thorax = value; break;
                    case BodyPart.Stomach: Stomach = value; break;
                    case BodyPart.LeftArm: LeftArm = value; break;
                    case BodyPart.RightArm: RightArm = value; break;
                    case BodyPart.LeftLeg: LeftLeg = value; break;
                    default: RightLeg = value; break;
                }
            }
        }

        public float Total => Head + Thorax + Stomach + LeftArm + RightArm + LeftLeg + RightLeg;

        public bool IsDestroyed(BodyPart part) => this[part] <= 0f;

        /// <summary>Destroyed legs (0-2): movement penalties hook here.</summary>
        public int DestroyedLegs => (LeftLeg <= 0f ? 1 : 0) + (RightLeg <= 0f ? 1 : 0);

        public static HealthState Full(in HealthTuning t) => new HealthState
        {
            Head = t.Head,
            Thorax = t.Thorax,
            Stomach = t.Stomach,
            LeftArm = t.Arm,
            RightArm = t.Arm,
            LeftLeg = t.Leg,
            RightLeg = t.Leg,
            Alive = true,
        };
    }

    public readonly struct DamageResult
    {
        /// <summary>HP actually removed (after clamping at zero).</summary>
        public readonly float Applied;
        /// <summary>The damage went to a destroyed limb and was spread.</summary>
        public readonly bool Spread;
        /// <summary>This hit destroyed the part it landed on.</summary>
        public readonly bool DestroyedPart;
        public readonly bool Killed;

        public DamageResult(float applied, bool spread, bool destroyedPart, bool killed)
        {
            Applied = applied;
            Spread = spread;
            DestroyedPart = destroyedPart;
            Killed = killed;
        }
    }

    /// <summary>Pure damage rules. Same result on every machine; the server is the one that applies it.</summary>
    public static class HealthModel
    {
        public static HealthState ApplyDamage(in HealthState state, BodyPart part, float damage, in HealthTuning tuning,
            out DamageResult result)
        {
            HealthState s = state;
            if (!s.Alive || damage <= 0f)
            {
                result = default;
                return s;
            }

            float before = s.Total;
            bool spread = false;
            bool destroyed = false;

            if (s[part] > 0f)
            {
                float hp = s[part] - damage;
                destroyed = hp <= 0f;
                s[part] = Mathf.Max(0f, hp);
            }
            else
            {
                // Blacked limb: the hit spreads, scaled, evenly over every part that still has HP.
                spread = true;
                float amount = damage * tuning.Spread(part);
                int alive = 0;
                for (int i = 0; i < HealthTuning.PartCount; i++)
                    if (s[(BodyPart)i] > 0f) alive++;
                if (alive > 0 && amount > 0f)
                {
                    float share = amount / alive;
                    for (int i = 0; i < HealthTuning.PartCount; i++)
                    {
                        var p = (BodyPart)i;
                        if (s[p] <= 0f) continue;
                        s[p] = Mathf.Max(0f, s[p] - share);
                    }
                }
            }

            bool killed = false;
            if (s.Head <= 0f || s.Thorax <= 0f)
            {
                killed = true;
                s.Alive = false;
                s.KillingPart = s.Head <= 0f ? BodyPart.Head : BodyPart.Thorax;
            }

            result = new DamageResult(before - s.Total, spread, destroyed, killed);
            return s;
        }
    }
}
