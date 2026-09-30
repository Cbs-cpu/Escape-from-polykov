using System;
using UnityEngine;

namespace Polykov.Combat
{
    /// <summary>How hard each zone flinches and how fast the body recovers. Presentation data, not gameplay state.</summary>
    [Serializable]
    public struct HitReactionTuning
    {
        [Tooltip("Degrees of flinch per unit of bullet impulse (N·s) at zone factor 1.")]
        [Min(0f)] public float DegreesPerImpulse;
        [Tooltip("Extra degrees per point of damage.")]
        [Min(0f)] public float DegreesPerDamage;
        [Min(1f)] public float MaxDegrees;
        [Tooltip("Share of the flinch passed from a bone to its parent (0 = only the hit bone).")]
        [Range(0f, 1f)] public float ChainFalloff;
        [Min(1)] public int ChainLength;
        [Min(1f)] public float Stiffness;
        [Range(0.05f, 1.5f)] public float Damping;

        public static HitReactionTuning Default => new HitReactionTuning
        {
            DegreesPerImpulse = 1.6f,
            DegreesPerDamage = 0.08f,
            MaxDegrees = 28f,
            ChainFalloff = 0.6f,
            ChainLength = 4,
            Stiffness = 120f,
            Damping = 0.42f,
        };
    }

    /// <summary>Damped angular spring on a rotation vector (axis * degrees). Impulses go into velocity.</summary>
    public struct AngularSpring
    {
        public Vector3 Value;
        public Vector3 Velocity;

        public bool Idle => Value.sqrMagnitude < 1e-6f && Velocity.sqrMagnitude < 1e-6f;

        /// <summary>Kicks the spring so that its peak displacement is about <paramref name="peakDegrees"/> along axis.</summary>
        public void Kick(Vector3 axis, float peakDegrees, float stiffness, float damping)
        {
            // Released from rest with speed v0, an underdamped spring peaks at (v0 / wd) * exp(-z * w * tp), with
            // tp = atan(wd / (z * w)) / wd. Solve for v0 so the peak is the requested size.
            float w = Mathf.Sqrt(Mathf.Max(stiffness, 1f));
            float z = Mathf.Clamp(damping, 0.01f, 0.999f);
            float wd = w * Mathf.Sqrt(1f - z * z);
            float tp = Mathf.Atan2(wd, z * w) / wd;
            float peakPerSpeed = Mathf.Exp(-z * w * tp) / wd;
            Velocity += axis.normalized * (peakDegrees / peakPerSpeed);
        }

        public void Step(float stiffness, float damping, float dt)
        {
            float c = 2f * Mathf.Sqrt(stiffness) * damping;
            int steps = Mathf.Max(1, Mathf.CeilToInt(dt / 0.004f));
            float h = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                Velocity += (-stiffness * Value - c * Velocity) * h;
                Value += Velocity * h;
            }
            if (Idle)
            {
                Value = Vector3.zero;
                Velocity = Vector3.zero;
            }
        }
    }

    public static class HitReactionModel
    {
        /// <summary>Flinch multiplier per zone: the head snaps, the torso rocks, limbs whip.</summary>
        public static float ZoneFactor(BodyPart part) => part switch
        {
            BodyPart.Head => 1.7f,
            BodyPart.Thorax => 0.9f,
            BodyPart.Stomach => 1.1f,
            BodyPart.LeftArm => 1.3f,
            BodyPart.RightArm => 1.3f,
            _ => 0.8f,
        };

        /// <summary>Peak flinch (degrees) at the hit bone, proportional to impulse and damage, capped.</summary>
        public static float Degrees(BodyPart part, float impulse, float damage, in HitReactionTuning t)
        {
            float d = (t.DegreesPerImpulse * Mathf.Max(0f, impulse) + t.DegreesPerDamage * Mathf.Max(0f, damage)) * ZoneFactor(part);
            return Mathf.Min(d, t.MaxDegrees);
        }

        /// <summary>Share of the hit bone's flinch that reaches the bone <paramref name="level"/> steps up the chain.</summary>
        public static float Attenuation(int level, in HitReactionTuning t) => level < 0 || level >= t.ChainLength
            ? 0f
            : Mathf.Pow(t.ChainFalloff, level);

        /// <summary>
        /// Axis to rotate a bone so its far end moves along the shot direction: cross(bone axis, direction).
        /// Zero when the shot is parallel to the bone.
        /// </summary>
        public static Vector3 Axis(Vector3 boneAxis, Vector3 shotDirection)
        {
            Vector3 axis = Vector3.Cross(boneAxis, shotDirection);
            return axis.sqrMagnitude < 1e-8f ? Vector3.zero : axis.normalized;
        }
    }
}
