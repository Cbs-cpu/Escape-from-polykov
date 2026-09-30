using UnityEngine;

namespace Polykov.Movement
{
    /// <summary>
    /// One tick of player intent. This is the exact payload the client will send to the server
    /// in Phase 2, so it must stay small and free of presentation data.
    /// </summary>
    public readonly struct MovementInput
    {
        /// <summary>x = strafe (right +), y = forward (+). Magnitude is clamped to 1 by the motor.</summary>
        public readonly Vector2 Move;
        /// <summary>Body yaw in degrees, world space.</summary>
        public readonly float Yaw;
        public readonly bool Sprint;
        public readonly bool Walk;
        /// <summary>Jump was pressed since the previous tick (edge, not held).</summary>
        public readonly bool Jump;
        /// <summary>Desired lean, -1 = full left, +1 = full right.</summary>
        public readonly float Lean;

        public MovementInput(Vector2 move, float yaw, bool sprint, bool walk)
            : this(move, yaw, sprint, walk, false, 0f)
        {
        }

        public MovementInput(Vector2 move, float yaw, bool sprint, bool walk, bool jump, float lean)
        {
            Move = move;
            Yaw = yaw;
            Sprint = sprint;
            Walk = walk;
            Jump = jump;
            Lean = lean;
        }
    }
}
