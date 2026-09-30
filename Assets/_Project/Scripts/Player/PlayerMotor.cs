using Polykov.Input;
using Polykov.Movement;
using UnityEngine;

namespace Polykov.Player
{
    /// <summary>
    /// Runs the pure <see cref="MovementMotor"/> at a fixed tick rate, resolves collisions with the
    /// CharacterController, probes the ground, and interpolates the visual between ticks.
    /// The fixed tick is the same model the network layer will use in Phase 2.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DefaultExecutionOrder(-50)]
    public sealed class PlayerMotor : MonoBehaviour
    {
        [SerializeField] private MovementSettings settings;
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerLook look;
        [Tooltip("Presentation root, moved every frame to the interpolated position.")]
        [SerializeField] private Transform visualRoot;

        [Header("Simulation")]
        [SerializeField, Range(20, 128)] private int tickRate = 60;

        [Header("Ground")]
        [SerializeField] private LayerMask groundMask = ~0;
        [Tooltip("Extra distance below the capsule that still counts as grounded.")]
        [SerializeField, Range(0.01f, 0.3f)] private float groundProbeDistance = 0.08f;
        [Tooltip("Max drop the character snaps down to stay glued to stairs and slope crests.")]
        [SerializeField, Range(0f, 0.6f)] private float groundSnapDistance = 0.4f;

        [Header("Crouch")]
        [Tooltip("Capsule height when fully crouched (standing height comes from the CharacterController).")]
        [SerializeField, Range(0.9f, 1.6f)] private float crouchHeight = 1.3f;

        private CharacterController _controller;
        private MovementState _state;
        private GroundInfo _ground;
        private float _accumulator;
        private Vector3 _previousPosition;
        private Vector3 _currentPosition;
        private MovementTuning _tuningOverride;
        private bool _hasTuningOverride;
        private float _standingHeight;
        private CharacterBody _body;

        /// <summary>Raised on the tick the character jumps (presentation hooks: animation, audio later).</summary>
        public event System.Action Jumped;
        /// <summary>Raised on the tick the character lands; argument is the downward impact speed (m/s).</summary>
        public event System.Action<float> Landed;
        /// <summary>Raised after every simulation tick with the tick length. Other tick-based systems (weapon) hook here.</summary>
        public event System.Action<float> Ticked;

        /// <summary>Set by the weapon system: aiming down sights slows movement and blocks sprint.</summary>
        public bool Aiming { get; set; }
        /// <summary>Set by the weapon system while firing/reloading: sprint input is ignored.</summary>
        public bool SprintBlocked { get; set; }

        public MovementState State => _state;
        public GroundInfo Ground => _ground;
        /// <summary>Active tuning: the settings asset, unless a runtime override (preset) is set.</summary>
        public MovementTuning Tuning => _hasTuningOverride ? _tuningOverride : settings.Tuning;
        public bool HasTuningOverride => _hasTuningOverride;
        /// <summary>Name of the active tuning, for debug UI.</summary>
        public string TuningLabel { get; private set; } = "Asset";

        /// <summary>Runs the motor with this tuning instead of the asset (does not modify the asset).</summary>
        public void SetTuningOverride(in MovementTuning tuning, string label)
        {
            _tuningOverride = tuning;
            _hasTuningOverride = true;
            TuningLabel = label;
        }

        public void ClearTuningOverride()
        {
            _hasTuningOverride = false;
            TuningLabel = "Asset";
        }
        public uint Tick { get; private set; }
        public float StandingHeight => _standingHeight;
        /// <summary>The physical simulation (re-runnable for reconciliation).</summary>
        public CharacterBody Body => _body;
        public CharacterBody.Settings BodySettings => new CharacterBody.Settings
        {
            GroundMask = groundMask,
            GroundProbeDistance = groundProbeDistance,
            GroundSnapDistance = groundSnapDistance,
            StandingHeight = _standingHeight,
            CrouchHeight = crouchHeight,
        };
        /// <summary>Input actually simulated on the last tick (after <see cref="InputFilter"/>).</summary>
        public MovementInput LastInput { get; private set; }
        /// <summary>Optional transform of each tick's input (e.g. network quantization). Null = raw input.</summary>
        public System.Func<MovementInput, MovementInput> InputFilter { get; set; }
        /// <summary>Presentation-only offset added to the interpolated position (hides reconciliation snaps).</summary>
        public Vector3 VisualOffset { get; set; }
        /// <summary>No room to stand up this tick (debug/UI).</summary>
        public bool CeilingBlocked { get; private set; }
        /// <summary>Current capsule height (shrinks while crouching).</summary>
        public float CurrentHeight => _controller.height;
        public float TickInterval => 1f / tickRate;
        /// <summary>Smooth position for presentation (camera, visuals), between the last two ticks.</summary>
        public Vector3 InterpolatedPosition { get; private set; }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _controller.slopeLimit = settings.Tuning.MaxWalkableSlope;
            _standingHeight = _controller.height;
            _body = new CharacterBody(_controller, BodySettings);
            _previousPosition = _currentPosition = InterpolatedPosition = transform.position;
            _ground = _body.Ground;
        }

        private void Update()
        {
            float dt = TickInterval;
            // Clamp to avoid a spiral of death after a hitch.
            _accumulator = Mathf.Min(_accumulator + Time.deltaTime, dt * 8f);
            while (_accumulator >= dt)
            {
                Simulate(dt);
                _accumulator -= dt;
            }

            InterpolatedPosition = Vector3.Lerp(_previousPosition, _currentPosition, _accumulator / dt) + VisualOffset;
            if (visualRoot != null) visualRoot.position = InterpolatedPosition;
        }

        private void Simulate(float dt)
        {
            _previousPosition = transform.position;

            var tickInput = new MovementInput(input.Move, look.Yaw, input.SprintHeld && !SprintBlocked, input.WalkHeld,
                input.ConsumeJump(), input.Lean, input.Crouch, Aiming);
            if (InputFilter != null) tickInput = InputFilter(tickInput);
            LastInput = tickInput;

            _state = _body.Step(_state, tickInput, Tuning, dt);
            _ground = _body.Ground;
            CeilingBlocked = _body.CeilingBlocked;
            if (_state.JustJumped) Jumped?.Invoke();
            if (_state.JustLanded) Landed?.Invoke(_state.LandingImpact);

            _currentPosition = transform.position;
            Tick++;
            Ticked?.Invoke(dt);
        }

        /// <summary>
        /// Replaces the simulation state with an authoritative one (network reconciliation). The caller has
        /// already moved <see cref="Body"/> there. Returns how far the simulated position jumped.
        /// </summary>
        public Vector3 ApplyCorrection(in MovementState state, Vector3 position)
        {
            _body.Teleport(position);
            _state = state;
            _ground = _body.ProbeGround();
            Vector3 delta = position - _currentPosition;
            _currentPosition = position;
            _previousPosition += delta;
            return delta;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_controller == null) return;
            Gizmos.color = _ground.Grounded ? Color.green : Color.red;
            Vector3 feet = transform.position + _controller.center + Vector3.down * (_controller.height * 0.5f);
            Gizmos.DrawLine(feet, feet + _ground.Normal * 0.5f);
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(feet, feet + _state.Velocity * 0.25f);
        }
#endif
    }
}
