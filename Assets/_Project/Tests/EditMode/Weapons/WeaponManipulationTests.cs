using NUnit.Framework;
using UnityEngine;

namespace Polykov.Weapons.Tests
{
    public class WeaponManipulationTests
    {
        private const float Dt = 1f / 60f;

        private static WeaponStats M => WeaponStats.M1911;

        private static WeaponInput In(bool press = false, bool aim = false, bool reload = false, bool safety = false,
            bool inspect = false, bool check = false)
            => new WeaponInput(press, press, aim, reload, safety, inspect, check);

        private static WeaponState Step(WeaponState s, WeaponInput input, bool sprint = false)
            => WeaponMotor.Step(s, input, new WeaponContext(sprint, false), M, Dt);

        private static WeaponState Run(WeaponState s, WeaponInput input, int ticks)
        {
            for (int i = 0; i < ticks; i++) s = Step(s, input);
            return s;
        }

        private static int TicksFor(float seconds) => Mathf.CeilToInt(seconds / Dt) + 1;

        [Test]
        public void Safety_BlocksTheTrigger_NoShotNoClick()
        {
            var s = Step(WeaponState.Loaded(M, 0), In(safety: true));
            Assert.IsTrue(s.SafetyOn);
            Assert.IsTrue(s.JustToggledSafety);

            var pressed = Step(s, In(press: true));
            Assert.IsFalse(pressed.JustFired);
            Assert.IsFalse(pressed.JustDryFired);
            Assert.AreEqual(8, pressed.RoundsLoaded);

            var off = Step(pressed, In(safety: true));
            Assert.IsFalse(off.SafetyOn);
            Assert.IsTrue(Step(off, In(press: true)).JustFired);
        }

        [Test]
        public void Safety_DoesNotBlockReloading()
        {
            var s = Step(WeaponState.Loaded(M, 20), In(safety: true));
            s.Magazine = 3;
            Assert.IsTrue(Step(s, In(reload: true)).IsReloading);
        }

        [Test]
        public void Inspect_RunsForItsDuration_AndBlocksFiring()
        {
            var s = Step(WeaponState.Loaded(M, 0), In(inspect: true));
            Assert.AreEqual(WeaponAction.Inspect, s.Action);
            Assert.IsTrue(s.JustStartedAction);
            Assert.IsTrue(s.IsBusy);

            bool completed = false;
            for (int i = 0; i < TicksFor(M.InspectTime); i++)
            {
                s = Step(s, In());
                completed |= s.JustCompletedAction;
            }
            Assert.IsTrue(completed);
            Assert.AreEqual(WeaponAction.None, s.Action);
        }

        [Test]
        public void TriggerPress_CancelsInspect_WithoutFiringThatTick()
        {
            var s = Run(Step(WeaponState.Loaded(M, 0), In(inspect: true)), In(), 30);
            var pressed = Step(s, In(press: true));
            Assert.IsTrue(pressed.JustCancelledAction);
            Assert.IsFalse(pressed.JustFired, "the press that cancels must not also shoot");
            Assert.IsTrue(Step(Step(pressed, In()), In(press: true)).JustFired, "next press fires");
        }

        [Test]
        public void Aim_CancelsAction_AndAims()
        {
            var s = Run(Step(WeaponState.Loaded(M, 0), In(check: true)), In(), 10);
            s = Step(s, In(aim: true));
            Assert.IsTrue(s.JustCancelledAction);
            s = Run(s, In(aim: true), TicksFor(M.AimTime));
            Assert.AreEqual(1f, s.Aim, 1e-4f);
        }

        [Test]
        public void Reload_CancelsAction_AndStartsImmediately()
        {
            var loaded = WeaponState.Loaded(M, 20);
            loaded.Magazine = 2;
            var s = Run(Step(loaded, In(inspect: true)), In(), 10);
            s = Step(s, In(reload: true));
            Assert.IsTrue(s.JustCancelledAction);
            Assert.IsTrue(s.IsReloading);
        }

        [Test]
        public void Sprint_CancelsAction()
        {
            var s = Run(Step(WeaponState.Loaded(M, 0), In(inspect: true)), In(), 10);
            Assert.IsTrue(Step(s, In(), sprint: true).JustCancelledAction);
        }

        [Test]
        public void PressingInspectAgain_StopsIt()
        {
            var s = Run(Step(WeaponState.Loaded(M, 0), In(inspect: true)), In(), 10);
            s = Step(s, In(inspect: true));
            Assert.AreEqual(WeaponAction.None, s.Action);
            Assert.IsTrue(s.JustCancelledAction);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ChamberCheck_ReportsWhetherARoundIsChambered(bool chambered)
        {
            var start = WeaponState.Loaded(M, 0);
            start.Chambered = chambered;
            start.Magazine = chambered ? 7 : 0;
            var s = Step(start, In(check: true));
            Assert.AreEqual(WeaponAction.ChamberCheck, s.Action);
            bool done = false;
            for (int i = 0; i < TicksFor(M.ChamberCheckTime); i++)
            {
                s = Step(s, In());
                done |= s.JustCompletedAction;
            }
            Assert.IsTrue(done);
            Assert.AreEqual(chambered, s.LastChamberCheckLoaded);
        }

        [Test]
        public void Actions_CannotStart_WhileReloading()
        {
            var loaded = WeaponState.Loaded(M, 20);
            loaded.Magazine = 1;
            var s = Step(loaded, In(reload: true));
            s = Step(s, In(inspect: true));
            Assert.AreEqual(WeaponAction.None, s.Action);
            Assert.IsTrue(s.IsReloading);
        }
    }
}
