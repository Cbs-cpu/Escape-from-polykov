using Polykov.Movement;
using UnityEngine;

namespace Polykov.Netcode
{
    /// <summary>
    /// Authoritative state of a player at the end of a tick: what the server sends to the owner (reconciliation)
    /// and to everyone else (interpolation). View angles are included because remote bodies need them.
    /// </summary>
    public struct NetPlayerState
    {
        public Vector3 Position;
        public float Yaw;
        public float Pitch;
        public MovementState Movement;
    }

    /// <summary>Runs one tick of a player's simulation. Implemented by the Unity side (collisions) and by tests.</summary>
    public interface IPlayerSimulator
    {
        NetPlayerState Simulate(in NetPlayerState state, in PlayerCommand command, float deltaTime);
    }
}
