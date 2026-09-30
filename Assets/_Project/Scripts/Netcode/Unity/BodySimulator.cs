using Polykov.Movement;
using Polykov.Player;

namespace Polykov.Netcode
{
    /// <summary><see cref="IPlayerSimulator"/> over a real <see cref="CharacterBody"/> (collisions included).</summary>
    public sealed class BodySimulator : IPlayerSimulator
    {
        private readonly CharacterBody _body;
        private readonly System.Func<MovementTuning> _tuning;

        public BodySimulator(CharacterBody body, System.Func<MovementTuning> tuning)
        {
            _body = body;
            _tuning = tuning;
        }

        public NetPlayerState Simulate(in NetPlayerState state, in PlayerCommand command, float deltaTime)
        {
            _body.Teleport(state.Position);
            MovementState next = _body.Step(state.Movement, command.Movement, _tuning(), deltaTime);
            return new NetPlayerState
            {
                Position = _body.Position,
                Yaw = command.Movement.Yaw,
                Pitch = command.Pitch,
                Movement = next,
            };
        }
    }
}
