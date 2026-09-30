using NUnit.Framework;

namespace Polykov.Combat.Tests
{
    public class HealthModelTests
    {
        private static HealthTuning T => HealthTuning.Default;
        private static HealthState Full => HealthState.Full(T);

        private static HealthState Hit(HealthState s, BodyPart part, float damage)
            => HealthModel.ApplyDamage(s, part, damage, T, out _);

        [Test]
        public void Full_HasEveryPartAtMax_AndIsAlive()
        {
            HealthState s = Full;
            Assert.IsTrue(s.Alive);
            for (int i = 0; i < HealthTuning.PartCount; i++)
                Assert.AreEqual(T.Max((BodyPart)i), s[(BodyPart)i]);
            Assert.AreEqual(T.Total, s.Total, 1e-3f);
        }

        [Test]
        public void Damage_ComesOffTheHitPart()
        {
            HealthState s = HealthModel.ApplyDamage(Full, BodyPart.Stomach, 20f, T, out DamageResult r);
            Assert.AreEqual(T.Stomach - 20f, s.Stomach, 1e-4f);
            Assert.AreEqual(20f, r.Applied, 1e-4f);
            Assert.IsFalse(r.Spread);
            Assert.IsTrue(s.Alive);
        }

        [Test]
        public void HeadOrThoraxAtZero_Kills()
        {
            HealthState head = HealthModel.ApplyDamage(Full, BodyPart.Head, 40f, T, out DamageResult r);
            Assert.IsFalse(head.Alive);
            Assert.IsTrue(r.Killed);
            Assert.AreEqual(BodyPart.Head, head.KillingPart);

            HealthState thorax = Hit(Hit(Full, BodyPart.Thorax, 50f), BodyPart.Thorax, 50f);
            Assert.IsFalse(thorax.Alive);
            Assert.AreEqual(BodyPart.Thorax, thorax.KillingPart);
        }

        [Test]
        public void DestroyingALimb_DoesNotKill()
        {
            HealthState s = HealthModel.ApplyDamage(Full, BodyPart.LeftArm, 500f, T, out DamageResult r);
            Assert.IsTrue(s.Alive);
            Assert.IsTrue(r.DestroyedPart);
            Assert.AreEqual(0f, s.LeftArm);
            Assert.AreEqual(T.Arm, r.Applied, 1e-4f, "overkill is clamped at the part's HP");
        }

        [Test]
        public void HitOnDestroyedLimb_SpreadsScaledDamageOverRemainingParts()
        {
            HealthState s = Hit(Full, BodyPart.LeftLeg, 500f);
            float before = s.Total;
            s = HealthModel.ApplyDamage(s, BodyPart.LeftLeg, 30f, T, out DamageResult r);
            Assert.IsTrue(r.Spread);
            Assert.AreEqual(30f * T.LegSpread, before - s.Total, 1e-3f);
            // Six parts still had HP: each took an equal share.
            Assert.AreEqual(T.Head - 30f * T.LegSpread / 6f, s.Head, 1e-3f);
        }

        [Test]
        public void SpreadDamage_CanKill()
        {
            HealthState s = Hit(Full, BodyPart.Stomach, 500f);
            for (int i = 0; i < 40 && s.Alive; i++) s = Hit(s, BodyPart.Stomach, 50f);
            Assert.IsFalse(s.Alive);
        }

        [Test]
        public void DeadBodies_TakeNoMoreDamage()
        {
            HealthState dead = Hit(Full, BodyPart.Head, 100f);
            HealthState after = HealthModel.ApplyDamage(dead, BodyPart.Thorax, 10f, T, out DamageResult r);
            Assert.AreEqual(dead.Thorax, after.Thorax);
            Assert.AreEqual(0f, r.Applied);
        }

        [Test]
        public void M1911_TwoThoraxHitsKill_OneHeadshotKills()
        {
            const float roundDamage = 62f; // .45 ACP FMJ, WeaponDefinition default
            Assert.IsTrue(Hit(Full, BodyPart.Thorax, roundDamage).Alive);
            Assert.IsFalse(Hit(Hit(Full, BodyPart.Thorax, roundDamage), BodyPart.Thorax, roundDamage).Alive);
            Assert.IsFalse(Hit(Full, BodyPart.Head, roundDamage).Alive);
        }

        [Test]
        public void DestroyedLegs_AreCounted()
        {
            HealthState s = Hit(Hit(Full, BodyPart.LeftLeg, 100f), BodyPart.RightLeg, 100f);
            Assert.AreEqual(2, s.DestroyedLegs);
        }
    }
}
