using NUnit.Framework;
<<<<<<< HEAD
using Polykov.Combat;
=======
>>>>>>> 0c260b25b241536a1c414610faff92f989e73203
using UnityEngine;

namespace Polykov.Combat.Tests
{
    public class HitReactionTests
    {
<<<<<<< HEAD
        private static HitSpring Kick(float degPerSec)
        {
            var spring = new HitSpring();
            spring.AddImpulse(Vector3.right * degPerSec);
            return spring;
        }

        [Test]
        public void Spring_ReturnsToRestWithinAboutHalfASecond()
        {
            HitSpring spring = Kick(500f);
            float t = 0f;
            while (t < 0.6f) { spring.Step(1f / 60f, HitReaction.Stiffness, HitReaction.Damping); t += 1f / 60f; }
            Assert.IsTrue(spring.AtRest, "angle " + spring.Angle + " velocity " + spring.Velocity);
        }

        [Test]
        public void Spring_DeflectsInTheImpulseDirectionThenComesBack()
        {
            HitSpring spring = Kick(500f);
            float peak = 0f;
            for (int i = 0; i < 12; i++)
            {
                spring.Step(1f / 60f, HitReaction.Stiffness, HitReaction.Damping);
                peak = Mathf.Max(peak, spring.Angle.x);
            }
            Assert.Greater(peak, 5f);
            Assert.Less(peak, 30f);
        }

        [Test]
        public void Spring_LongFrameStaysStable()
        {
            HitSpring spring = Kick(900f);
            spring.Step(0.5f, HitReaction.Stiffness, HitReaction.Damping);
            Assert.Less(spring.Angle.magnitude, 40f);
        }

        [Test]
        public void HeadReactsMoreThanThoraxThanLimbs()
        {
            float head = HitReaction.Magnitude(40f, BodyPart.Head);
            float thorax = HitReaction.Magnitude(40f, BodyPart.Thorax);
            float leg = HitReaction.Magnitude(40f, BodyPart.LeftLeg);
            Assert.Greater(head, thorax);
            Assert.Greater(thorax, leg);
        }

        [Test]
        public void MagnitudeGrowsWithDamageAndIsCapped()
        {
            Assert.Greater(HitReaction.Magnitude(60f, BodyPart.Thorax), HitReaction.Magnitude(20f, BodyPart.Thorax));
            Assert.AreEqual(HitReaction.MaxVelocity, HitReaction.Magnitude(10000f, BodyPart.Head));
            Assert.AreEqual(0f, HitReaction.Magnitude(-5f, BodyPart.Head));
        }

        [Test]
        public void AttenuationShrinksUpTheChainAndStops()
        {
            Assert.AreEqual(1f, HitReaction.Attenuation(0));
            Assert.Less(HitReaction.Attenuation(1), 1f);
            Assert.Less(HitReaction.Attenuation(2), HitReaction.Attenuation(1));
            Assert.AreEqual(0f, HitReaction.Attenuation(HitReaction.MaxChainDepth + 1));
        }

        [Test]
        public void OnlyBigTorsoOrHeadHitsStagger()
        {
            Assert.IsTrue(HitReaction.Staggers(50f, BodyPart.Thorax));
            Assert.IsFalse(HitReaction.Staggers(20f, BodyPart.Thorax));
            Assert.IsFalse(HitReaction.Staggers(80f, BodyPart.LeftArm));
        }

        [Test]
        public void Axis_IsPerpendicularToLeverAndShot()
        {
            Vector3 axis = HitReaction.Axis(Vector3.up, Vector3.forward);
            Assert.AreEqual(0f, Vector3.Dot(axis, Vector3.up), 1e-4f);
            Assert.AreEqual(0f, Vector3.Dot(axis, Vector3.forward), 1e-4f);
            Assert.AreEqual(1f, axis.magnitude, 1e-4f);
            // Degenerate (lever parallel to the shot) still gives a usable axis.
            Assert.AreEqual(1f, HitReaction.Axis(Vector3.forward, Vector3.forward).magnitude, 1e-4f);
        }
    }

    public class RagdollProfileTests
    {
        [Test]
        public void HasElevenBodiesAndAdultMass()
        {
            Assert.AreEqual(11, RagdollProfile.Count);
            Assert.That(RagdollProfile.TotalMass, Is.InRange(70f, 80f));
        }

        [Test]
        public void EveryBodyButHipsHangsFromAnotherBody()
        {
            for (int i = 1; i < RagdollProfile.Count; i++)
            {
                var bone = (RagdollBone)i;
                Assert.AreNotEqual(bone, RagdollProfile.Parent(bone), bone.ToString());
                Assert.Greater(RagdollProfile.Mass(bone), 0f);
            }
        }

        [Test]
        public void HingesHaveTightSwingAndWideFlexion()
        {
            foreach (RagdollBone bone in new[] { RagdollBone.LeftLowerArm, RagdollBone.RightLowerLeg })
            {
                Assert.IsTrue(RagdollProfile.IsHinge(bone));
                JointLimits l = RagdollProfile.Limits(bone);
                Assert.Less(l.Swing1, 10f);
                Assert.Greater(l.TwistHigh, 100f);
            }
            Assert.IsFalse(RagdollProfile.IsHinge(RagdollBone.Head));
        }

        [Test]
        public void LethalImpulseIsBoundedAndGrowsWithDamage()
        {
            Assert.Greater(RagdollProfile.LethalImpulse(80f, BodyPart.Thorax), RagdollProfile.LethalImpulse(35f, BodyPart.Thorax));
            Assert.LessOrEqual(RagdollProfile.LethalImpulse(1e6f, BodyPart.Thorax), 160f);
            Assert.GreaterOrEqual(RagdollProfile.CorpseImpulse(0f), 15f);
        }

        [Test]
        public void BudgetFreezesTheOldestBeyondTheLimit()
        {
            var budget = new RagdollBudget(3);
            Assert.AreEqual(-1, budget.Register(1));
            Assert.AreEqual(-1, budget.Register(2));
            Assert.AreEqual(-1, budget.Register(3));
            Assert.AreEqual(1, budget.Register(4));
            Assert.AreEqual(2, budget.Register(5));
            Assert.AreEqual(3, budget.Count);
            Assert.IsFalse(budget.Contains(1));
            Assert.IsTrue(budget.Contains(5));
        }

        [Test]
        public void BudgetRemoveFreesASlot()
        {
            var budget = new RagdollBudget(2);
            budget.Register(1);
            budget.Register(2);
            Assert.IsTrue(budget.Remove(1));
            Assert.IsFalse(budget.Remove(1));
            Assert.AreEqual(-1, budget.Register(3));
            Assert.AreEqual(2, budget.Register(4));
=======
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
>>>>>>> 0c260b25b241536a1c414610faff92f989e73203
        }
    }
}
