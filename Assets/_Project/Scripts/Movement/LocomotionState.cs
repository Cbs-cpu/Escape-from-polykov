namespace Polykov.Movement
{
    /// <summary>High-level locomotion intent. Replicated in multiplayer, drives animation.</summary>
    public enum LocomotionState : byte
    {
        Idle = 0,
        Walk = 1,
        Run = 2,
        Sprint = 3,
    }
}
