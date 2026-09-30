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

            // Crouch: a low ceiling keeps you down even when the key is released.
            bool crouching = input.Crouch || (state.Crouch > 0.001f && ground.CeilingBlocked);
            float crouch = Mathf.MoveTowards(state.Crouch, crouching ? 1f : 0f, deltaTime / tuning.CrouchTime);

            bool staminaEnabled = tuning.MaxStamina > 0f;
            bool exhausted = staminaEnabled && state.Exhausted;
            bool sprinting = hasInput && input.Sprint && !input.Walk && grounded && !crouching && !exhausted
                             && direction.y >= Mathf.Cos(tuning.SprintMaxAngle * Mathf.Deg2Rad);

            // Lean: sprinting cancels it; leaning slows you down.
            float leanTarget = sprinting ? 0f : Mathf.Clamp(input.Lean, -1f, 1f);
            float lean = Mathf.MoveTowards(state.Lean, leanTarget, deltaTime / tuning.LeanTime);

            float baseSpeed = sprinting ? tuning.SprintSpeed : input.Walk ? tuning.WalkSpeed : tuning.RunSpeed;
            baseSpeed = Mathf.Lerp(baseSpeed, Mathf.Min(baseSpeed, tuning.CrouchSpeed), crouch);
            Quaternion yaw = Quaternion.Euler(0f, input.Yaw, 0f);
            Vector3 worldDirection = yaw * new Vector3(direction.x, 0f, direction.y);

            float targetSpeed = hasInput
                ? baseSpeed * DirectionalFactor(direction, tuning) * magnitude * SlopeFactor(worldDirection, ground, tuning)
                  * Mathf.Lerp(1f, tuning.LeanMoveMultiplier, Mathf.Abs(lean))
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

            float rate = AccelerationRate(planar, targetVelocity, targetSpeed, tuning);
            if (!grounded) rate *= tuning.AirControl;
            planar = Vector3.MoveTowards(planar, targetVelocity, rate * deltaTime);

            float jumpBuffer = input.Jump ? tuning.JumpBufferTime : Mathf.Max(0f, state.JumpBuffer - deltaTime);
            float coyote = grounded ? tuning.CoyoteTime : Mathf.Max(0f, state.CoyoteTime - deltaTime);
            bool jump = jumpBuffer > 0f && (grounded || coyote > 0f) && state.VerticalSpeed <= 0f && tuning.JumpHeight > 0f
                        && !crouching && crouch < 0.5f && !exhausted;

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

            UpdateStamina(state, tuning, sprinting, jump, hasInput, deltaTime,
                out float staminaUsed, out float regenDelay, out bool nowExhausted);

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
                Crouch = crouch,
                Crouching = crouching,
                StaminaUsed = staminaUsed,
                StaminaRegenDelay = regenDelay,
                Exhausted = nowExhausted,
            };
        }

        private static void UpdateStamina(in MovementState state, in MovementTuning tuning, bool sprinting, bool jumped,
            bool moving, float deltaTime, out float used, out float regenDelay, out bool exhausted)
        {
            used = state.StaminaUsed;
            regenDelay = state.StaminaRegenDelay;
            exhausted = state.Exhausted;
            if (tuning.MaxStamina <= 0f)
            {
                used = 0f;
                exhausted = false;
                return;
            }

            float spent = (sprinting ? tuning.SprintStaminaDrain * deltaTime : 0f) + (jumped ? tuning.JumpStaminaCost : 0f);
            if (spent > 0f)
            {
                used = Mathf.Min(tuning.MaxStamina, used + spent);
                regenDelay = tuning.StaminaRegenDelay;
                if (used >= tuning.MaxStamina) exhausted = true;
            }
            else if (regenDelay > 0f)
            {
                regenDelay = Mathf.Max(0f, regenDelay - deltaTime);
            }
            else
            {
                float rate = tuning.StaminaRegenRate * (moving ? 0.7f : 1f);
                used = Mathf.Max(0f, used - rate * deltaTime);
            }

            if (exhausted && used <= tuning.MaxStamina * (1f - tuning.ExhaustedRecovery)) exhausted = false;
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

        private static float AccelerationRate(Vector3 current, Vector3 target, float targetSpeed, in MovementTuning tuning)
        {
            float currentSpeed = current.magnitude;
            bool braking = targetSpeed < currentSpeed - 1e-3f || Vector3.Dot(target, current) < 0f;
            if (braking) return tuning.RunSpeed / tuning.DecelerationTime;

            bool enteringSprint = targetSpeed > tuning.RunSpeed && currentSpeed >= tuning.RunSpeed * 0.95f;
            if (enteringSprint) return Mathf.Max(tuning.SprintSpeed - tuning.RunSpeed, 0.01f) / tuning.SprintAccelerationTime;

            return tuning.RunSpeed / tuning.AccelerationTime;
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
