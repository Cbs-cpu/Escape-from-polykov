<<<<<<< HEAD
=======
using System;
>>>>>>> 0c260b25b241536a1c414610faff92f989e73203
using UnityEngine;

namespace Polykov.Combat
{
<<<<<<< HEAD
    /// <summary>
    /// Damped angular spring on one bone (degrees / degrees per second, world-space rotation vector).
    /// Pure and allocation-free: the presentation layer adds the result on top of the animated pose.
    /// </summary>
    public struct HitSpring
    {
        public Vector3 Angle;
        public Vector3 Velocity;

        public bool AtRest => Angle.sqrMagnitude < 0.01f && Velocity.sqrMagnitude < 1f;

        public void AddImpulse(Vector3 angularVelocity) => Velocity += angularVelocity;

        public void Reset()
        {
            Angle = Vector3.zero;
            Velocity = Vector3.zero;
        }

        /// <summary>Semi-implicit Euler, sub-stepped so a long frame never explodes the spring.</summary>
        public void Step(float dt, float stiffness, float damping)
        {
            const float MaxStep = 1f / 120f;
            while (dt > 0f)
            {
                float h = dt > MaxStep ? MaxStep : dt;
                Velocity += (-stiffness * Angle - damping * Velocity) * h;
                Angle += Velocity * h;
                dt -= h;
            }
            if (AtRest) Reset();
        }
    }

    /// <summary>Tuning and mapping from a hit (damage, body part) to a bone reaction.</summary>
    public static class HitReaction
    {
        /// <summary>Spring natural frequency (rad/s): period of about 0.3 s.</summary>
        public const float Stiffness = 440f;
        /// <summary>2 * zeta * omega, zeta ~ 0.45: settles back to the animated pose in ~0.3-0.4 s.</summary>
        public const float Damping = 19f;
        /// <summary>Degrees per second per point of damage for a unit-weight part.</summary>
        public const float DegreesPerDamage = 9f;
        public const float MaxVelocity = 950f;
        /// <summary>Share of the impulse that reaches the next bone up the chain.</summary>
        public const float ChainFalloff = 0.6f;
        public const int MaxChainDepth = 4;
        /// <summary>Damage from which a hit makes the character stagger.</summary>
        public const float StaggerDamage = 40f;

        public static float PartWeight(BodyPart part) => part switch
        {
            BodyPart.Head => 1.7f,
            BodyPart.Thorax => 1f,
            BodyPart.Stomach => 0.9f,
            BodyPart.LeftArm => 0.7f,
            BodyPart.RightArm => 0.7f,
            _ => 0.6f,
        };

        /// <summary>Angular speed (deg/s) given to the hit bone.</summary>
        public static float Magnitude(float damage, BodyPart part) =>
            Mathf.Min(Mathf.Max(0f, damage) * DegreesPerDamage * PartWeight(part), MaxVelocity);

        /// <summary>Multiplier for the bone <paramref name="depth"/> parents above the hit bone (0 = the hit bone).</summary>
        public static float Attenuation(int depth) =>
            depth < 0 || depth > MaxChainDepth ? 0f : Mathf.Pow(ChainFalloff, depth);

        public static bool Staggers(float damage, BodyPart part) =>
            damage >= StaggerDamage && (part == BodyPart.Head || part == BodyPart.Thorax || part == BodyPart.Stomach);

        /// <summary>Axis (world) that swings a lever <paramref name="lever"/> along <paramref name="shotDirection"/>.</summary>
        public static Vector3 Axis(Vector3 lever, Vector3 shotDirection)
        {
            Vector3 axis = Vector3.Cross(lever, shotDirection);
            if (axis.sqrMagnitude < 1e-6f) axis = Vector3.Cross(Vector3.up, shotDirection);
            return axis.normalized;
=======
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
>>>>>>> 0c260b25b241536a1c414610faff92f989e73203
        }
    }
}
