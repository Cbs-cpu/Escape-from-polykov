using NUnit.Framework;
using Polykov.Movement;
using Polykov.Weapons;
using UnityEngine;

namespace Polykov.Netcode.Tests
{
    public class PlayerCommandTests
    {
        private static PlayerCommand Sample(uint tick = 1234567u) => new PlayerCommand(tick,
            new MovementInput(new Vector2(0.31f, -0.87f), 271.3f, true, false, true, -0.42f, true),
            new WeaponInput(true, false, true, true, true, false, true), 37.25f);

        [Test]
        public void RoundTrip_PreservesIntent_WithinQuantization()
        {
            var buffer = new byte[64];
            var w = new ByteWriter(buffer);
            Sample().Write(ref w);
            Assert.AreEqual(PlayerCommand.SerializedSize, w.Length);

            var r = new ByteReader(buffer, w.Length);
            PlayerCommand c = PlayerCommand.Read(ref r);
            Assert.AreEqual(1234567u, c.Tick);
            Assert.AreEqual(0.31f, c.Movement.Move.x, 1f / 127f);
            Assert.AreEqual(-0.87f, c.Movement.Move.y, 1f / 127f);
            Assert.AreEqual(271.3f, c.Movement.Yaw, 360f / 65536f);
            Assert.AreEqual(37.25f, c.Pitch, 0.01f);
            Assert.AreEqual(-0.42f, c.Movement.Lean, 1f / 127f);
            Assert.IsTrue(c.Movement.Sprint);
            Assert.IsFalse(c.Movement.Walk);
            Assert.IsTrue(c.Movement.Jump);
            Assert.IsTrue(c.Movement.Crouch);
            Assert.IsTrue(c.Weapon.TriggerHeld);
            Assert.IsFalse(c.Weapon.TriggerPressed);
            Assert.IsTrue(c.Weapon.AimHeld);
            Assert.IsTrue(c.Weapon.ReloadPressed);
            Assert.IsTrue(c.Weapon.SafetyToggle);
            Assert.IsFalse(c.Weapon.InspectPressed);
            Assert.IsTrue(c.Weapon.ChamberCheckPressed);
        }

        [Test]
        public void Quantized_IsIdempotent()
        {
            PlayerCommand once = Sample().Quantized();
            PlayerCommand twice = once.Quantized();
            Assert.AreEqual(once.Movement.Move.x, twice.Movement.Move.x);
            Assert.AreEqual(once.Movement.Move.y, twice.Movement.Move.y);
            Assert.AreEqual(once.Movement.Yaw, twice.Movement.Yaw);
            Assert.AreEqual(once.Pitch, twice.Pitch);
            Assert.AreEqual(once.Movement.Lean, twice.Movement.Lean);
        }

        [Test]
        public void YawWrapsIntoRange()
        {
            var c = new PlayerCommand(1, new MovementInput(Vector2.zero, -90f, false, false), default, 0f).Quantized();
            Assert.AreEqual(270f, c.Movement.Yaw, 0.01f);
        }

        [Test]
        public void Repeat_DropsOneShotActions()
        {
            PlayerCommand repeat = Sample().AsRepeat(99);
            Assert.AreEqual(99u, repeat.Tick);
            Assert.IsFalse(repeat.Movement.Jump);
            Assert.IsFalse(repeat.Weapon.ReloadPressed);
            Assert.IsFalse(repeat.Weapon.TriggerPressed);
            Assert.IsFalse(repeat.Weapon.SafetyToggle, "toggles must never repeat");
            Assert.IsFalse(repeat.Weapon.ChamberCheckPressed);
            Assert.IsTrue(repeat.Movement.Sprint, "held intent is kept");
            Assert.IsTrue(repeat.Weapon.TriggerHeld);
        }
    }
}
