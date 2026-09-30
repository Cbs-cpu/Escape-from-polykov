using NUnit.Framework;

namespace Polykov.Combat.Tests
{
    public class WoundModelTests
    {
        private static WoundTuning T => WoundTuning.Default;

        private static HealthState Destroyed(BodyPart part)
        {
            HealthState h = HealthState.Full(HealthTuning.Default);
            h[part] = 0f;
            return h;
        }

        private static WoundState WithOverflow(BodyPart part, float v)
        {
            WoundState w = default;
            w.SetOverflow(part, v);
            return w;
        }

        [Test]
        public void UndestroyedPart_NeverSevers()
        {
            HealthState h = HealthState.Full(HealthTuning.Default);
            for (uint i = 0; i < 2000; i++)
            {
                WoundModel.ApplyHit(WithOverflow(BodyPart.LeftArm, 500f), h, BodyPart.LeftArm, 50f, 50f, 1f, i, T, out var e);
                Assert.IsFalse(e.Severed);
            }
        }

        [Test]
        public void BelowThreshold_ChanceIsZero()
        {
            var h = Destroyed(BodyPart.RightArm);
            Assert.AreEqual(0f, WoundModel.Chance(default, h, BodyPart.RightArm, 50f, T.ArmThreshold - 1f, 1f, T));
        }

        [Test]
        public void SameSeed_SameResult()
        {
            var h = Destroyed(BodyPart.LeftLeg);
            var w = WithOverflow(BodyPart.LeftLeg, 60f);
            for (uint seed = 0; seed < 200; seed++)
            {
                var a = WoundModel.ApplyHit(w, h, BodyPart.LeftLeg, 40f, 10f, 1f, seed, T, out var ea);
                var b = WoundModel.ApplyHit(w, h, BodyPart.LeftLeg, 40f, 10f, 1f, seed, T, out var eb);
                Assert.AreEqual(ea.Kind, eb.Kind);
                Assert.AreEqual(a.LeftLegSevered, b.LeftLegSevered);
            }
        }

        [Test]
        public void Chance_GrowsWithOverflowAndDamage_ButIsCapped()
        {
            var h = Destroyed(BodyPart.LeftArm);
            float lo = WoundModel.Chance(default, h, BodyPart.LeftArm, 20f, 25f, 1f, T);
            float moreOverflow = WoundModel.Chance(default, h, BodyPart.LeftArm, 20f, 60f, 1f, T);
            float moreDamage = WoundModel.Chance(default, h, BodyPart.LeftArm, 80f, 25f, 1f, T);
            float huge = WoundModel.Chance(default, h, BodyPart.LeftArm, 5000f, 100000f, 1f, T);
            Assert.Greater(moreOverflow, lo);
            Assert.Greater(moreDamage, lo);
            Assert.AreEqual(T.MaxChance, huge, 1e-6f);
        }

        private static int Count(BodyPart part, WoundState w, float damage, int n)
        {
            var h = Destroyed(part);
            int hits = 0;
            for (uint i = 0; i < n; i++)
            {
                WoundModel.ApplyHit(w, h, part, damage, 10f, 1f, i * 7919u + 13u, T, out var e);
                if (e.Severed) hits++;
            }
            return hits;
        }

        [Test]
        public void Arm_RateOverManyHits_IsInLowBand()
        {
            float rate = Count(BodyPart.LeftArm, WithOverflow(BodyPart.LeftArm, 30f), 40f, 10000) / 10000f;
            Assert.GreaterOrEqual(rate, 0.01f);
            Assert.LessOrEqual(rate, 0.12f);
        }

        [Test]
        public void Head_IsMuchRarerThanArm()
        {
            int arm = Count(BodyPart.LeftArm, WithOverflow(BodyPart.LeftArm, 60f), 40f, 10000);
            int head = Count(BodyPart.Head, WithOverflow(BodyPart.Head, 60f), 40f, 10000);
            Assert.Less(head * 2, arm);
        }

        [Test]
        public void SeveredHead_SetsKillFlag_AndLegSeverDoesNot()
        {
            var h = Destroyed(BodyPart.Head);
            var w = WithOverflow(BodyPart.Head, 100f);
            WoundEvent found = default;
            for (uint i = 0; i < 20000 && !found.Severed; i++)
                WoundModel.ApplyHit(w, h, BodyPart.Head, 60f, 10f, 4f, i, T, out found);
            Assert.IsTrue(found.Severed);
            Assert.IsTrue(found.Kills);

            var hl = Destroyed(BodyPart.RightLeg);
            var wl = WithOverflow(BodyPart.RightLeg, 100f);
            WoundEvent leg = default;
            for (uint i = 0; i < 20000 && !leg.Severed; i++)
                WoundModel.ApplyHit(wl, hl, BodyPart.RightLeg, 60f, 10f, 1f, i, T, out leg);
            Assert.IsTrue(leg.Severed);
            Assert.IsFalse(leg.Kills);
            Assert.AreEqual(1, hl.DestroyedLegs);
        }

        [Test]
        public void Torso_NeverSevers()
        {
            foreach (var part in new[] { BodyPart.Thorax, BodyPart.Stomach })
                Assert.AreEqual(0, Count(part, WithOverflow(part, 1000f), 100f, 3000));
        }

        [Test]
        public void AlreadySevered_DoesNotSeverAgain()
        {
            var h = Destroyed(BodyPart.LeftArm);
            var w = WithOverflow(BodyPart.LeftArm, 100f);
            w.SetSevered(BodyPart.LeftArm);
            for (uint i = 0; i < 3000; i++)
            {
                WoundModel.ApplyHit(w, h, BodyPart.LeftArm, 100f, 50f, 10f, i, T, out var e);
                Assert.IsFalse(e.Severed);
            }
        }

        [Test]
        public void HealthModel_ExposesOverflow()
        {
            var t = HealthTuning.Default;
            HealthModel.ApplyDamage(HealthState.Full(t), BodyPart.LeftArm, t.Arm + 15f, t, out var r);
            Assert.AreEqual(15f, r.Overflow, 1e-3f);
        }
    }
}
