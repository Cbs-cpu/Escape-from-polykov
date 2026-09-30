using NUnit.Framework;

namespace Polykov.Raid.Tests
{
    public class ExtractionTimerTests
    {
        private const float Dt = 1f / 60f;

        private static ExtractionTimer Run(ExtractionTimer t, bool inside, float seconds)
        {
            int ticks = (int)System.Math.Round(seconds / Dt);
            for (int i = 0; i < ticks; i++) t = ExtractionTimer.Step(t, inside, Dt);
            return t;
        }

        [Test]
        public void StartsOutside()
        {
            var t = ExtractionTimer.Create(7f);
            Assert.AreEqual(ExtractionPhase.Outside, t.Phase);
            Assert.AreEqual(0f, t.Progress, 1e-5f);
        }

        [Test]
        public void StayingInsideForTheDurationExtracts()
        {
            var t = Run(ExtractionTimer.Create(7f), true, 7.02f);
            Assert.AreEqual(ExtractionPhase.Extracted, t.Phase);
            Assert.AreEqual(1f, t.Progress, 1e-5f);
        }

        [Test]
        public void CountsDownWhileInside()
        {
            var t = Run(ExtractionTimer.Create(7f), true, 3f);
            Assert.AreEqual(ExtractionPhase.Counting, t.Phase);
            Assert.AreEqual(4f, t.Remaining, 0.05f);
        }

        [Test]
        public void LeavingResetsTheCountdown()
        {
            var t = Run(ExtractionTimer.Create(7f), true, 5f);
            t = ExtractionTimer.Step(t, false, Dt);
            Assert.AreEqual(ExtractionPhase.Outside, t.Phase);
            Assert.AreEqual(7f, t.Remaining, 1e-5f);
            t = Run(t, true, 5f);
            Assert.AreEqual(ExtractionPhase.Counting, t.Phase);
        }

        [Test]
        public void ExtractedIsTerminal()
        {
            var t = Run(ExtractionTimer.Create(1f), true, 1.1f);
            t = Run(t, false, 2f);
            Assert.AreEqual(ExtractionPhase.Extracted, t.Phase);
        }

        [Test]
        public void IsDeterministic()
        {
            var a = Run(ExtractionTimer.Create(7f), true, 4.3f);
            var b = Run(ExtractionTimer.Create(7f), true, 4.3f);
            Assert.AreEqual(a.Remaining, b.Remaining);
            Assert.AreEqual(a.Phase, b.Phase);
        }
    }
}
