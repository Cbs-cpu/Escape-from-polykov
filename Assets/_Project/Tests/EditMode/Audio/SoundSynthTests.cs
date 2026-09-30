using System;
using NUnit.Framework;

namespace Polykov.Audio.Tests
{
    public class SoundSynthTests
    {
        private static readonly Func<int, float[]>[] All =
        {
            SoundSynth.Gunshot, SoundSynth.DryClick, SoundSynth.MagazineOut, SoundSynth.MagazineIn,
            SoundSynth.SlideRelease, SoundSynth.Footstep, SoundSynth.Land,
        };

        [Test]
        public void EverySound_IsFinite_Audible_AndNotClipping()
        {
            foreach (Func<int, float[]> synth in All)
            {
                float[] samples = synth(3);
                Assert.Greater(samples.Length, 1000, synth.Method.Name);
                float peak = 0f;
                foreach (float s in samples)
                {
                    Assert.IsFalse(float.IsNaN(s) || float.IsInfinity(s), synth.Method.Name);
                    peak = Math.Max(peak, Math.Abs(s));
                }
                Assert.Greater(peak, 0.05f, synth.Method.Name + " is silent");
                Assert.LessOrEqual(peak, 1f, synth.Method.Name + " clips");
                Assert.Less(Math.Abs(samples[samples.Length - 1]), 0.01f, synth.Method.Name + " ends with a click");
            }
        }

        [Test]
        public void SameSeed_SameSamples()
        {
            Assert.AreEqual(SoundSynth.Gunshot(5), SoundSynth.Gunshot(5));
            Assert.AreNotEqual(SoundSynth.Footstep(1), SoundSynth.Footstep(2));
        }
    }
}
