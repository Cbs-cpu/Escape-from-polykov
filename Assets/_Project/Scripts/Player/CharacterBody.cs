using Polykov.Movement;
using UnityEngine;

namespace Polykov.Player
{
    /// <summary>
    /// One character's physical simulation step: environment probes (ground, headroom), the pure
    /// <see cref="MovementMotor"/>, capsule resize and CharacterController collision. Every tick starts from
    /// (position, MovementState) only — no hidden cached state — so the same step can be re-run for network
    /// reconciliation and runs identically on a server body.
    /// </summary>
    public sealed class CharacterBody
    {
        [System.Serializable]
        public struct Settings
        {
            public LayerMask GroundMask;
            [Tooltip("Extra distance below the capsule that still counts as grounded.")]
            public float GroundProbeDistance;
            [Tooltip("Max drop the character snaps down to stay glued to stairs and slope crests.")]
            public float GroundSnapDistance;
            public float StandingHeight;
            public float CrouchHeight;
        }

        private readonly CharacterController _controller;
        private readonly Transform _transform;
        private readonly Settings _settings;

        public CharacterController Controller => _controller;
        public Vector3 Position => _transform.position;
        /// <summary>Ground under the capsule after the last step.</summary>
        public GroundInfo Ground { get; private set; }
        /// <summary>No room to stand up during the last step.</summary>
        public bool CeilingBlocked { get; private set; }

        public CharacterBody(CharacterController controller, in Settings settings)
        {
            _controller = controller;
            _transform = controller.transform;
            _settings = settings;
            Ground = ProbeGround();
        }

        /// <summary>Moves the capsule without collision (reconciliation, respawn).</summary>
        public void Teleport(Vector3 position)
        {
            if ((_transform.position - position).sqrMagnitude < 1e-12f) return;
            bool enabled = _controller.enabled;
            _controller.enabled = false;
            _transform.position = position;
            _controller.enabled = enabled;
        }

        public MovementState Step(in MovementState state, in MovementInput input, in MovementTuning tuning, float dt)
        {
            SetCapsuleHeight(Mathf.Lerp(_settings.StandingHeight, _settings.CrouchHeight, state.Crouch));
            Vector3 start = _transform.position;
            GroundInfo ground = ProbeGround();
            bool wasGrounded = ground.Grounded;
            CeilingBlocked = IsCeilingBlocked();

            MovementState next = MovementMotor.Step(state, input, ground.WithCeiling(CeilingBlocked), tuning, dt);
            SetCapsuleHeight(Mathf.Lerp(_settings.StandingHeight, _settings.CrouchHeight, next.Crouch));

            CollisionFlags flags = _controller.Move(next.Velocity * dt);
            Ground = ProbeGround();

            // Bumping a ceiling kills upward speed.
            if ((flags & CollisionFlags.Above) != 0 && next.VerticalSpeed > 0f)
                next.VerticalSpeed = 0f;

            if (wasGrounded && !Ground.Grounded && next.VerticalSpeed <= 0f)
                TrySnapToGround();

            // Walls eat momentum: keep planar velocity consistent with what actually happened.
            if ((flags & CollisionFlags.Sides) != 0)
            {
                Vector3 actual = (_transform.position - start) / dt;
                actual.y = 0f;
                if (actual.sqrMagnitude < next.PlanarVelocity.sqrMagnitude)
                    next.PlanarVelocity = actual;
            }
            return next;
        }

        private void SetCapsuleHeight(float height)
        {
            if (Mathf.Abs(_controller.height - height) < 1e-4f) return;
            _controller.height = height;
            _controller.center = new Vector3(_controller.center.x, height * 0.5f, _controller.center.z);
        }

        /// <summary>True when a crouched capsule has no room to grow back to standing height.</summary>
        private bool IsCeilingBlocked()
        {
            float missing = _settings.StandingHeight - _controller.height;
            if (missing < 1e-3f) return false;
            float radius = _controller.radius * 0.95f;
            Vector3 topSphere = _transform.position + _controller.center
                                + Vector3.up * (_controller.height * 0.5f - _controller.radius);
            return Physics.SphereCast(topSphere, radius, Vector3.up, out _, missing + _controller.skinWidth,
                _settings.GroundMask, QueryTriggerInteraction.Ignore);
        }

        private void TrySnapToGround()
        {
            if (!CastDown(_settings.GroundSnapDistance, out RaycastHit hit)) return;
            if (Vector3.Angle(hit.normal, Vector3.up) > _controller.slopeLimit) return;
            _controller.Move(Vector3.down * hit.distance);
            Ground = ProbeGround();
        }

        public GroundInfo ProbeGround()
        {
            if (!CastDown(_settings.GroundProbeDistance + _controller.skinWidth, out RaycastHit hit))
                return GroundInfo.Air;

            // Sphere casts return edge normals on corners; a short ray gives the real surface slope.
            Vector3 normal = hit.normal;
            if (Physics.Raycast(hit.point + Vector3.up * 0.05f, Vector3.down, out RaycastHit ray, 0.1f,
                    _settings.GroundMask, QueryTriggerInteraction.Ignore))
            {
                normal = ray.normal;
            }

            bool walkable = Vector3.Angle(normal, Vector3.up) <= _controller.slopeLimit + 0.5f;
            return new GroundInfo(walkable, walkable ? normal : Vector3.up);
        }

        private bool CastDown(float distance, out RaycastHit hit)
        {
            float radius = _controller.radius * 0.95f;
            Vector3 bottomSphere = _transform.position + _controller.center
                                   + Vector3.down * (_controller.height * 0.5f - _controller.radius);
            Vector3 origin = bottomSphere + Vector3.up * 0.05f;
            return Physics.SphereCast(origin, radius, Vector3.down, out hit, distance + 0.05f, _settings.GroundMask,
                QueryTriggerInteraction.Ignore);
        }
    }
}
