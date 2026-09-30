using UnityEngine;

namespace Polykov.Movement
{
    /// <summary>
    /// Pure, deterministic character movement simulation.
    /// Same inputs + same state = same result, on client (prediction) and server (authority).
    /// No physics queries, no Unity objects, no allocations.
    /// </summary>
    public static class MovementMotor
    {
        private const float InputDeadzone = 0.01f;

        public static MovementState Step(in MovementState state, in MovementInput input, in GroundInfo ground,
            in MovementTuning tuning, float deltaTime)
        {
            // While rising (just jumped) the probe may still see the floor: ignore it until we fall back.
            bool grounded = ground.Grounded && state.VerticalSpeed <= 0f;

            Vector2 move = Vector2.ClampMagnitude(input.Move, 1f);
            float magnitude = move.magnitude;
            bool hasInput = magnitude > InputDeadzone;
            Vector2 direction = hasInput ? move / magnitude : Vector2.zero;

            bool sprinting = hasInput && input.Sprint && !input.Walk && !input.Aim && grounded
                             && direction.y >= Mathf.Cos(tuning.SprintMaxAngle * Mathf.Deg2Rad);

            // Lean: sprinting cancels it; leaning slows you down.
            float leanTarget = sprinting ? 0f : Mathf.Clamp(input.Lean, -1f, 1f);
            float lean = Mathf.MoveTowards(state.Lean, leanTarget, deltaTime / tuning.LeanTime);

            float baseSpeed = sprinting ? tuning.SprintSpeed : input.Walk ? tuning.WalkSpeed : tuning.RunSpeed;
            Quaternion yaw = Quaternion.Euler(0f, input.Yaw, 0f);
            Vector3 worldDirection = yaw * new Vector3(direction.x, 0f, direction.y);

            float targetSpeed = hasInput
                ? baseSpeed * DirectionalFactor(direction, tuning) * magnitude * SlopeFactor(worldDirection, ground, tuning)
                  * Mathf.Lerp(1f, tuning.LeanMoveMultiplier, Mathf.Abs(lean))
                  * (input.Aim ? tuning.AimMoveMultiplier : 1f)
                : 0f;
            Vector3 targetVelocity = worldDirection * targetSpeed;

            Vector3 planar = state.PlanarVelocity;
            planar.y = 0f;

            bool landed = grounded && !state.Grounded;
            float landingImpact = state.LandingImpact;
            if (landed)
            {
                landingImpact = Mathf.Max(0f, -state.VerticalSpeed);
                if (landingImpact > tuning.HardLandingSpeed) planar *= tuning.LandingMomentum;
            }

            planar = Accelerate(planar, worldDirection, targetSpeed, grounded, tuning, deltaTime);

            float jumpBuffer = input.Jump ? tuning.JumpBufferTime : Mathf.Max(0f, state.JumpBuffer - deltaTime);
            float coyote = grounded ? tuning.CoyoteTime : Mathf.Max(0f, state.CoyoteTime - deltaTime);
            bool jump = jumpBuffer > 0f && (grounded || coyote > 0f) && state.VerticalSpeed <= 0f && tuning.JumpHeight > 0f;

            float vertical;
            Vector3 velocity;
            if (jump)
            {
                vertical = Mathf.Sqrt(2f * tuning.Gravity * tuning.JumpHeight);
                velocity = planar + Vector3.up * vertical;
                grounded = false;
                jumpBuffer = 0f;
                coyote = 0f;
            }
            else if (grounded)
            {
                vertical = -tuning.GroundStickSpeed;
                velocity = AlignToGround(planar, ground.Normal) + Vector3.up * vertical;
            }
            else
            {
                vertical = Mathf.Max(state.VerticalSpeed - tuning.Gravity * deltaTime, -tuning.TerminalFallSpeed);
                velocity = planar + Vector3.up * vertical;
            }

            return new MovementState
            {
                PlanarVelocity = planar,
                VerticalSpeed = vertical,
                Velocity = velocity,
                Grounded = grounded,
                Locomotion = ResolveLocomotion(state.Locomotion, hasInput, sprinting, targetSpeed, planar.magnitude, tuning),
                Lean = lean,
                JumpBuffer = jumpBuffer,
                CoyoteTime = coyote,
                JustJumped = jump,
                JustLanded = landed,
                LandingImpact = landingImpact,
            };
        }

        /// <summary>Blend of forward / strafe / backward multipliers for a unit input direction.</summary>
        public static float DirectionalFactor(Vector2 direction, in MovementTuning tuning)
        {
            float forward = direction.y > 0f ? direction.y * direction.y : 0f;
            float backward = direction.y < 0f ? direction.y * direction.y : 0f;
            float side = direction.x * direction.x;
            return forward + backward * tuning.BackwardMultiplier + side * tuning.StrafeMultiplier;
        }

        private static float SlopeFactor(Vector3 worldDirection, in GroundInfo ground, in MovementTuning tuning)
        {
            if (!ground.Grounded) return 1f;
            Vector3 downhill = new Vector3(ground.Normal.x, 0f, ground.Normal.z);
            float steepness = Mathf.Clamp01(ground.SlopeAngle / tuning.MaxWalkableSlope);
            if (steepness <= 0f || downhill.sqrMagnitude < 1e-6f) return 1f;
            float uphill = Mathf.Clamp01(-Vector3.Dot(worldDirection, downhill.normalized));
            return Mathf.Lerp(1f, tuning.UphillMinMultiplier, uphill * steepness);
        }

        /// <summary>
        /// Velocity is split into the component along the wished direction (accelerates / brakes toward the
        /// target speed) and the lateral remainder, which ground friction removes quickly. Killing the lateral
        /// part fast is what makes direction changes crisp instead of icy.
        /// </summary>
        private static Vector3 Accelerate(Vector3 planar, Vector3 wishDirection, float targetSpeed, bool grounded,
            in MovementTuning tuning, float dt)
        {
            float control = grounded ? 1f : tuning.AirControl;
            float accel = tuning.RunSpeed / tuning.AccelerationTime * control;
            float decel = tuning.RunSpeed / tuning.DecelerationTime * control;
            float turn = tuning.RunSpeed / tuning.TurnTime * control;

            // In the air momentum is kept: no braking, only weak steering.
            if (targetSpeed <= 0f)
                return grounded ? Vector3.MoveTowards(planar, Vector3.zero, decel * dt) : planar;

            float along = Vector3.Dot(planar, wishDirection);
            Vector3 lateral = planar - wishDirection * along;
            lateral = Vector3.MoveTowards(lateral, Vector3.zero, turn * dt);

            float rate;
            if (!grounded && along > targetSpeed) rate = 0f;
            else if (along < 0f || along > targetSpeed) rate = decel;
            else if (targetSpeed > tuning.RunSpeed && along >= tuning.RunSpeed * 0.95f)
                rate = Mathf.Max(tuning.SprintSpeed - tuning.RunSpeed, 0.01f) / tuning.SprintAccelerationTime * control;
            else rate = accel;
            along = Mathf.MoveTowards(along, targetSpeed, rate * dt);

            return wishDirection * along + lateral;
        }

        private static Vector3 AlignToGround(Vector3 planar, Vector3 normal)
        {
            float speed = planar.magnitude;
            if (speed < 1e-5f) return Vector3.zero;
            Vector3 along = Vector3.ProjectOnPlane(planar, normal);
            return along.sqrMagnitude < 1e-8f ? planar : along.normalized * speed;
        }

        private static LocomotionState ResolveLocomotion(LocomotionState previous, bool hasInput, bool sprinting,
            float targetSpeed, float planarSpeed, in MovementTuning tuning)
        {
            if (!hasInput)
                return planarSpeed < tuning.IdleSpeedThreshold ? LocomotionState.Idle : previous;
            if (sprinting) return LocomotionState.Sprint;
            return targetSpeed <= tuning.WalkSpeed * 1.001f ? LocomotionState.Walk : LocomotionState.Run;
        }
    }
}
