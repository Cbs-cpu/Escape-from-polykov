namespace Polykov.Raid
{
    public enum ExtractionPhase : byte
    {
        /// <summary>Not inside an extraction zone.</summary>
        Outside = 0,
        /// <summary>Inside, counting down.</summary>
        Counting = 1,
        /// <summary>Countdown finished: the player leaves the raid (terminal).</summary>
        Extracted = 2,
    }

    /// <summary>
    /// Tarkov-style extraction: stay inside the zone for <see cref="Duration"/> seconds. Leaving resets the countdown;
    /// once extracted it stays extracted. Pure value type, stepped by the server in multiplayer (the client only shows it).
    /// </summary>
    public struct ExtractionTimer
    {
        public float Duration;
        public float Remaining;
        public ExtractionPhase Phase;

        public static ExtractionTimer Create(float duration)
        {
            if (duration < 0f) duration = 0f;
            return new ExtractionTimer { Duration = duration, Remaining = duration, Phase = ExtractionPhase.Outside };
        }

        /// <summary>Progress 0..1 of the current countdown.</summary>
        public float Progress => Duration <= 0f ? (Phase == ExtractionPhase.Outside ? 0f : 1f) : 1f - Remaining / Duration;

        public static ExtractionTimer Step(ExtractionTimer t, bool inside, float dt)
        {
            if (t.Phase == ExtractionPhase.Extracted) return t;
            if (!inside)
            {
                t.Phase = ExtractionPhase.Outside;
                t.Remaining = t.Duration;
                return t;
            }
            t.Phase = ExtractionPhase.Counting;
            if (dt > 0f) t.Remaining -= dt;
            if (t.Remaining <= 0f)
            {
                t.Remaining = 0f;
                t.Phase = ExtractionPhase.Extracted;
            }
            return t;
        }
    }
}
