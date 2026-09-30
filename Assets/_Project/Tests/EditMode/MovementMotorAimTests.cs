using NUnit.Framework;
using UnityEngine;

namespace Polykov.Movement.Tests
{
    public class MovementMotorAimTests
    {
        private const float Dt = 1f / 60f;
        private static MovementTuning T => MovementTuning.Default;

        private static MovementState Run(MovementInput input, int ticks)
        {
            var s = default(MovementState);
            for (int i = 0; i < ticks; i++) s = MovementMotor.Step(s, input, GroundInfo.Flat, T, Dt);
            return s;
        }

        [Test]
        public void Aiming_SlowsMovement()
        {
            var s = Run(new MovementInput(new Vector2(0f, 1f), 0f, false, false, false, 0f, false, true), 240);
            Assert.AreEqual(T.RunSpeed * T.AimMoveMultiplier, s.PlanarSpeed, 0.05f);
        }

        [Test]
        public void Aiming_BlocksSprint()
        {
            var s = Run(new MovementInput(new Vector2(0f, 1f), 0f, true, false, false, 0f, false, true), 240);
            Assert.AreNotEqual(LocomotionState.Sprint, s.Locomotion);
        }

        [Test]
        public void TurningAround_KillsSidewaysVelocityQuickly()
        {
            var s = Run(new MovementInput(new Vector2(0f, 1f), 0f, false, false), 240);
            var input = new MovementInput(new Vector2(1f, 0f), 0f, false, false);
            for (int i = 0; i < Mathf.CeilToInt(T.TurnTime * 2f / Dt); i++) s = MovementMotor.Step(s, input, GroundInfo.Flat, T, Dt);
            Assert.Less(Mathf.Abs(s.PlanarVelocity.z), 0.5f, "old forward velocity must be mostly gone after ~2x TurnTime");
        }
    }
}
