using UnityEngine;

namespace Polykov.Netcode
{
    public enum ReconcileResult : byte
    {
        /// <summary>Prediction matched the server: nothing to do.</summary>
        Confirmed,
        /// <summary>Prediction diverged: state reset to the server's and inputs replayed.</summary>
        Corrected,
        /// <summary>The server state is older than the history (or ahead of it): caller should snap.</summary>
        NoHistory,
    }

    /// <summary>
    /// Client-side prediction history for the local player. Every tick the client records the command it sent and
    /// the state it predicted. When an authoritative state for tick T arrives, <see cref="Reconcile"/> compares it with
    /// the prediction for T and, if they differ, rewinds to the server state and replays commands T+1..latest.
    /// </summary>
    public sealed class ClientPrediction
    {
        private readonly PlayerCommand[] _commands;
        private readonly NetPlayerState[] _states;
        private readonly uint[] _ticks;
        private readonly bool[] _valid;

        public int Capacity { get; }
        public uint LatestTick { get; private set; }
        public bool HasAny { get; private set; }

        /// <summary>Position error (m) above which a prediction is corrected.</summary>
        public float PositionTolerance { get; set; } = 0.02f;
        /// <summary>Velocity error (m/s) above which a prediction is corrected.</summary>
        public float VelocityTolerance { get; set; } = 0.1f;

        public ClientPrediction(int capacity = 256)
        {
            Capacity = capacity;
            _commands = new PlayerCommand[capacity];
            _states = new NetPlayerState[capacity];
            _ticks = new uint[capacity];
            _valid = new bool[capacity];
        }

        /// <summary>Stores the command simulated at <c>command.Tick</c> and the state it produced.</summary>
        public void Record(in PlayerCommand command, in NetPlayerState predicted)
        {
            int i = Index(command.Tick);
            _commands[i] = command;
            _states[i] = predicted;
            _ticks[i] = command.Tick;
            _valid[i] = true;
            if (!HasAny || command.Tick > LatestTick) LatestTick = command.Tick;
            HasAny = true;
        }

        public bool TryGetState(uint tick, out NetPlayerState state)
        {
            int i = Index(tick);
            bool ok = _valid[i] && _ticks[i] == tick;
            state = ok ? _states[i] : default;
            return ok;
        }

        /// <param name="serverTick">Tick the authoritative state belongs to (state after that tick's command).</param>
        /// <param name="corrected">The state for <see cref="LatestTick"/> after reconciliation.</param>
        /// <param name="error">Position error that was found (m).</param>
        public ReconcileResult Reconcile(uint serverTick, in NetPlayerState server, IPlayerSimulator simulator, float deltaTime,
            out NetPlayerState corrected, out float error)
        {
            error = 0f;
            corrected = server;
            if (!TryGetState(serverTick, out NetPlayerState predicted) || serverTick > LatestTick)
                return ReconcileResult.NoHistory;

            error = Vector3.Distance(predicted.Position, server.Position);
            float velocityError = Vector3.Distance(predicted.Movement.Velocity, server.Movement.Velocity);
            if (error <= PositionTolerance && velocityError <= VelocityTolerance)
            {
                TryGetState(LatestTick, out corrected);
                return ReconcileResult.Confirmed;
            }

            NetPlayerState state = server;
            _states[Index(serverTick)] = server;
            for (uint t = serverTick + 1; t <= LatestTick; t++)
            {
                int i = Index(t);
                if (!_valid[i] || _ticks[i] != t) break;
                state = simulator.Simulate(state, _commands[i], deltaTime);
                _states[i] = state;
            }
            corrected = state;
            return ReconcileResult.Corrected;
        }

        private int Index(uint tick) => (int)(tick % (uint)Capacity);
    }

    /// <summary>
    /// Hides reconciliation snaps: the rendered position keeps an offset toward where the player was drawn and the
    /// offset decays to zero quickly. Large errors (teleports, respawns) snap immediately.
    /// </summary>
    public struct VisualErrorSmoother
    {
        public Vector3 Offset;

        /// <param name="previousRendered">Where the player was drawn before the correction (without offset).</param>
        /// <param name="correctedPosition">Simulation position after the correction.</param>
        public void OnCorrection(Vector3 previousRendered, Vector3 correctedPosition, float snapDistance = 1.5f)
        {
            // Accumulate: a second correction while the first is still fading keeps the drawn position continuous.
            Vector3 offset = Offset + (previousRendered - correctedPosition);
            Offset = offset.magnitude > snapDistance ? Vector3.zero : offset;
        }

        /// <param name="rate">Decay speed (1/s); 12-20 hides typical corrections in ~0.1-0.2 s.</param>
        public void Update(float deltaTime, float rate = 15f)
        {
            Offset *= Mathf.Exp(-rate * deltaTime);
            if (Offset.sqrMagnitude < 1e-8f) Offset = Vector3.zero;
        }
    }
}
