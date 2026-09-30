using Polykov.Movement;
using UnityEngine;

namespace Polykov.Netcode
{
    /// <summary>
    /// Remote-player interpolation: stores authoritative snapshots by tick and samples them at a render tick that is
    /// a few ticks in the past, so there are always two snapshots to blend between. Short gaps are extrapolated with
    /// the last velocity (bounded), longer ones hold the last state. Also serves as position history for server-side
    /// lag compensation (rewind hitboxes to the shooter's view tick).
    /// </summary>
    public sealed class SnapshotBuffer
    {
        private readonly NetPlayerState[] _states;
        private readonly uint[] _ticks;
        private readonly bool[] _valid;

        public int Capacity { get; }
        public uint NewestTick { get; private set; }
        public bool HasAny { get; private set; }
        /// <summary>Max ticks to extrapolate past the newest snapshot.</summary>
        public float MaxExtrapolationTicks { get; set; } = 6f;

        public SnapshotBuffer(int capacity = 64)
        {
            Capacity = capacity;
            _states = new NetPlayerState[capacity];
            _ticks = new uint[capacity];
            _valid = new bool[capacity];
        }

        public void Add(uint tick, in NetPlayerState state)
        {
            if (HasAny && NewestTick >= tick && NewestTick - tick >= (uint)Capacity) return; // far too old
            int i = (int)(tick % (uint)Capacity);
            _states[i] = state;
            _ticks[i] = tick;
            _valid[i] = true;
            if (!HasAny || tick > NewestTick) NewestTick = tick;
            HasAny = true;
        }

        /// <param name="renderTick">Fractional tick to display (typically serverTick - interpolation delay).</param>
        /// <param name="tickInterval">Seconds per tick (for extrapolation).</param>
        public bool Sample(float renderTick, float tickInterval, out NetPlayerState result)
        {
            result = default;
            if (!HasAny) return false;

            if (renderTick >= NewestTick)
            {
                Get(NewestTick, out NetPlayerState newest);
                float ahead = Mathf.Min(renderTick - NewestTick, MaxExtrapolationTicks);
                result = newest;
                result.Position += newest.Movement.Velocity * (ahead * tickInterval);
                return true;
            }

            // Find the snapshots around renderTick (walking back over gaps from lost packets).
            uint from = (uint)Mathf.FloorToInt(renderTick);
            uint oldestAllowed = NewestTick >= (uint)Capacity ? NewestTick - (uint)Capacity + 1 : 0;
            uint a = from;
            while (!Has(a) && a > oldestAllowed) a--;
            if (!Has(a))
            {
                // Older than anything we keep: show the oldest we have.
                uint b0 = from + 1;
                while (!Has(b0) && b0 < NewestTick) b0++;
                return Get(b0, out result);
            }
            uint b = a + 1;
            while (!Has(b) && b < NewestTick) b++;
            Get(a, out NetPlayerState sa);
            Get(b, out NetPlayerState sb);
            float t = b == a ? 0f : Mathf.Clamp01((renderTick - a) / (b - a));
            result = Blend(sa, sb, t);
            return true;
        }

        private static NetPlayerState Blend(in NetPlayerState a, in NetPlayerState b, float t)
        {
            NetPlayerState r = t < 0.5f ? a : b; // discrete fields from the nearest snapshot
            r.Position = Vector3.Lerp(a.Position, b.Position, t);
            r.Yaw = Mathf.LerpAngle(a.Yaw, b.Yaw, t);
            r.Pitch = Mathf.Lerp(a.Pitch, b.Pitch, t);
            MovementState m = r.Movement;
            m.PlanarVelocity = Vector3.Lerp(a.Movement.PlanarVelocity, b.Movement.PlanarVelocity, t);
            m.Velocity = Vector3.Lerp(a.Movement.Velocity, b.Movement.Velocity, t);
            m.Crouch = Mathf.Lerp(a.Movement.Crouch, b.Movement.Crouch, t);
            m.Lean = Mathf.Lerp(a.Movement.Lean, b.Movement.Lean, t);
            r.Movement = m;
            return r;
        }

        private bool Has(uint tick)
        {
            int i = (int)(tick % (uint)Capacity);
            return _valid[i] && _ticks[i] == tick;
        }

        private bool Get(uint tick, out NetPlayerState state)
        {
            int i = (int)(tick % (uint)Capacity);
            bool ok = _valid[i] && _ticks[i] == tick;
            state = ok ? _states[i] : default;
            return ok;
        }
    }
}
