using UnityEngine;

namespace Polykov.Combat
{
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
        }
    }
}
