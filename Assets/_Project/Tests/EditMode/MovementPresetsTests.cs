using NUnit.Framework;
using UnityEngine;

namespace Polykov.Movement.Tests
{
    public class MovementPresetsTests
    {
        private const float Dt = 1f / 60f;

        [Test]
        public void EveryPreset_HasAName_AndSaneValues()
        {
            Assert.AreEqual(MovementPresets.Names.Length, MovementPresets.Count);
            for (int i = 0; i < MovementPresets.Count; i++)
            {
                MovementTuning t = MovementPresets.Get(i);
                Assert.Less(t.WalkSpeed, t.RunSpeed, MovementPresets.Names[i]);
                Assert.Less(t.RunSpeed, t.SprintSpeed, MovementPresets.Names[i]);
                Assert.Greater(t.AccelerationTime, 0f);
                Assert.Greater(t.DecelerationTime, 0f);
                Assert.Greater(t.SprintAccelerationTime, 0f);
            }
        }

        [Test]
        public void EveryPreset_ReachesItsRunSpeed()
        {
            for (int i = 0; i < MovementPresets.Count; i++)
            {
                MovementTuning t = MovementPresets.Get(i);
                var s = default(MovementState);
                var input = new MovementInput(new Vector2(0f, 1f), 0f, false, false);
                for (int k = 0; k < 240; k++) s = MovementMotor.Step(s, input, GroundInfo.Flat, t, Dt);
                Assert.AreEqual(t.RunSpeed, s.PlanarSpeed, 0.05f, MovementPresets.Names[i]);
            }
        }

        [Test]
        public void TacticalPreset_IsHeavierThanArcade()
        {
            MovementTuning tactical = MovementPresets.Get(1);
            MovementTuning arcade = MovementPresets.Get(2);
            Assert.Greater(tactical.AccelerationTime, arcade.AccelerationTime);
            Assert.Greater(tactical.DecelerationTime, arcade.DecelerationTime);
        }
    }
}
