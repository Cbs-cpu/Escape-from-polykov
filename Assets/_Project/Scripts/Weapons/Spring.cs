using UnityEngine;

namespace Polykov.Weapons
{
    /// <summary>
    /// Damped spring on a Vector3, sub-stepped for stability. Impulses go into velocity, the spring pulls the
    /// value back to <see cref="Target"/>. Used for recoil, sway and landing bumps.
    /// </summary>
    public struct SpringVector3
    {
        public Vector3 Value;
        public Vector3 Velocity;
        public Vector3 Target;

        public void AddImpulse(Vector3 impulse) => Velocity += impulse;

        public void Step(float stiffness, float dampingRatio, float dt)
        {
            float c = 2f * Mathf.Sqrt(stiffness) * dampingRatio;
            int steps = Mathf.Max(1, Mathf.CeilToInt(dt / 0.004f));
            float h = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                Velocity += (stiffness * (Target - Value) - c * Velocity) * h;
                Value += Velocity * h;
            }
        }

        /// <summary>Impulse that makes a spring at rest peak close to <paramref name="displacement"/>.</summary>
        public static Vector3 ImpulseFor(Vector3 displacement, float stiffness, float dampingRatio)
        {
            // Peak of an under/critically damped step response scales ~ v0 / omega; empirical factor for zeta 0.5-0.8.
            float omega = Mathf.Sqrt(stiffness);
            float factor = Mathf.Lerp(1.6f, 2.4f, Mathf.InverseLerp(0.5f, 0.9f, dampingRatio));
            return displacement * omega * factor;
        }
    }
}
