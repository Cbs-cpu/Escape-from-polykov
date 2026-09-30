using NUnit.Framework;
using UnityEngine;

namespace Polykov.Movement.Tests
{
    public class MovementMotorTests
    {
        private const float Dt = 1f / 60f;
        private const float Tol = 0.05f;

        private static MovementTuning T => MovementTuning.Default;

        private static MovementInput In(float x, float y, float yaw = 0f, bool sprint = false, bool walk = false)
            => new MovementInput(new Vector2(x, y), yaw, sprint, walk);

        private static MovementState Run(MovementState start, MovementInput input, GroundInfo ground, int ticks)
        {
            var tuning = T;
            var s = start;
            for (int i = 0; i < ticks; i++) s = MovementMotor.Step(s, input, ground, tuning, Dt);
            return s;
        }

        private static MovementState Run(MovementInput input, GroundInfo ground, int ticks)
            => Run(default, input, ground, ticks);

        private static int TicksFor(float seconds) => Mathf.CeilToInt(seconds / Dt);

        // Long enough to settle at any target speed.
        private const int Settle = 240;

        [Test]
        public void Standstill_NoInput_StaysIdleWithZeroPlanarVelocity()
        {
            var s = Run(In(0, 0), GroundInfo.Flat, 60);
            Assert.AreEqual(LocomotionState.Idle, s.Locomotion);
            Assert.AreEqual(0f, s.PlanarVelocity.magnitude, 1e-4f);
        }

        [Test]
        public void RunForward_FromRest_ReachesRunSpeedWithinAccelerationTime()
        {
            int ticks = TicksFor(T.AccelerationTime) + 2;
            var s = Run(In(0, 1), GroundInfo.Flat, ticks);
            Assert.AreEqual(T.RunSpeed, s.PlanarSpeed, Tol);
            Assert.AreEqual(LocomotionState.Run, s.Locomotion);
        }

        [Test]
        public void RunForward_FromRest_AccelerationIsProgressive()
        {
            var first = Run(In(0, 1), GroundInfo.Flat, 1);
            Assert.Greater(first.PlanarSpeed, 0f);
            Assert.Less(first.PlanarSpeed, T.RunSpeed * 0.25f, "first tick must not jump to full speed");

            int half = Mathf.FloorToInt(T.AccelerationTime * 0.5f / Dt);
            var mid = Run(In(0, 1), GroundInfo.Flat, half);
            Assert.Less(mid.PlanarSpeed, T.RunSpeed * 0.6f, "at ~half the acceleration time we must be well below run speed");
            Assert.Greater(mid.PlanarSpeed, 0f);
        }

        [Test]
        public void ReleaseInput_FromRunSpeed_StopsWithinDecelerationTimeAndBecomesIdle()
        {
            var running = Run(In(0, 1), GroundInfo.Flat, Settle);
            Assert.AreEqual(T.RunSpeed, running.PlanarSpeed, Tol);

            var oneTick = Run(running, In(0, 0), GroundInfo.Flat, 1);
            Assert.Greater(oneTick.PlanarSpeed, 0.5f, "braking must not be instant");

            var stopped = Run(running, In(0, 0), GroundInfo.Flat, TicksFor(T.DecelerationTime) + 2);
            Assert.AreEqual(0f, stopped.PlanarSpeed, Tol);
            Assert.AreEqual(LocomotionState.Idle, stopped.Locomotion);
        }

        [Test]
        public void DiagonalInput_IsNotFasterThanStraightForward()
        {
            var forward = Run(In(0, 1), GroundInfo.Flat, Settle);
            var diagonal = Run(In(1, 1), GroundInfo.Flat, Settle);
            Assert.LessOrEqual(diagonal.PlanarSpeed, forward.PlanarSpeed + Tol);
            Assert.Greater(diagonal.PlanarSpeed, 0f);
        }

        [Test]
        public void PureStrafe_SettlesAtRunSpeedTimesStrafeMultiplier()
        {
            var right = Run(In(1, 0), GroundInfo.Flat, Settle);
            var left = Run(In(-1, 0), GroundInfo.Flat, Settle);
            Assert.AreEqual(T.RunSpeed * T.StrafeMultiplier, right.PlanarSpeed, Tol);
            Assert.AreEqual(T.RunSpeed * T.StrafeMultiplier, left.PlanarSpeed, Tol);
            Assert.AreEqual(T.RunSpeed * T.StrafeMultiplier, MovementMotor.DirectionalFactor(Vector2.right, T) * T.RunSpeed, 1e-4f);
        }

        [Test]
        public void PureBackward_SettlesAtRunSpeedTimesBackwardMultiplier()
        {
            var s = Run(In(0, -1), GroundInfo.Flat, Settle);
            Assert.AreEqual(T.RunSpeed * T.BackwardMultiplier, s.PlanarSpeed, Tol);
            Assert.Less(s.PlanarVelocity.z, 0f, "backward moves along -Z at yaw 0");
        }

        [Test]
        public void WalkFlag_SettlesAtWalkSpeedAndLocomotionIsWalk()
        {
            var s = Run(In(0, 1, walk: true), GroundInfo.Flat, Settle);
            Assert.AreEqual(T.WalkSpeed, s.PlanarSpeed, Tol);
            Assert.AreEqual(LocomotionState.Walk, s.Locomotion);
        }

        [Test]
        public void SprintForward_SettlesAtSprintSpeedAndLocomotionIsSprint()
        {
            var s = Run(In(0, 1, sprint: true), GroundInfo.Flat, Settle);
            Assert.AreEqual(T.SprintSpeed, s.PlanarSpeed, Tol);
            Assert.AreEqual(LocomotionState.Sprint, s.Locomotion);
        }

        [Test]
        public void SprintForward_FromRest_TakesLongerThanRunAcceleration()
        {
            int sprintTicks = TicksUntilSpeed(In(0, 1, sprint: true), T.SprintSpeed - Tol, 600);
            int runTicks = TicksUntilSpeed(In(0, 1), T.RunSpeed - Tol, 600);
            Assert.GreaterOrEqual(sprintTicks, 0, "sprint speed was never reached");
            Assert.GreaterOrEqual(runTicks, 0, "run speed was never reached");
            Assert.Greater(sprintTicks * Dt, T.AccelerationTime, "sprint must take longer than AccelerationTime");
            Assert.Greater(sprintTicks, runTicks);
        }

        private static int TicksUntilSpeed(MovementInput input, float speed, int maxTicks)
        {
            var tuning = T;
            var s = default(MovementState);
            for (int i = 1; i <= maxTicks; i++)
            {
                s = MovementMotor.Step(s, input, GroundInfo.Flat, tuning, Dt);
                if (s.PlanarSpeed >= speed) return i;
            }
            return -1;
        }

        [Test]
        public void Sprint_IsIgnoredWhenMovingBackwards()
        {
            var s = Run(In(0, -1, sprint: true), GroundInfo.Flat, Settle);
            Assert.AreNotEqual(LocomotionState.Sprint, s.Locomotion);
            Assert.LessOrEqual(s.PlanarSpeed, T.RunSpeed * T.BackwardMultiplier + Tol);
        }

        [Test]
        public void Sprint_IsIgnoredWhenMovingPureSideways()
        {
            var s = Run(In(1, 0, sprint: true), GroundInfo.Flat, Settle);
            Assert.AreNotEqual(LocomotionState.Sprint, s.Locomotion);
            Assert.LessOrEqual(s.PlanarSpeed, T.RunSpeed * T.StrafeMultiplier + Tol);
        }

        [Test]
        public void Sprint_IsIgnoredWhenAirborne()
        {
            var running = Run(In(0, 1), GroundInfo.Flat, Settle);
            var s = Run(running, In(0, 1, sprint: true), GroundInfo.Air, Settle);
            Assert.AreNotEqual(LocomotionState.Sprint, s.Locomotion);
            Assert.LessOrEqual(s.PlanarSpeed, T.RunSpeed + Tol);
        }

        [Test]
        public void Yaw90_ForwardInput_MovesAlongPositiveWorldX()
        {
            var s = Run(In(0, 1, yaw: 90f), GroundInfo.Flat, Settle);
            Assert.AreEqual(T.RunSpeed, s.PlanarVelocity.x, Tol);
            Assert.AreEqual(0f, s.PlanarVelocity.z, Tol);
        }

        [Test]
        public void AnalogHalfStick_Forward_GivesProportionalSpeedAndWalk()
        {
            var s = Run(In(0, 0.4f), GroundInfo.Flat, Settle);
            Assert.AreEqual(T.RunSpeed * 0.4f, s.PlanarSpeed, Tol);
            Assert.AreEqual(LocomotionState.Walk, s.Locomotion);

            var half = Run(In(0, 0.5f), GroundInfo.Flat, Settle);
            Assert.AreEqual(T.RunSpeed * 0.5f, half.PlanarSpeed, Tol);
        }

        [Test]
        public void Airborne_VerticalSpeedDecreasesByGravityPerTick()
        {
            var tuning = T;
            var s = default(MovementState);
            for (int i = 0; i < 10; i++)
            {
                float before = s.VerticalSpeed;
                s = MovementMotor.Step(s, In(0, 0), GroundInfo.Air, tuning, Dt);
                Assert.AreEqual(before - tuning.Gravity * Dt, s.VerticalSpeed, 1e-4f);
                Assert.IsFalse(s.Grounded);
            }
        }

        [Test]
        public void Airborne_LongFall_ClampsAtTerminalFallSpeed()
        {
            var s = Run(In(0, 0), GroundInfo.Air, 600);
            Assert.AreEqual(-T.TerminalFallSpeed, s.VerticalSpeed, 1e-4f);
            Assert.AreEqual(-T.TerminalFallSpeed, s.Velocity.y, 1e-4f);
        }

        [Test]
        public void Grounded_VerticalSpeedIsMinusGroundStickSpeed()
        {
            var s = Run(In(0, 1), GroundInfo.Flat, 30);
            Assert.AreEqual(-T.GroundStickSpeed, s.VerticalSpeed, 1e-5f);
            Assert.IsTrue(s.Grounded);
        }

        // Normal tilted towards +Z, so downhill is +Z (yaw 0) and uphill is -Z (yaw 180).
        private static GroundInfo Slope30 => new GroundInfo(true, Quaternion.Euler(30f, 0f, 0f) * Vector3.up);

        [Test]
        public void Slope_Uphill_IsSlowerThanFlat()
        {
            var ground = Slope30;
            Assert.Greater(ground.Normal.z, 0f, "test setup: normal must tilt towards +Z (downhill = +Z)");

            var flat = Run(In(0, 1, yaw: 180f), GroundInfo.Flat, Settle);
            var uphill = Run(In(0, 1, yaw: 180f), ground, Settle);

            Assert.Less(uphill.PlanarSpeed, flat.PlanarSpeed - 0.1f);

            float steepness = 30f / T.MaxWalkableSlope;
            float expected = T.RunSpeed * Mathf.Lerp(1f, T.UphillMinMultiplier, steepness);
            Assert.AreEqual(expected, uphill.PlanarSpeed, Tol);
            Assert.Less(uphill.PlanarVelocity.z, 0f, "moving along -Z");
            Assert.Greater(uphill.Velocity.y - (-T.GroundStickSpeed), 0f, "velocity climbs the slope");
        }

        [Test]
        public void Slope_Downhill_IsNotSlowerThanFlat()
        {
            var ground = Slope30;
            var flat = Run(In(0, 1, yaw: 0f), GroundInfo.Flat, Settle);
            var downhill = Run(In(0, 1, yaw: 0f), ground, Settle);

            Assert.GreaterOrEqual(downhill.PlanarSpeed, flat.PlanarSpeed - Tol);
            Assert.Less(downhill.Velocity.y - (-T.GroundStickSpeed), 0f, "velocity descends the slope");
        }

        [Test]
        public void Slope_VelocityIsParallelToSlopePlane()
        {
            var ground = Slope30;
            foreach (float yaw in new[] { 0f, 90f, 180f, 270f })
            {
                var s = Run(In(0, 1, yaw: yaw), ground, Settle);
                Vector3 alongSlope = s.Velocity - Vector3.up * s.VerticalSpeed;
                Assert.AreEqual(0f, Vector3.Dot(alongSlope, ground.Normal), 0.01f, $"yaw {yaw}: along-slope part must be perpendicular to normal");
                Assert.AreEqual(s.PlanarSpeed, alongSlope.magnitude, 0.01f, $"yaw {yaw}: slope alignment must preserve speed");
            }
        }

        [Test]
        public void Determinism_IdenticalScriptedRuns_ProduceBitIdenticalStates()
        {
            var a = RunScripted(300, out var historyA);
            var b = RunScripted(300, out var historyB);

            for (int i = 0; i < historyA.Length; i++)
                AssertBitIdentical(historyA[i], historyB[i], i);
            AssertBitIdentical(a, b, -1);
        }

        private static MovementState RunScripted(int ticks, out MovementState[] history)
        {
            var tuning = T;
            var s = default(MovementState);
            history = new MovementState[ticks];
            for (int i = 0; i < ticks; i++)
            {
                float x = Mathf.Sin(i * 0.13f);
                float y = Mathf.Cos(i * 0.07f) * 1.2f;
                float yaw = i * 1.7f;
                bool sprint = (i / 40) % 2 == 0;
                bool walk = (i / 55) % 3 == 2;
                var ground = (i / 90) % 3 == 1 ? GroundInfo.Air : (i % 2 == 0 ? GroundInfo.Flat : Slope30);
                s = MovementMotor.Step(s, In(x, y, yaw, sprint, walk), ground, tuning, Dt);
                history[i] = s;
            }
            return s;
        }

        private static void AssertBitIdentical(MovementState a, MovementState b, int tick)
        {
            string msg = $"tick {tick}";
            Assert.AreEqual(a.PlanarVelocity, b.PlanarVelocity, msg);
            Assert.AreEqual(a.Velocity, b.Velocity, msg);
            Assert.AreEqual(a.VerticalSpeed, b.VerticalSpeed, msg);
            Assert.AreEqual(a.Locomotion, b.Locomotion, msg);
            Assert.AreEqual(a.Grounded, b.Grounded, msg);
            Assert.AreEqual(a.PlanarVelocity.x, b.PlanarVelocity.x, 0f, msg);
            Assert.AreEqual(a.PlanarVelocity.z, b.PlanarVelocity.z, 0f, msg);
            Assert.AreEqual(a.Velocity.x, b.Velocity.x, 0f, msg);
            Assert.AreEqual(a.Velocity.y, b.Velocity.y, 0f, msg);
            Assert.AreEqual(a.Velocity.z, b.Velocity.z, 0f, msg);
        }

        [Test]
        public void ReversingDirection_PassesThroughNearZeroSpeedInsteadOfFlippingInstantly()
        {
            var tuning = T;
            var s = Run(In(0, 1), GroundInfo.Flat, Settle);
            float before = s.PlanarSpeed;
            Assert.AreEqual(T.RunSpeed, before, Tol);

            float minSpeed = float.MaxValue;
            for (int i = 1; i <= 30; i++)
            {
                s = MovementMotor.Step(s, In(0, -1), GroundInfo.Flat, tuning, Dt);
                if (i == 3)
                {
                    Assert.Less(s.PlanarSpeed, before, "speed must drop right after reversing");
                    Assert.Greater(s.PlanarVelocity.z, 0f, "still moving forward a few ticks after reversing");
                }
                minSpeed = Mathf.Min(minSpeed, s.PlanarSpeed);
            }

            Assert.Less(minSpeed, 0.5f, "speed must pass (near) zero while reversing");
            Assert.Less(s.PlanarVelocity.z, 0f, "eventually moving backward");
            Assert.AreEqual(T.RunSpeed * T.BackwardMultiplier, s.PlanarSpeed, Tol);
        }
    }
}
