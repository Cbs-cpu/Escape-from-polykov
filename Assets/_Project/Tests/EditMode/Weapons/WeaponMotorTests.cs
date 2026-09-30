using NUnit.Framework;
using UnityEngine;

namespace Polykov.Weapons.Tests
{
    public class WeaponMotorTests
    {
        private const float Dt = 1f / 60f;

        private static WeaponStats M => WeaponStats.M1911;

        private static WeaponInput Idle => new WeaponInput(false, false, false, false);
        private static WeaponInput Press => new WeaponInput(true, true, false, false);
        private static WeaponInput Hold => new WeaponInput(true, false, false, false);
        private static WeaponInput Aim => new WeaponInput(false, false, true, false);
        private static WeaponInput Reload => new WeaponInput(false, false, false, true);

        private static WeaponState Step(WeaponState s, WeaponInput input, WeaponContext? context = null)
            => WeaponMotor.Step(s, input, context ?? WeaponContext.Ready, M, Dt);

        private static WeaponState Run(WeaponState s, WeaponInput input, int ticks, WeaponContext? context = null)
        {
            for (int i = 0; i < ticks; i++) s = Step(s, input, context);
            return s;
        }

        private static int TicksFor(float seconds) => Mathf.CeilToInt(seconds / Dt) + 1;

        /// <summary>Press the trigger once and wait for the action to cycle.</summary>
        private static WeaponState Shoot(WeaponState s)
        {
            s = Step(s, Press);
            return Run(s, Idle, TicksFor(M.FireInterval));
        }

        [Test]
        public void Loaded_M1911_HasSevenPlusOne()
        {
            var s = WeaponState.Loaded(M, 21);
            Assert.AreEqual(7, s.Magazine);
            Assert.IsTrue(s.Chambered);
            Assert.AreEqual(8, s.RoundsLoaded);
        }

        [Test]
        public void TriggerPress_FiresOnce_AndChambersNextRound()
        {
            var s = Step(WeaponState.Loaded(M, 0), Press);
            Assert.IsTrue(s.JustFired);
            Assert.AreEqual(6, s.Magazine);
            Assert.IsTrue(s.Chambered);
            Assert.AreEqual(1u, s.ShotCount);
        }

        [Test]
        public void SemiAuto_HoldingTrigger_DoesNotFireAgain()
        {
            var s = Step(WeaponState.Loaded(M, 0), Press);
            int fired = 0;
            for (int i = 0; i < 120; i++)
            {
                s = Step(s, Hold);
                if (s.JustFired) fired++;
            }
            Assert.AreEqual(0, fired);
        }

        [Test]
        public void FireRate_IsCappedByRoundsPerMinute()
        {
            var s = Step(WeaponState.Loaded(M, 0), Press);
            s = Step(s, Idle);
            var tooSoon = Step(s, Press);
            Assert.IsFalse(tooSoon.JustFired, "a second press inside the fire interval must not fire");

            s = Run(s, Idle, TicksFor(M.FireInterval));
            Assert.IsTrue(Step(s, Press).JustFired);
        }

        [Test]
        public void AutoMode_FiresContinuouslyAtCyclicRate()
        {
            WeaponStats auto = M;
            auto.FireMode = FireMode.Auto;
            auto.MagazineCapacity = 100;
            auto.RoundsPerMinute = 600f;
            var s = WeaponState.Loaded(auto, 0);
            int fired = 0;
            for (int i = 0; i < 60; i++)
            {
                s = WeaponMotor.Step(s, Hold, WeaponContext.Ready, auto, Dt);
                if (s.JustFired) fired++;
            }
            Assert.AreEqual(10, fired, 1, "600 RPM for one second");
        }

        [Test]
        public void EmptyingTheWeapon_LocksSlide_ThenDryFires()
        {
            var s = WeaponState.Loaded(M, 0);
            for (int i = 0; i < 8; i++) s = Shoot(s);
            Assert.AreEqual(0, s.RoundsLoaded);
            Assert.IsTrue(s.SlideLocked);

            var click = Step(s, Press);
            Assert.IsFalse(click.JustFired);
            Assert.IsTrue(click.JustDryFired);
            Assert.AreEqual(8u, click.ShotCount);
        }

        [Test]
        public void TacticalReload_KeepsChamberedRound_SevenPlusOne()
        {
            var s = Shoot(Shoot(WeaponState.Loaded(M, 20)));
            Assert.AreEqual(5, s.Magazine);

            s = Step(s, Reload);
            Assert.IsTrue(s.JustStartedReload);
            Assert.AreEqual(ReloadKind.Tactical, s.Reload);

            bool finished = false;
            for (int i = 0; i < TicksFor(M.TacticalReloadTime); i++)
            {
                s = Step(s, Idle);
                finished |= s.JustFinishedReload;
            }
            Assert.IsTrue(finished);
            Assert.IsFalse(s.IsReloading);
            Assert.AreEqual(7, s.Magazine);
            Assert.IsTrue(s.Chambered);
            Assert.AreEqual(8, s.RoundsLoaded);
            Assert.AreEqual(20 + 5 - 7, s.Reserve, "old magazine rounds return to the pool");
        }

        [Test]
        public void EmptyReload_IsLonger_AndReleasesSlide()
        {
            var s = WeaponState.Loaded(M, 20);
            for (int i = 0; i < 8; i++) s = Shoot(s);
            s = Step(s, Reload);
            Assert.AreEqual(ReloadKind.Empty, s.Reload);

            var notYet = Run(s, Idle, TicksFor(M.TacticalReloadTime) - 2);
            Assert.IsTrue(notYet.IsReloading, "empty reload must take longer than a tactical one");

            s = Run(s, Idle, TicksFor(M.EmptyReloadTime));
            Assert.IsFalse(s.IsReloading);
            Assert.IsTrue(s.Chambered);
            Assert.AreEqual(6, s.Magazine);
            Assert.AreEqual(7, s.RoundsLoaded);
            Assert.AreEqual(13, s.Reserve);
        }

        [Test]
        public void AmmoCountsChange_OnlyWhenMagazineIsSeated()
        {
            var s = Step(Shoot(WeaponState.Loaded(M, 20)), Reload);
            int before = s.Magazine;
            s = Run(s, Idle, Mathf.FloorToInt(M.TacticalReloadTime * M.MagazineInsertPoint / Dt) - 2);
            Assert.AreEqual(before, s.Magazine);
            Assert.IsFalse(s.MagazineInserted);
            s = Run(s, Idle, 4);
            Assert.IsTrue(s.MagazineInserted);
            Assert.AreEqual(7, s.Magazine);
        }

        [Test]
        public void Reload_IsIgnored_WhenFullOrNoReserve()
        {
            Assert.IsFalse(Step(WeaponState.Loaded(M, 20), Reload).IsReloading, "full magazine");
            Assert.IsFalse(Step(Shoot(WeaponState.Loaded(M, 0)), Reload).IsReloading, "no spare ammo");
        }

        [Test]
        public void PartialReserve_FillsWhatItCan()
        {
            var s = WeaponState.Loaded(M, 3);
            for (int i = 0; i < 8; i++) s = Shoot(s);
            s = Run(Step(s, Reload), Idle, TicksFor(M.EmptyReloadTime));
            Assert.AreEqual(3, s.RoundsLoaded);
            Assert.AreEqual(0, s.Reserve);
        }

        [Test]
        public void CannotFire_WhileReloading()
        {
            var s = Step(Shoot(WeaponState.Loaded(M, 20)), Reload);
            s = Step(s, Press);
            Assert.IsFalse(s.JustFired);
            Assert.IsFalse(s.JustDryFired);
        }

        [Test]
        public void Sprinting_LowersWeapon_BlocksFireAndAim_ThenNeedsTimeToRaise()
        {
            var sprint = new WeaponContext(true, false);
            var s = Run(WeaponState.Loaded(M, 0), new WeaponInput(false, false, true, false), 30, sprint);
            Assert.AreEqual(1f, s.Lowered, 1e-4f);
            Assert.AreEqual(0f, s.Aim, 1e-4f);
            Assert.IsFalse(Step(s, Press, sprint).JustFired);

            var justStopped = Step(s, Press);
            Assert.IsFalse(justStopped.JustFired, "weapon must be raised before it can fire");

            s = Run(s, Idle, TicksFor(M.ReadyTime));
            Assert.IsTrue(Step(s, Press).JustFired);
        }

        [Test]
        public void Obstructed_BlocksFire()
        {
            var s = Run(WeaponState.Loaded(M, 0), Idle, 30, new WeaponContext(false, true));
            Assert.IsFalse(Step(s, Press, new WeaponContext(false, true)).JustFired);
        }

        [Test]
        public void Aim_ReachesFullWithinAimTime_Progressively()
        {
            var one = Step(WeaponState.Loaded(M, 0), Aim);
            Assert.Greater(one.Aim, 0f);
            Assert.Less(one.Aim, 0.5f);
            var full = Run(WeaponState.Loaded(M, 0), Aim, TicksFor(M.AimTime));
            Assert.AreEqual(1f, full.Aim, 1e-4f);
        }

        [Test]
        public void Recoil_KicksUp_WithinConfiguredBounds()
        {
            var s = WeaponState.Loaded(M, 0);
            for (int i = 0; i < 8; i++)
            {
                s = Step(s, Press);
                Assert.IsTrue(s.JustFired);
                Assert.Greater(s.RecoilKick.y, 0f);
                Assert.LessOrEqual(s.RecoilKick.y, M.VerticalRecoil * (1f + M.RecoilVariance) + 1e-4f);
                Assert.LessOrEqual(Mathf.Abs(s.RecoilKick.x), M.HorizontalRecoil + 1e-4f);
                Assert.LessOrEqual(s.SpreadOffset.magnitude, M.HipSpread + 1e-4f);
                s = Run(s, Idle, TicksFor(M.FireInterval));
            }
        }

        [Test]
        public void Aiming_ReducesRecoilAndSpread()
        {
            var hip = Step(WeaponState.Loaded(M, 0), Press);
            var aimed = Run(WeaponState.Loaded(M, 0), Aim, TicksFor(M.AimTime));
            aimed = Step(aimed, new WeaponInput(true, true, true, false));
            Assert.Less(aimed.RecoilKick.y, hip.RecoilKick.y);
            Assert.LessOrEqual(aimed.SpreadOffset.magnitude, M.AimSpread + 1e-4f);
        }

        [Test]
        public void SameSeedAndInputs_GiveIdenticalShots()
        {
            var a = Step(WeaponState.Loaded(M, 0, seed: 42), Press);
            var b = Step(WeaponState.Loaded(M, 0, seed: 42), Press);
            Assert.AreEqual(a.RecoilKick.x, b.RecoilKick.x);
            Assert.AreEqual(a.RecoilKick.y, b.RecoilKick.y);
            Assert.AreEqual(a.SpreadOffset.x, b.SpreadOffset.x);

            var c = Step(WeaponState.Loaded(M, 0, seed: 7), Press);
            Assert.AreNotEqual(a.SpreadOffset.x, c.SpreadOffset.x, "different seeds should differ");
        }

        [Test]
        public void EventFlags_LastOneTickOnly()
        {
            var s = Step(WeaponState.Loaded(M, 0), Press);
            Assert.IsTrue(s.JustFired);
            s = Step(s, Idle);
            Assert.IsFalse(s.JustFired);
            Assert.AreEqual(Vector2.zero, s.RecoilKick);
        }
    }
}
