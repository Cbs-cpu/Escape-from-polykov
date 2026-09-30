using NUnit.Framework;
using UnityEngine;

namespace Polykov.Movement.Tests
{
    public class MovementMotorCrouchTests
    {
        private const float Dt = 1f / 60f;
        private const float Tol = 0.05f;
        private const int Settle = 240;

        private static MovementTuning T => MovementTuning.Default;

        private static MovementInput In(float x, float y, bool sprint = false, bool walk = false, bool jump = false,
            bool crouch = false)
            => new MovementInput(new Vector2(x, y), 0f, sprint, walk, jump, 0f, crouch);

        private static MovementState Run(MovementState s, MovementInput input, GroundInfo ground, int ticks)
        {
            var tuning = T;
            for (int i = 0; i < ticks; i++) s = MovementMotor.Step(s, input, ground, tuning, Dt);
            return s;
        }

        private static int TicksFor(float seconds) => Mathf.CeilToInt(seconds / Dt);

        [Test]
        public void NoCrouchInput_StaysStanding()
        {
            var s = Run(default, In(0, 1), GroundInfo.Flat, Settle);
            Assert.AreEqual(0f, s.Crouch);
            Assert.IsFalse(s.Crouching);
        }

        [Test]
        public void Crouch_ReachesFullCrouchWithinCrouchTime_Progressively()
        {
            var one = Run(default, In(0, 0, crouch: true), GroundInfo.Flat, 1);
            Assert.Greater(one.Crouch, 0f);
            Assert.Less(one.Crouch, 0.5f, "crouching must not be instant");

            var full = Run(default, In(0, 0, crouch: true), GroundInfo.Flat, TicksFor(T.CrouchTime) + 1);
            Assert.AreEqual(1f, full.Crouch, 1e-4f);
            Assert.IsTrue(full.Crouching);
        }

        [Test]
        public void CrouchedRun_IsCappedAtCrouchSpeed()
        {
            var s = Run(default, In(0, 1, crouch: true), GroundInfo.Flat, Settle);
            Assert.AreEqual(T.CrouchSpeed, s.PlanarSpeed, Tol);
            Assert.AreEqual(LocomotionState.Walk, s.Locomotion);
        }

        [Test]
        public void CrouchedWalk_IsNotFasterThanWalking()
        {
            var s = Run(default, In(0, 1, walk: true, crouch: true), GroundInfo.Flat, Settle);
            Assert.LessOrEqual(s.PlanarSpeed, Mathf.Min(T.WalkSpeed, T.CrouchSpeed) + Tol);
        }

        [Test]
        public void Crouch_CancelsSprint()
        {
            var sprinting = Run(default, In(0, 1, sprint: true), GroundInfo.Flat, Settle);
            Assert.AreEqual(LocomotionState.Sprint, sprinting.Locomotion);

            var s = Run(sprinting, In(0, 1, sprint: true, crouch: true), GroundInfo.Flat, Settle);
            Assert.AreNotEqual(LocomotionState.Sprint, s.Locomotion);
            Assert.AreEqual(T.CrouchSpeed, s.PlanarSpeed, Tol);
        }

        [Test]
        public void ReleasingCrouch_StandsUpOverCrouchTime()
        {
            var crouched = Run(default, In(0, 0, crouch: true), GroundInfo.Flat, Settle);
            var s = Run(crouched, In(0, 0), GroundInfo.Flat, TicksFor(T.CrouchTime) + 1);
            Assert.AreEqual(0f, s.Crouch, 1e-4f);
            Assert.IsFalse(s.Crouching);
        }

        [Test]
        public void LowCeiling_KeepsYouCrouched_UntilClear()
        {
            var crouched = Run(default, In(0, 0, crouch: true), GroundInfo.Flat, Settle);
            GroundInfo blocked = GroundInfo.Flat.WithCeiling(true);

            var underCeiling = Run(crouched, In(0, 1), blocked, Settle);
            Assert.AreEqual(1f, underCeiling.Crouch, 1e-4f);
            Assert.IsTrue(underCeiling.Crouching);
            Assert.AreEqual(T.CrouchSpeed, underCeiling.PlanarSpeed, Tol);

            var clear = Run(underCeiling, In(0, 1), GroundInfo.Flat, Settle);
            Assert.AreEqual(0f, clear.Crouch, 1e-4f);
            Assert.AreEqual(T.RunSpeed, clear.PlanarSpeed, Tol);
        }

        [Test]
        public void LowCeiling_WhileStandingUp_StopsRisingAndGoesBackDown()
        {
            var crouched = Run(default, In(0, 0, crouch: true), GroundInfo.Flat, Settle);
            var half = Run(crouched, In(0, 0), GroundInfo.Flat, TicksFor(T.CrouchTime * 0.5f));
            Assert.Greater(half.Crouch, 0f);
            var s = Run(half, In(0, 0), GroundInfo.Flat.WithCeiling(true), 3);
            Assert.GreaterOrEqual(s.Crouch, half.Crouch);
        }

        [Test]
        public void LowCeiling_DoesNotForceCrouchWhenStanding()
        {
            var s = Run(default, In(0, 1), GroundInfo.Flat.WithCeiling(true), Settle);
            Assert.AreEqual(0f, s.Crouch);
            Assert.AreEqual(T.RunSpeed, s.PlanarSpeed, Tol);
        }

        [Test]
        public void CannotJump_WhileCrouched()
        {
            var crouched = Run(default, In(0, 0, crouch: true), GroundInfo.Flat, Settle);
            var s = MovementMotor.Step(crouched, In(0, 0, jump: true, crouch: true), GroundInfo.Flat, T, Dt);
            Assert.IsFalse(s.JustJumped);
            Assert.LessOrEqual(s.VerticalSpeed, 0f);
        }

        [Test]
        public void Crouch_IsDeterministic()
        {
            var a = Run(default, In(0.3f, 1, crouch: true), GroundInfo.Flat, 37);
            var b = Run(default, In(0.3f, 1, crouch: true), GroundInfo.Flat, 37);
            Assert.AreEqual(a.Crouch, b.Crouch);
            Assert.AreEqual(a.PlanarVelocity.x, b.PlanarVelocity.x);
            Assert.AreEqual(a.PlanarVelocity.z, b.PlanarVelocity.z);
        }
    }
}
