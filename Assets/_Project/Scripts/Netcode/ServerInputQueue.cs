namespace Polykov.Netcode
{
    /// <summary>
    /// Server-side queue of one client's commands. The server consumes exactly one command per tick in client-tick
    /// order; if the next one hasn't arrived it repeats the last (without one-shot actions) so the player keeps
    /// moving smoothly, and drops the late command when it finally arrives. Clients send each command several times
    /// (redundancy), so duplicates are expected and ignored. Redundant copies only help if they arrive before the
    /// command is due, so the buffered lead (see <see cref="TickRateAdjuster"/>) must be at least redundancy - 1.
    /// </summary>
    public sealed class ServerInputQueue
    {
        private readonly PlayerCommand[] _buffer;
        private readonly bool[] _has;
        private readonly int _capacity;
        private PlayerCommand _last;
        private bool _started;

        /// <summary>Client tick of the last command processed (received or repeated).</summary>
        public uint LastProcessedTick { get; private set; }
        /// <summary>Ticks that had to be repeated because the command was missing.</summary>
        public int MissedCommands { get; private set; }

        public ServerInputQueue(int capacity = 64)
        {
            _capacity = capacity;
            _buffer = new PlayerCommand[capacity];
            _has = new bool[capacity];
        }

        /// <returns>False if the command was a duplicate, too late, or too far ahead.</returns>
        public bool Enqueue(in PlayerCommand command)
        {
            if (_started && command.Tick <= LastProcessedTick) return false;
            if (_started && command.Tick - LastProcessedTick > (uint)_capacity) return false;
            int i = (int)(command.Tick % (uint)_capacity);
            if (_has[i] && _buffer[i].Tick == command.Tick) return false;
            _buffer[i] = command;
            _has[i] = true;
            return true;
        }

        /// <summary>Command to simulate this server tick. False only before the first command ever arrives.</summary>
        public bool TryDequeue(out PlayerCommand command)
        {
            if (!_started)
            {
                // Start from the oldest command we have.
                bool found = false;
                uint oldest = uint.MaxValue;
                for (int i = 0; i < _capacity; i++)
                    if (_has[i] && _buffer[i].Tick < oldest)
                    {
                        oldest = _buffer[i].Tick;
                        found = true;
                    }
                if (!found)
                {
                    command = default;
                    return false;
                }
                _started = true;
                LastProcessedTick = oldest - 1;
            }

            uint next = LastProcessedTick + 1;
            int slot = (int)(next % (uint)_capacity);
            if (_has[slot] && _buffer[slot].Tick == next)
            {
                command = _buffer[slot];
                _has[slot] = false;
            }
            else
            {
                command = _last.AsRepeat(next);
                MissedCommands++;
            }
            _last = command;
            LastProcessedTick = next;
            return true;
        }

        /// <summary>Commands waiting (ahead of the last processed). Sent back to the client for tick-rate sync.</summary>
        public int BufferedCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _capacity; i++)
                    if (_has[i] && (!_started || _buffer[i].Tick > LastProcessedTick)) n++;
                return n;
            }
        }
    }

    /// <summary>
    /// Keeps the client's command stream a little ahead of the server: the server reports how many commands it
    /// has buffered; the client runs its tick clock slightly faster or slower until that matches the target.
    /// </summary>
    public struct TickRateAdjuster
    {
        public float TimeScale;

        /// <param name="buffered">Commands waiting on the server (from the latest server message).</param>
        /// <param name="target">Desired buffer (2-3 ticks absorbs jitter).</param>
        public float Update(int buffered, int target = 2, float gain = 0.02f, float maxAdjust = 0.08f)
        {
            float error = target - buffered;
            float scale = 1f + UnityEngine.Mathf.Clamp(error * gain, -maxAdjust, maxAdjust);
            TimeScale = TimeScale <= 0f ? scale : UnityEngine.Mathf.Lerp(TimeScale, scale, 0.2f);
            return TimeScale;
        }
    }
}
