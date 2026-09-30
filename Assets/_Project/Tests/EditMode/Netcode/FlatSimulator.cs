using Polykov.Movement;
using UnityEngine;

namespace Polykov.Netcode.Tests
{
    /// <summary>Test simulator: the real movement motor on an infinite flat floor (no collisions).</summary>
    public sealed class FlatSimulator : IPlayerSimulator
    {
        private readonly MovementTuning _tuning = MovementTuning.Default;

        public NetPlayerState Simulate(in NetPlayerState state, in PlayerCommand command, float deltaTime)
        {
            NetPlayerState next = state;
            next.Movement = MovementMotor.Step(state.Movement, command.Movement, GroundInfo.Flat, _tuning, deltaTime);
            Vector3 v = next.Movement.Velocity;
            v.y = 0f;
            next.Position += v * deltaTime;
            next.Yaw = command.Movement.Yaw;
            next.Pitch = command.Pitch;
            return next;
        }
    }
}
