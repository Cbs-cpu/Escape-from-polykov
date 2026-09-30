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

        private CharacterController _controller;
        private MovementState _state;
        private GroundInfo _ground;
        private float _accumulator;
        private Vector3 _previousPosition;
        private Vector3 _currentPosition;

        public MovementState State => _state;
        public GroundInfo Ground => _ground;
        public MovementTuning Tuning => settings.Tuning;
        public uint Tick { get; private set; }
        public float TickInterval => 1f / tickRate;
        /// <summary>Smooth position for presentation (camera, visuals), between the last two ticks.</summary>
        public Vector3 InterpolatedPosition { get; private set; }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _controller.slopeLimit = settings.Tuning.MaxWalkableSlope;
            _previousPosition = _currentPosition = InterpolatedPosition = transform.position;
            _ground = ProbeGround();
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

            InterpolatedPosition = Vector3.Lerp(_previousPosition, _currentPosition, _accumulator / dt);
            if (visualRoot != null) visualRoot.position = InterpolatedPosition;
        }

        private void Simulate(float dt)
        {
            _previousPosition = transform.position;
            bool wasGrounded = _ground.Grounded;

            var tickInput = new MovementInput(input.Move, look.Yaw, input.SprintHeld, input.WalkHeld);
            _state = MovementMotor.Step(_state, tickInput, _ground, settings.Tuning, dt);

            CollisionFlags flags = _controller.Move(_state.Velocity * dt);
            _ground = ProbeGround();

            if (wasGrounded && !_ground.Grounded && _state.VerticalSpeed <= 0f)
                TrySnapToGround();

            // Walls eat momentum: keep planar velocity consistent with what actually happened.
            if ((flags & CollisionFlags.Sides) != 0)
            {
                Vector3 actual = (transform.position - _previousPosition) / dt;
                actual.y = 0f;
                if (actual.sqrMagnitude < _state.PlanarVelocity.sqrMagnitude)
                    _state.PlanarVelocity = actual;
            }

            _currentPosition = transform.position;
            Tick++;
        }

        private void TrySnapToGround()
        {
            if (!CastDown(groundSnapDistance, out RaycastHit hit)) return;
            if (Vector3.Angle(hit.normal, Vector3.up) > _controller.slopeLimit) return;
            _controller.Move(Vector3.down * hit.distance);
            _ground = ProbeGround();
        }

        private GroundInfo ProbeGround()
        {
            if (!CastDown(groundProbeDistance + _controller.skinWidth, out RaycastHit hit))
                return GroundInfo.Air;

            // Sphere casts return edge normals on corners; a short ray gives the real surface slope.
            Vector3 normal = hit.normal;
            if (Physics.Raycast(hit.point + Vector3.up * 0.05f, Vector3.down, out RaycastHit ray, 0.1f, groundMask,
                    QueryTriggerInteraction.Ignore))
            {
                normal = ray.normal;
            }

            bool walkable = Vector3.Angle(normal, Vector3.up) <= _controller.slopeLimit + 0.5f;
            return new GroundInfo(walkable, walkable ? normal : Vector3.up);
        }

        private bool CastDown(float distance, out RaycastHit hit)
        {
            float radius = _controller.radius * 0.95f;
            Vector3 bottomSphere = transform.position + _controller.center
                                   + Vector3.down * (_controller.height * 0.5f - _controller.radius);
            Vector3 origin = bottomSphere + Vector3.up * 0.05f;
            return Physics.SphereCast(origin, radius, Vector3.down, out hit, distance + 0.05f, groundMask,
                QueryTriggerInteraction.Ignore);
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
