using NUnit.Framework;
using UnityEngine;

namespace Polykov.Movement.Tests
{
    public class MovementMotorJumpLeanTests
    {
        private const float Dt = 1f / 60f;
        private const float Tol = 0.05f;
        private const int Settle = 240;

        private static MovementTuning T => MovementTuning.Default;
        private static float JumpSpeed => Mathf.Sqrt(2f * T.Gravity * T.JumpHeight);

        private static MovementInput In(float x, float y, float yaw = 0f, bool sprint = false, bool walk = false,
            bool jump = false, float lean = 0f)
            => new MovementInput(new Vector2(x, y), yaw, sprint, walk, jump, lean);

        private static MovementState Step(MovementState s, MovementInput input, GroundInfo ground)
            => MovementMotor.Step(s, input, ground, T, Dt);

        private static MovementState Run(MovementState start, MovementInput input, GroundInfo ground, int ticks)
        {
            var s = start;
            for (int i = 0; i < ticks; i++) s = Step(s, input, ground);
            return s;
        }

        private static MovementState Run(MovementInput input, GroundInfo ground, int ticks)
            => Run(default, input, ground, ticks);

        private static int TicksFor(float seconds) => Mathf.CeilToInt(seconds / Dt);

        /// <summary>A character standing still on flat ground (coyote time refreshed).</summary>
        private static MovementState Standing() => Run(In(0, 0), GroundInfo.Flat, 5);

        /// <summary>A character in free fall, well past any coyote window.</summary>
        private static MovementState Falling(int airTicks = 30) => Run(In(0, 0), GroundInfo.Air, airTicks);

        /// <summary>
        /// Steps one tick of a simulated flight. The ground probe reports the floor while rising (as it does right
        /// after takeoff), then air, then the floor again once the accumulated height is back at or below zero.
        /// </summary>
        private static MovementState FlightStep(MovementState s, MovementInput input, ref float height)
        {
            var ground = s.VerticalSpeed > 0f ? GroundInfo.Flat : (height > 0f ? GroundInfo.Air : GroundInfo.Flat);
            var next = Step(s, input, ground);
            height += next.Velocity.y * Dt;
            return next;
        }

        // ---------------------------------------------------------------- Jump

        [Test]
        public void Jump_FromGrounded_SetsUpwardSpeedAndLeavesGroundForOneJustJumpedTick()
        {
            var standing = Standing();
            Assert.IsTrue(standing.Grounded);

            var takeoff = Step(standing, In(0, 0, jump: true), GroundInfo.Flat);
            Assert.AreEqual(JumpSpeed, takeoff.VerticalSpeed, 1e-3f);
            Assert.AreEqual(JumpSpeed, takeoff.Velocity.y, 1e-3f);
            Assert.IsFalse(takeoff.Grounded);
            Assert.IsTrue(takeoff.JustJumped);

            var next = Step(takeoff, In(0, 0), GroundInfo.Flat);
            Assert.IsFalse(next.JustJumped, "JustJumped is a one-tick flag");
            Assert.IsFalse(next.Grounded);
        }

        [Test]
        public void Jump_NotPressed_DoesNothing()
        {
            var s = Run(Standing(), In(0, 0), GroundInfo.Flat, 30);
            Assert.IsTrue(s.Grounded);
            Assert.IsFalse(s.JustJumped);
            Assert.AreEqual(-T.GroundStickSpeed, s.VerticalSpeed, 1e-5f);
        }

        [Test]
        public void Jump_SimulatedFlight_ReachesJumpHeightAndLandsOnce()
        {
            var s = Step(Standing(), In(0, 0, jump: true), GroundInfo.Flat);
            float height = s.Velocity.y * Dt;
            float apex = height;
            int justLanded = 0;
            int landingTick = -1;

            for (int i = 1; i <= 200 && (landingTick < 0 || i <= landingTick + 5); i++)
            {
                s = FlightStep(s, In(0, 0), ref height);
                apex = Mathf.Max(apex, height);
                Assert.IsFalse(s.JustJumped, $"tick {i}");
                if (s.JustLanded)
                {
                    justLanded++;
                    if (landingTick < 0) landingTick = i;
                }
            }

            Assert.Greater(landingTick, 0, "character never landed");
            Assert.AreEqual(T.JumpHeight, apex, Tol, "apex height");
            Assert.AreEqual(1, justLanded, "JustLanded must be true on exactly one tick");
            Assert.IsTrue(s.Grounded);
            Assert.AreEqual(-T.GroundStickSpeed, s.VerticalSpeed, 1e-5f);

            // Flight time of a ballistic arc is 2*v/g; allow a couple of ticks of discretisation error.
            float expectedTicks = 2f * JumpSpeed / T.Gravity / Dt;
            Assert.AreEqual(expectedTicks, landingTick, 3f);
        }

        [Test]
        public void Jump_WhileRising_FlatGroundProbeDoesNotReGroundTheCharacter()
        {
            var s = Step(Standing(), In(0, 0, jump: true), GroundInfo.Flat);
            for (int i = 0; i < 12; i++)
            {
                Assert.Greater(s.VerticalSpeed, 0f, $"precondition tick {i}");
                s = Step(s, In(0, 0), GroundInfo.Flat);
                Assert.IsFalse(s.Grounded, $"tick {i}: must stay airborne while rising");
                Assert.IsFalse(s.JustLanded, $"tick {i}");
            }
        }

        [Test]
        public void Jump_PressedAgainMidAir_DoesNotDoubleJump()
        {
            var s = Step(Standing(), In(0, 0, jump: true), GroundInfo.Flat);
            s = Run(s, In(0, 0), GroundInfo.Air, 3);

            // Spam jump every tick, rising and then falling.
            for (int i = 0; i < 30; i++)
            {
                float before = s.VerticalSpeed;
                s = Step(s, In(0, 0, jump: true), GroundInfo.Air);
                Assert.IsFalse(s.JustJumped, $"tick {i}");
                Assert.IsFalse(s.Grounded, $"tick {i}");
                Assert.AreEqual(before - T.Gravity * Dt, s.VerticalSpeed, 1e-4f, $"tick {i}: only gravity may act");
            }
        }

        [Test]
        public void JumpBuffer_PressedThreeTicksBeforeLanding_JumpsOnLandingTick()
        {
            var s = Falling();
            Assert.Less(s.VerticalSpeed, 0f);

            s = Step(s, In(0, 0, jump: true), GroundInfo.Air);   // press
            Assert.IsFalse(s.JustJumped);
            s = Step(s, In(0, 0), GroundInfo.Air);
            s = Step(s, In(0, 0), GroundInfo.Air);
            Assert.IsFalse(s.JustJumped);
            Assert.IsFalse(s.Grounded);

            s = Step(s, In(0, 0), GroundInfo.Flat);              // landing tick, 3 ticks after the press
            Assert.IsTrue(s.JustJumped, "buffered jump must fire on landing");
            Assert.AreEqual(JumpSpeed, s.VerticalSpeed, 1e-3f);
            Assert.IsFalse(s.Grounded);
        }

        [Test]
        public void JumpBuffer_PressedLongerThanBufferTimeAgo_DoesNotJump()
        {
            int ticksAgo = TicksFor(T.JumpBufferTime) + 3;
            var s = Falling();
            s = Step(s, In(0, 0, jump: true), GroundInfo.Air);
            s = Run(s, In(0, 0), GroundInfo.Air, ticksAgo);

            s = Step(s, In(0, 0), GroundInfo.Flat);
            Assert.IsTrue(s.JustLanded);
            Assert.IsFalse(s.JustJumped, "buffer expired");
            Assert.IsTrue(s.Grounded);

            s = Step(s, In(0, 0), GroundInfo.Flat);
            Assert.IsFalse(s.JustJumped);
            Assert.IsTrue(s.Grounded);
        }

        [Test]
        public void CoyoteTime_JumpWithinWindowAfterLeavingLedge_Jumps()
        {
            var s = Standing();
            s = Run(s, In(0, 0), GroundInfo.Air, 2);
            Assert.IsFalse(s.Grounded);

            s = Step(s, In(0, 0, jump: true), GroundInfo.Air);   // ~0.05s after the ledge, CoyoteTime = 0.1s
            Assert.IsTrue(s.JustJumped);
            Assert.AreEqual(JumpSpeed, s.VerticalSpeed, 1e-3f);
        }

        [Test]
        public void CoyoteTime_JumpAfterWindowExpired_DoesNotJump()
        {
            var s = Standing();
            s = Run(s, In(0, 0), GroundInfo.Air, TicksFor(T.CoyoteTime) + 3);

            var pressed = Step(s, In(0, 0, jump: true), GroundInfo.Air);
            Assert.IsFalse(pressed.JustJumped);
            Assert.Less(pressed.VerticalSpeed, 0f);
        }

        [Test]
        public void CoyoteTime_DoesNotAllowSecondJumpAfterJumping()
        {
            var s = Step(Standing(), In(0, 0, jump: true), GroundInfo.Flat);
            Assert.IsTrue(s.JustJumped);
            // Right after takeoff, press jump again: coyote must have been consumed.
            s = Step(s, In(0, 0, jump: true), GroundInfo.Air);
            Assert.IsFalse(s.JustJumped);
            Assert.AreEqual(JumpSpeed - T.Gravity * Dt, s.VerticalSpeed, 1e-3f);
        }

        [Test]
        public void SprintJump_KeepsPlanarMomentumAtTakeoff()
        {
            var running = Run(In(0, 1, sprint: true), GroundInfo.Flat, Settle);
            Assert.AreEqual(T.SprintSpeed, running.PlanarSpeed, Tol);

            var takeoff = Step(running, In(0, 1, sprint: true, jump: true), GroundInfo.Flat);
            Assert.IsTrue(takeoff.JustJumped);
            Assert.AreEqual(T.SprintSpeed, takeoff.PlanarSpeed, Tol);
            Assert.AreEqual(T.SprintSpeed, new Vector2(takeoff.Velocity.x, takeoff.Velocity.z).magnitude, Tol);
        }

        [Test]
        public void AirControl_IsMuchWeakerThanGroundControl()
        {
            var running = Run(In(0, 1, sprint: true), GroundInfo.Flat, Settle);
            var backward = In(0, -1, sprint: true);

            var onGround = Run(running, backward, GroundInfo.Flat, 10);
            float groundChange = Mathf.Abs(running.PlanarSpeed - onGround.PlanarSpeed);

            var airborne = Step(running, In(0, 1, sprint: true, jump: true), GroundInfo.Flat);
            float airStart = airborne.PlanarSpeed;
            airborne = Run(airborne, backward, GroundInfo.Air, 10);
            Assert.IsFalse(airborne.Grounded);
            float airChange = Mathf.Abs(airStart - airborne.PlanarSpeed);

            Assert.Greater(groundChange, 3f, "sanity: on the ground 10 ticks of reverse input change speed a lot");
            Assert.Less(airChange, 1f, "in the air the same input barely changes speed");
            Assert.Less(airChange, groundChange * 0.25f);
            Assert.Greater(airborne.PlanarVelocity.z, 0f, "still moving forward after only 10 ticks of reverse");
        }

        [Test]
        public void HardLanding_MultipliesPlanarSpeedByLandingMomentum()
        {
            var run = In(0, 1);
            var s = Run(run, GroundInfo.Flat, Settle);
            s = Run(s, run, GroundInfo.Air, 10);      // fall speed ~5.3 m/s > HardLandingSpeed
            Assert.Greater(-s.VerticalSpeed, T.HardLandingSpeed);
            float fall = -s.VerticalSpeed;
            float before = s.PlanarSpeed;
            Assert.AreEqual(T.RunSpeed, before, Tol);

            var landed = Step(s, run, GroundInfo.Flat);
            Assert.IsTrue(landed.JustLanded);
            Assert.IsTrue(landed.Grounded);
            Assert.AreEqual(fall, landed.LandingImpact, 1e-4f);

            // Momentum penalty first, then normal ground acceleration back towards the (unchanged) run target.
            float expected = before * T.LandingMomentum + T.RunSpeed / T.AccelerationTime * Dt;
            Assert.AreEqual(expected, landed.PlanarSpeed, 0.02f);
            Assert.Less(landed.PlanarSpeed, before - 0.2f, "hard landing must clearly cost speed");
        }

        [Test]
        public void SoftLanding_DoesNotCostMomentum()
        {
            var run = In(0, 1);
            var s = Run(run, GroundInfo.Flat, Settle);
            s = Run(s, run, GroundInfo.Air, 3);       // fall speed ~3 m/s < HardLandingSpeed
            Assert.Less(-s.VerticalSpeed, T.HardLandingSpeed);
            float fall = -s.VerticalSpeed;
            float before = s.PlanarSpeed;

            var landed = Step(s, run, GroundInfo.Flat);
            Assert.IsTrue(landed.JustLanded);
            Assert.AreEqual(fall, landed.LandingImpact, 1e-4f);
            Assert.GreaterOrEqual(landed.PlanarSpeed, before - 0.01f, "no landing penalty below HardLandingSpeed");
            Assert.AreEqual(T.RunSpeed, landed.PlanarSpeed, Tol);
        }

        [Test]
        public void JustLanded_IsOneTickOnly()
        {
            var s = Falling(10);
            var landed = Step(s, In(0, 0), GroundInfo.Flat);
            Assert.IsTrue(landed.JustLanded);
            var next = Step(landed, In(0, 0), GroundInfo.Flat);
            Assert.IsFalse(next.JustLanded);
            Assert.IsTrue(next.Grounded);
        }

        // ---------------------------------------------------------------- Lean

        private static int LeanTicks => TicksFor(T.LeanTime) + 1;

        [Test]
        public void Lean_PositiveInput_ReachesFullLeanAfterLeanTimeButNotInstantly()
        {
            var input = In(0, 0, lean: 1f);
            var first = Run(input, GroundInfo.Flat, 1);
            Assert.Greater(first.Lean, 0f);
            Assert.Less(first.Lean, 0.2f, "lean must not snap");

            var half = Run(input, GroundInfo.Flat, TicksFor(T.LeanTime * 0.5f));
            Assert.Less(half.Lean, 0.75f);
            Assert.Greater(half.Lean, 0.25f);

            var full = Run(input, GroundInfo.Flat, LeanTicks);
            Assert.AreEqual(1f, full.Lean, 1e-4f);
        }

        [Test]
        public void Lean_NegativeInput_GoesToMinusOne()
        {
            var input = In(0, 0, lean: -1f);
            var first = Run(input, GroundInfo.Flat, 1);
            Assert.Less(first.Lean, 0f);
            Assert.Greater(first.Lean, -0.2f);

            var full = Run(input, GroundInfo.Flat, LeanTicks);
            Assert.AreEqual(-1f, full.Lean, 1e-4f);
        }

        [Test]
        public void Lean_ReleasingInput_ReturnsToZeroProgressively()
        {
            var leaning = Run(In(0, 0, lean: 1f), GroundInfo.Flat, LeanTicks);
            Assert.AreEqual(1f, leaning.Lean, 1e-4f);

            var one = Run(leaning, In(0, 0), GroundInfo.Flat, 1);
            Assert.Less(one.Lean, 1f);
            Assert.Greater(one.Lean, 0.8f, "return must not snap");

            var released = Run(leaning, In(0, 0), GroundInfo.Flat, LeanTicks);
            Assert.AreEqual(0f, released.Lean, 1e-4f);
        }

        [Test]
        public void Lean_OutOfRangeInput_IsClampedToOne()
        {
            var s = Run(In(0, 0, lean: 5f), GroundInfo.Flat, Settle);
            Assert.AreEqual(1f, s.Lean, 1e-4f);
        }

        [Test]
        public void Lean_Sprinting_ForcesLeanTowardsZeroEvenWithLeanHeld()
        {
            var leaning = Run(In(0, 0, lean: 1f), GroundInfo.Flat, LeanTicks);
            Assert.AreEqual(1f, leaning.Lean, 1e-4f);

            var sprintLean = In(0, 1, sprint: true, lean: 1f);
            var s = leaning;
            float previous = s.Lean;
            for (int i = 0; i < LeanTicks; i++)
            {
                s = Step(s, sprintLean, GroundInfo.Flat);
                Assert.LessOrEqual(s.Lean, previous + 1e-6f, $"tick {i}: lean must not grow while sprinting");
                previous = s.Lean;
            }
            Assert.AreEqual(0f, s.Lean, 1e-4f);

            // Stays at zero for as long as sprint is held.
            s = Run(s, sprintLean, GroundInfo.Flat, 120);
            Assert.AreEqual(LocomotionState.Sprint, s.Locomotion);
            Assert.AreEqual(0f, s.Lean, 1e-4f);
        }

        [Test]
        public void Lean_SprintFromStart_NeverLeans()
        {
            var sprintLean = In(0, 1, sprint: true, lean: -1f);
            var s = default(MovementState);
            for (int i = 0; i < 120; i++)
            {
                s = Step(s, sprintLean, GroundInfo.Flat);
                Assert.AreEqual(0f, s.Lean, 1e-6f, $"tick {i}");
            }
            Assert.AreEqual(T.SprintSpeed, s.PlanarSpeed, Tol);
        }

        [Test]
        public void Lean_FullLean_SlowsRunningSpeedByLeanMoveMultiplier()
        {
            var right = Run(In(0, 1, lean: 1f), GroundInfo.Flat, Settle);
            var left = Run(In(0, 1, lean: -1f), GroundInfo.Flat, Settle);
            var upright = Run(In(0, 1), GroundInfo.Flat, Settle);

            Assert.AreEqual(T.RunSpeed, upright.PlanarSpeed, Tol);
            Assert.AreEqual(T.RunSpeed * T.LeanMoveMultiplier, right.PlanarSpeed, Tol);
            Assert.AreEqual(T.RunSpeed * T.LeanMoveMultiplier, left.PlanarSpeed, Tol);
            Assert.AreEqual(LocomotionState.Run, right.Locomotion);
        }

        [Test]
        public void Lean_ReleasingLeanWhileRunning_RestoresFullSpeed()
        {
            var s = Run(In(0, 1, lean: 1f), GroundInfo.Flat, Settle);
            Assert.AreEqual(1f, s.Lean, 1e-4f);
            var released = Run(s, In(0, 1), GroundInfo.Flat, Settle);
            Assert.AreEqual(0f, released.Lean, 1e-4f);
            Assert.AreEqual(T.RunSpeed, released.PlanarSpeed, Tol);
        }

        // ---------------------------------------------------------------- Determinism

        [Test]
        public void Determinism_JumpAndLeanScriptedRuns_ProduceBitIdenticalStates()
        {
            var a = RunScripted(300, out var historyA);
            var b = RunScripted(300, out var historyB);

            int jumps = 0, landings = 0;
            bool leaned = false;
            for (int i = 0; i < historyA.Length; i++)
            {
                AssertBitIdentical(historyA[i], historyB[i], i);
                if (historyA[i].JustJumped) jumps++;
                if (historyA[i].JustLanded) landings++;
                leaned |= Mathf.Abs(historyA[i].Lean) > 0.5f;
            }
            AssertBitIdentical(a, b, -1);

            Assert.Greater(jumps, 0, "script must actually exercise jumping");
            Assert.Greater(landings, 0, "script must actually exercise landing");
            Assert.IsTrue(leaned, "script must actually exercise leaning");
        }

        private static MovementState RunScripted(int ticks, out MovementState[] history)
        {
            var s = default(MovementState);
            history = new MovementState[ticks];
            for (int i = 0; i < ticks; i++)
            {
                float x = Mathf.Sin(i * 0.13f);
                float y = Mathf.Cos(i * 0.07f) * 1.2f;
                float yaw = i * 1.7f;
                bool sprint = (i / 40) % 2 == 0;
                bool walk = (i / 55) % 3 == 2;
                bool jump = i % 37 == 0 || i % 53 == 5;
                float lean = Mathf.Sin(i * 0.05f) * 1.5f;
                var ground = (i / 70) % 3 == 1 ? GroundInfo.Air : GroundInfo.Flat;
                s = MovementMotor.Step(s, In(x, y, yaw, sprint, walk, jump, lean), ground, T, Dt);
                history[i] = s;
            }
            return s;
        }

        private static void AssertBitIdentical(MovementState a, MovementState b, int tick)
        {
            string msg = $"tick {tick}";
            Assert.AreEqual(a.PlanarVelocity.x, b.PlanarVelocity.x, 0f, msg);
            Assert.AreEqual(a.PlanarVelocity.z, b.PlanarVelocity.z, 0f, msg);
            Assert.AreEqual(a.Velocity.x, b.Velocity.x, 0f, msg);
            Assert.AreEqual(a.Velocity.y, b.Velocity.y, 0f, msg);
            Assert.AreEqual(a.Velocity.z, b.Velocity.z, 0f, msg);
            Assert.AreEqual(a.VerticalSpeed, b.VerticalSpeed, 0f, msg);
            Assert.AreEqual(a.Locomotion, b.Locomotion, msg);
            Assert.AreEqual(a.Grounded, b.Grounded, msg);
            Assert.AreEqual(a.Lean, b.Lean, 0f, msg);
            Assert.AreEqual(a.JumpBuffer, b.JumpBuffer, 0f, msg);
            Assert.AreEqual(a.CoyoteTime, b.CoyoteTime, 0f, msg);
            Assert.AreEqual(a.JustJumped, b.JustJumped, msg);
            Assert.AreEqual(a.JustLanded, b.JustLanded, msg);
            Assert.AreEqual(a.LandingImpact, b.LandingImpact, 0f, msg);
        }
    }
}
