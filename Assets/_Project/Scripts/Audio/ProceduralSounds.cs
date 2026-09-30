using UnityEngine;

namespace Polykov.Audio
{
    /// <summary>Clip cache for <see cref="SoundSynth"/>; generated once per play session, then reused.</summary>
    public static class ProceduralSounds
    {
        private static AudioClip[] _gunshots;
        private static AudioClip[] _footsteps;
        private static AudioClip _dryClick;
        private static AudioClip _magazineOut;
        private static AudioClip _magazineIn;
        private static AudioClip _slideRelease;
        private static AudioClip _land;

        public static AudioClip Gunshot(int variant) => Get(ref _gunshots, 3, "Gunshot", SoundSynth.Gunshot)[variant % 3];
        public static AudioClip Footstep(int variant) => Get(ref _footsteps, 6, "Footstep", SoundSynth.Footstep)[variant % 6];
        public static AudioClip DryClick => _dryClick != null ? _dryClick : _dryClick = Make("DryClick", SoundSynth.DryClick(11));
        public static AudioClip MagazineOut => _magazineOut != null ? _magazineOut : _magazineOut = Make("MagOut", SoundSynth.MagazineOut(21));
        public static AudioClip MagazineIn => _magazineIn != null ? _magazineIn : _magazineIn = Make("MagIn", SoundSynth.MagazineIn(31));
        public static AudioClip SlideRelease => _slideRelease != null ? _slideRelease : _slideRelease = Make("SlideRelease", SoundSynth.SlideRelease(41));
        public static AudioClip Land => _land != null ? _land : _land = Make("Land", SoundSynth.Land(51));

        private static AudioClip[] Get(ref AudioClip[] cache, int count, string name, System.Func<int, float[]> synth)
        {
            if (cache != null && cache[0] != null) return cache;
            cache = new AudioClip[count];
            for (int i = 0; i < count; i++) cache[i] = Make(name + i, synth(100 + i * 17));
            return cache;
        }

        private static AudioClip Make(string name, float[] samples)
        {
            AudioClip clip = AudioClip.Create(name, samples.Length, 1, SoundSynth.SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
