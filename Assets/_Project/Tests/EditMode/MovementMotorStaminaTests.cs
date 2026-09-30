using NUnit.Framework;
using UnityEngine;

namespace Polykov.Movement.Tests
{
    public class MovementMotorStaminaTests
    {
        private const float Dt = 1f / 60f;

        private static MovementTuning T => MovementTuning.Default;

        private static MovementInput Sprint => new MovementInput(new Vector2(0f, 1f), 0f, true, false);
        private static MovementInput Idle => new MovementInput(Vector2.zero, 0f, false, false);
        private static MovementInput Jump => new MovementInput(Vector2.zero, 0f, false, false, true, 0f);

        private static MovementState Run(MovementState s, MovementInput input, int ticks, MovementTuning? tuning = null)
        {
            MovementTuning t = tuning ?? T;
            for (int i = 0; i < ticks; i++) s = MovementMotor.Step(s, input, GroundInfo.Flat, t, Dt);
            return s;
        }

        private static int TicksFor(float seconds) => Mathf.CeilToInt(seconds / Dt);

        [Test]
        public void DefaultState_StartsRested()
        {
            MovementState s = default;
            Assert.AreEqual(1f, s.StaminaFraction(T));
            Assert.IsFalse(s.Exhausted);
        }

        [Test]
        public void Sprinting_DrainsStamina_AtConfiguredRate()
        {
            var s = Run(default, Sprint, TicksFor(2f));
            Assert.AreEqual(2f * T.SprintStaminaDrain, s.StaminaUsed, 0.05f);
            Assert.AreEqual(LocomotionState.Sprint, s.Locomotion);
        }

        [Test]
        public void RunningOutOfStamina_StopsSprint_UntilRecovered()
        {
            float secondsToEmpty = T.MaxStamina / T.SprintStaminaDrain;
            var s = Run(default, Sprint, TicksFor(secondsToEmpty) + 30);
            Assert.IsTrue(s.Exhausted);
            Assert.AreNotEqual(LocomotionState.Sprint, s.Locomotion);
            Assert.AreEqual(T.RunSpeed, s.PlanarSpeed, 0.1f, "exhausted: falls back to run speed");

            // Rest until the recovery threshold is passed.
            float toRecover = T.MaxStamina * T.ExhaustedRecovery / T.StaminaRegenRate + T.StaminaRegenDelay;
            var rested = Run(s, Idle, TicksFor(toRecover) + 5);
            Assert.IsFalse(rested.Exhausted);
            Assert.AreEqual(LocomotionState.Sprint, Run(rested, Sprint, 60).Locomotion);
        }

        [Test]
        public void Stamina_RecoversOnlyAfterDelay()
        {
            var tired = Run(default, Sprint, TicksFor(3f));
            float used = tired.StaminaUsed;
            var justStopped = Run(tired, Idle, TicksFor(T.StaminaRegenDelay * 0.5f));
            Assert.AreEqual(used, justStopped.StaminaUsed, 1e-4f);
            var later = Run(tired, Idle, TicksFor(T.StaminaRegenDelay + 1f));
            Assert.Less(later.StaminaUsed, used);
        }

        [Test]
        public void Jump_CostsStamina_AndIsBlockedWhenExhausted()
        {
            var s = MovementMotor.Step(default, Jump, GroundInfo.Flat, T, Dt);
            Assert.IsTrue(s.JustJumped);
            Assert.AreEqual(T.JumpStaminaCost, s.StaminaUsed, 1e-4f);

            var exhausted = default(MovementState);
            exhausted.StaminaUsed = T.MaxStamina;
            exhausted.Exhausted = true;
            Assert.IsFalse(MovementMotor.Step(exhausted, Jump, GroundInfo.Flat, T, Dt).JustJumped);
        }

        [Test]
        public void ZeroMaxStamina_DisablesTheSystem()
        {
            MovementTuning t = T;
            t.MaxStamina = 0f;
            var s = Run(default, Sprint, TicksFor(30f), t);
            Assert.AreEqual(LocomotionState.Sprint, s.Locomotion);
            Assert.AreEqual(0f, s.StaminaUsed);
            Assert.AreEqual(1f, s.StaminaFraction(t));
        }
    }
}
