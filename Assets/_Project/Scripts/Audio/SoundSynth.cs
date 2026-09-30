using System;

namespace Polykov.Audio
{
    /// <summary>
    /// Pure sample synthesis for placeholder sounds (no audio assets yet). Deterministic per seed.
    /// Everything returns mono float samples in [-1, 1]; <see cref="ProceduralSounds"/> turns them into clips.
    /// </summary>
    public static class SoundSynth
    {
        public const int SampleRate = 44100;

        /// <summary>.45 ACP pistol: sharp crack, low body thump and a short room tail.</summary>
        public static float[] Gunshot(int seed)
        {
            var rng = new Random(seed);
            float[] s = Buffer(0.55f);
            float lp1 = 0f, lp2 = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)SampleRate;
                float n = Noise(rng);
                lp1 += 0.35f * (n - lp1);
                lp2 += 0.06f * (n - lp2);
                float crack = n * Exp(t, 0.004f) * 1.1f;
                float body = lp1 * Exp(t, 0.035f) * 1.4f;
                float thump = (float)Math.Sin(2.0 * Math.PI * (70f + 60f * Exp(t, 0.02f)) * t) * Exp(t, 0.06f) * 0.9f;
                float tail = lp2 * Exp(t, 0.16f) * 1.6f;
                s[i] = SoftClip(crack + body + thump + tail);
            }
            return Fade(s, 0.003f);
        }

        /// <summary>Hammer falling on an empty chamber.</summary>
        public static float[] DryClick(int seed) => Click(seed, 0.05f, 3200f, 0.004f, 0.6f);

        /// <summary>Magazine release and pull.</summary>
        public static float[] MagazineOut(int seed)
        {
            float[] a = Click(seed, 0.12f, 1800f, 0.008f, 0.5f);
            Mix(a, Rattle(seed + 1, 0.12f, 0.02f), 0.03f);
            return a;
        }

        /// <summary>Magazine seating.</summary>
        public static float[] MagazineIn(int seed)
        {
            float[] a = Rattle(seed, 0.14f, 0.3f);
            Mix(a, Click(seed + 7, 0.1f, 1400f, 0.01f, 0.9f), 0.05f);
            return a;
        }

        /// <summary>Slide slamming forward.</summary>
        public static float[] SlideRelease(int seed)
        {
            float[] a = Click(seed, 0.18f, 2400f, 0.012f, 1f);
            Mix(a, Click(seed + 3, 0.1f, 900f, 0.02f, 0.5f), 0.004f);
            return a;
        }

        /// <summary>Boot on a hard floor. Variation comes from the seed.</summary>
        public static float[] Footstep(int seed)
        {
            var rng = new Random(seed);
            float[] s = Buffer(0.16f);
            float lp = 0f;
            float cutoff = 0.08f + (float)rng.NextDouble() * 0.05f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)SampleRate;
                lp += cutoff * (Noise(rng) - lp);
                float heel = lp * Exp(t, 0.012f) * 2.2f;
                float toe = t > 0.035f ? lp * Exp(t - 0.035f, 0.02f) * 1.4f : 0f;
                s[i] = SoftClip(heel + toe);
            }
            return Fade(s, 0.002f);
        }

        /// <summary>Body landing: low thud plus gear rattle.</summary>
        public static float[] Land(int seed)
        {
            var rng = new Random(seed);
            float[] s = Buffer(0.3f);
            float lp = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)SampleRate;
                lp += 0.03f * (Noise(rng) - lp);
                float thud = (float)Math.Sin(2.0 * Math.PI * 55.0 * t) * Exp(t, 0.05f) * 0.8f + lp * Exp(t, 0.04f) * 3f;
                s[i] = SoftClip(thud);
            }
            Mix(s, Rattle(seed + 5, 0.2f, 0.15f), 0.01f);
            return Fade(s, 0.002f);
        }

        private static float[] Click(int seed, float length, float frequency, float decay, float gain)
        {
            var rng = new Random(seed);
            float[] s = Buffer(length);
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)SampleRate;
                float tone = (float)Math.Sin(2.0 * Math.PI * frequency * t) * 0.6f;
                s[i] = SoftClip((Noise(rng) * 0.8f + tone) * Exp(t, decay) * gain);
            }
            return Fade(s, 0.001f);
        }

        private static float[] Rattle(int seed, float length, float gain)
        {
            var rng = new Random(seed);
            float[] s = Buffer(length);
            float hp = 0f, prev = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)SampleRate;
                float n = Noise(rng);
                hp = 0.9f * (hp + n - prev);
                prev = n;
                float env = Exp(t, length * 0.3f) * (0.5f + 0.5f * (float)Math.Abs(Math.Sin(t * 180.0)));
                s[i] = hp * env * gain;
            }
            return Fade(s, 0.002f);
        }

        private static void Mix(float[] target, float[] source, float offsetSeconds)
        {
            int offset = (int)(offsetSeconds * SampleRate);
            for (int i = 0; i < source.Length && i + offset < target.Length; i++)
                target[i + offset] = SoftClip(target[i + offset] + source[i]);
        }

        private static float[] Buffer(float seconds) => new float[(int)(seconds * SampleRate)];
        private static float Noise(Random rng) => (float)(rng.NextDouble() * 2.0 - 1.0);
        private static float Exp(float t, float tau) => (float)Math.Exp(-t / tau);
        private static float SoftClip(float x) => (float)Math.Tanh(x);

        private static float[] Fade(float[] s, float seconds)
        {
            int n = Math.Min(s.Length, (int)(seconds * SampleRate));
            for (int i = 0; i < n; i++) s[s.Length - 1 - i] *= i / (float)n;
            return s;
        }
    }
}
