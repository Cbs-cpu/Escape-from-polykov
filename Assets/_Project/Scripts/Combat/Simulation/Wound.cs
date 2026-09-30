using System;
using UnityEngine;

namespace Polykov.Combat
{
    /// <summary>Dismemberment odds. Plain data; the calibre multiplier is a per-hit input.</summary>
    [Serializable]
    public struct WoundTuning
    {
        [Header("Accumulated overflow (damage beyond 0 HP) needed before a destroyed part can be severed")]
        [Min(0f)] public float HeadThreshold;
        [Min(0f)] public float ArmThreshold;
        [Min(0f)] public float LegThreshold;

        [Header("Chance per eligible hit")]
        [Range(0f, 1f)] public float BaseChance;
        [Min(0f)] public float ChancePerOverflow;
        [Min(0f)] public float ChancePerDamage;
        [Range(0f, 1f)] public float MaxChance;
        [Range(0f, 1f)] public float HeadMultiplier;

        public float Threshold(BodyPart part) => part switch
        {
            BodyPart.Head => HeadThreshold,
            BodyPart.LeftArm => ArmThreshold,
            BodyPart.RightArm => ArmThreshold,
            _ => LegThreshold,
        };

        public static WoundTuning Default => new WoundTuning
        {
            HeadThreshold = 40f,
            ArmThreshold = 20f,
            LegThreshold = 25f,
            BaseChance = 0.02f,
            ChancePerOverflow = 0.0005f,
            ChancePerDamage = 0.0002f,
            MaxChance = 0.12f,
            HeadMultiplier = 0.25f,
        };
    }

    /// <summary>Accumulated overflow per part and which limbs are gone.</summary>
    [Serializable]
    public struct WoundState
    {
        public float OverflowHead, OverflowThorax, OverflowStomach, OverflowLeftArm, OverflowRightArm, OverflowLeftLeg, OverflowRightLeg;
        public bool HeadSevered, LeftArmSevered, RightArmSevered, LeftLegSevered, RightLegSevered;

        public float Overflow(BodyPart part) => part switch
        {
            BodyPart.Head => OverflowHead,
            BodyPart.Thorax => OverflowThorax,
            BodyPart.Stomach => OverflowStomach,
            BodyPart.LeftArm => OverflowLeftArm,
            BodyPart.RightArm => OverflowRightArm,
            BodyPart.LeftLeg => OverflowLeftLeg,
            _ => OverflowRightLeg,
        };

        public void SetOverflow(BodyPart part, float v)
        {
            switch (part)
            {
                case BodyPart.Head: OverflowHead = v; break;
                case BodyPart.Thorax: OverflowThorax = v; break;
                case BodyPart.Stomach: OverflowStomach = v; break;
                case BodyPart.LeftArm: OverflowLeftArm = v; break;
                case BodyPart.RightArm: OverflowRightArm = v; break;
                case BodyPart.LeftLeg: OverflowLeftLeg = v; break;
                default: OverflowRightLeg = v; break;
            }
        }

        public bool IsSevered(BodyPart part) => part switch
        {
            BodyPart.Head => HeadSevered,
            BodyPart.LeftArm => LeftArmSevered,
            BodyPart.RightArm => RightArmSevered,
            BodyPart.LeftLeg => LeftLegSevered,
            BodyPart.RightLeg => RightLegSevered,
            _ => false,
        };

        public void SetSevered(BodyPart part)
        {
            switch (part)
            {
                case BodyPart.Head: HeadSevered = true; break;
                case BodyPart.LeftArm: LeftArmSevered = true; break;
                case BodyPart.RightArm: RightArmSevered = true; break;
                case BodyPart.LeftLeg: LeftLegSevered = true; break;
                case BodyPart.RightLeg: RightLegSevered = true; break;
            }
        }

        public static bool CanSever(BodyPart part) => part != BodyPart.Thorax && part != BodyPart.Stomach;
    }

    public enum WoundEventKind : byte { None = 0, Severed = 1 }

    public readonly struct WoundEvent
    {
        public readonly WoundEventKind Kind;
        public readonly BodyPart Part;
        /// <summary>The severed part is the head: the caller must mark the target dead.</summary>
        public readonly bool Kills;

        public WoundEvent(WoundEventKind kind, BodyPart part, bool kills)
        {
            Kind = kind;
            Part = part;
            Kills = kills;
        }

        public bool Severed => Kind == WoundEventKind.Severed;
    }

    /// <summary>Pure, deterministic dismemberment rules. Same seed, same outcome on server and clients.</summary>
    public static class WoundModel
    {
        /// <summary>Chance of severing given the accumulated overflow (already including this hit). 0 when not eligible.</summary>
        public static float Chance(in WoundState wounds, in HealthState health, BodyPart part, float damage,
            float accumulatedOverflow, float calibreMultiplier, in WoundTuning t)
        {
            if (!WoundState.CanSever(part) || wounds.IsSevered(part) || !health.IsDestroyed(part)) return 0f;
            float threshold = t.Threshold(part);
            if (accumulatedOverflow < threshold || accumulatedOverflow <= 0f) return 0f;
            float c = t.BaseChance + t.ChancePerOverflow * (accumulatedOverflow - threshold) + t.ChancePerDamage * Mathf.Max(0f, damage);
            c = Mathf.Min(c, t.MaxChance);
            if (part == BodyPart.Head) c *= t.HeadMultiplier;
            return Mathf.Clamp01(c * Mathf.Max(0f, calibreMultiplier));
        }

        /// <param name="health">State after the hit was applied.</param>
        /// <param name="overflow">Damage beyond 0 HP from this hit (DamageResult.Overflow).</param>
        public static WoundState ApplyHit(in WoundState state, in HealthState health, BodyPart part, float damage,
            float overflow, float calibreMultiplier, uint seed, in WoundTuning tuning, out WoundEvent evt)
        {
            WoundState s = state;
            evt = default;
            if (overflow > 0f && health.IsDestroyed(part))
                s.SetOverflow(part, s.Overflow(part) + overflow);

            float chance = Chance(s, health, part, damage, s.Overflow(part), calibreMultiplier, tuning);
            if (chance <= 0f) return s;

            if (Hash01(seed, (uint)part) < chance)
            {
                s.SetSevered(part);
                evt = new WoundEvent(WoundEventKind.Severed, part, part == BodyPart.Head);
            }
            return s;
        }

        /// <summary>Deterministic value in [0, 1), same mixing as WeaponMotor.Hash01.</summary>
        public static float Hash01(uint seed, uint salt)
        {
            unchecked
            {
                uint x = seed * 0x9E3779B1u ^ salt * 0xC2B2AE35u;
                x ^= x >> 16;
                x *= 0x7FEB352Du;
                x ^= x >> 15;
                x *= 0x846CA68Bu;
                x ^= x >> 16;
                return (x & 0xFFFFFFu) / 16777216f;
            }
        }
    }
}
