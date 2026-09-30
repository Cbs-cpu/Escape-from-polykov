using NUnit.Framework;
using Polykov.Combat;
using UnityEngine;

namespace Polykov.Combat.Tests
{
    public class HitReactionTests
    {
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
        }
    }
}
