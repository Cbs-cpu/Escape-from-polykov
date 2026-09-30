using NUnit.Framework;
using UnityEngine;

namespace Polykov.Combat.Tests
{
    public class HitReactionTests
    {
        private static HitReactionTuning T => HitReactionTuning.Default;

        [Test]
        public void Flinch_GrowsWithImpulseAndDamage_AndIsCapped()
        {
            float small = HitReactionModel.Degrees(BodyPart.Thorax, 2f, 20f, T);
            float big = HitReactionModel.Degrees(BodyPart.Thorax, 6f, 62f, T);
            Assert.Greater(big, small);
            Assert.AreEqual(T.MaxDegrees, HitReactionModel.Degrees(BodyPart.Head, 500f, 500f, T), 1e-4f);
            Assert.AreEqual(0f, HitReactionModel.Degrees(BodyPart.Thorax, 0f, 0f, T), 1e-6f);
        }

        [Test]
        public void HeadFlinchesMoreThanLegs_ForTheSameBullet()
        {
            Assert.Greater(HitReactionModel.Degrees(BodyPart.Head, 4f, 62f, T), HitReactionModel.Degrees(BodyPart.LeftLeg, 4f, 62f, T));
        }

        [Test]
        public void ChainAttenuation_DecaysAndEnds()
        {
            Assert.AreEqual(1f, HitReactionModel.Attenuation(0, T));
            Assert.Less(HitReactionModel.Attenuation(1, T), 1f);
            Assert.Less(HitReactionModel.Attenuation(2, T), HitReactionModel.Attenuation(1, T));
            Assert.AreEqual(0f, HitReactionModel.Attenuation(T.ChainLength, T));
            Assert.AreEqual(0f, HitReactionModel.Attenuation(-1, T));
        }

        [Test]
        public void Axis_PushesTheFarEndAlongTheShot()
        {
            // A vertical bone shot from the front (+Z direction): rotating about the axis tilts its top toward +Z.
            Vector3 axis = HitReactionModel.Axis(Vector3.up, Vector3.forward);
            Vector3 tilted = Quaternion.AngleAxis(10f, axis) * Vector3.up;
            Assert.Greater(tilted.z, 0.1f);
            Assert.AreEqual(Vector3.zero, HitReactionModel.Axis(Vector3.up, Vector3.up));
        }

        [Test]
        public void Spring_PeaksNearTheRequestedDegrees_ThenReturnsToRest()
        {
            var spring = new AngularSpring();
            spring.Kick(Vector3.right, 12f, T.Stiffness, T.Damping);
            float peak = 0f;
            for (int i = 0; i < 600; i++)
            {
                spring.Step(T.Stiffness, T.Damping, 1f / 60f);
                peak = Mathf.Max(peak, spring.Value.magnitude);
                Assert.IsFalse(float.IsNaN(spring.Value.x));
            }
            Assert.AreEqual(12f, peak, 5f);
            Assert.AreEqual(0f, spring.Value.magnitude, 1e-3f, "the body recovers its animated pose");
        }

        [Test]
        public void Spring_IsStableAtLowFrameRates()
        {
            var spring = new AngularSpring();
            spring.Kick(Vector3.up, 25f, T.Stiffness, T.Damping);
            for (int i = 0; i < 400; i++) spring.Step(T.Stiffness, T.Damping, 0.1f);
            Assert.IsFalse(float.IsNaN(spring.Value.y) || float.IsInfinity(spring.Value.y));
            Assert.Less(spring.Value.magnitude, 30f);
        }

        [Test]
        public void Spring_IsDeterministic()
        {
            var a = new AngularSpring();
            var b = new AngularSpring();
            a.Kick(Vector3.forward, 9f, T.Stiffness, T.Damping);
            b.Kick(Vector3.forward, 9f, T.Stiffness, T.Damping);
            for (int i = 0; i < 50; i++)
            {
                a.Step(T.Stiffness, T.Damping, 1f / 60f);
                b.Step(T.Stiffness, T.Damping, 1f / 60f);
            }
            Assert.AreEqual(a.Value, b.Value);
        }
    }
}
