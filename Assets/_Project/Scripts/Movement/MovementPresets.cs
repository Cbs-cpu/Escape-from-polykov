namespace Polykov.Movement
{
    /// <summary>
    /// Named game-feel profiles to compare quickly in play mode against the MovementSettings asset.
    /// Index 0 is the code default (<see cref="MovementTuning.Default"/>); the rest vary inertia and speeds.
    /// </summary>
    public static class MovementPresets
    {
        public static readonly string[] Names = { "Default", "Tactical (heavy)", "Arcade (snappy)" };

        public static int Count => Names.Length;

        public static MovementTuning Get(int index)
        {
            MovementTuning t = MovementTuning.Default;
            switch (index)
            {
                case 1: // Tarkov-like: more mass, longer sprint build-up, less air control.
                    t.RunSpeed = 3.4f;
                    t.SprintSpeed = 5.5f;
                    t.StrafeMultiplier = 0.8f;
                    t.BackwardMultiplier = 0.62f;
                    t.AccelerationTime = 0.3f;
                    t.SprintAccelerationTime = 0.8f;
                    t.DecelerationTime = 0.22f;
                    t.AirControl = 0.04f;
                    t.JumpHeight = 0.42f;
                    t.LandingMomentum = 0.65f;
                    t.LeanTime = 0.24f;
                    t.LeanMoveMultiplier = 0.6f;
                    t.MaxStamina = 6f;
                    t.JumpStaminaCost = 1.4f;
                    break;
                case 2: // Fast and responsive.
                    t.RunSpeed = 4.1f;
                    t.SprintSpeed = 6.4f;
                    t.StrafeMultiplier = 0.95f;
                    t.BackwardMultiplier = 0.85f;
                    t.AccelerationTime = 0.1f;
                    t.SprintAccelerationTime = 0.25f;
                    t.DecelerationTime = 0.07f;
                    t.AirControl = 0.25f;
                    t.JumpHeight = 0.6f;
                    t.LandingMomentum = 0.95f;
                    t.LeanTime = 0.12f;
                    t.LeanMoveMultiplier = 0.8f;
                    t.MaxStamina = 0f; // unlimited
                    break;
            }
            return t;
        }
    }
}
