using UnityEngine;

namespace Polykov.Weapons
{
    /// <summary>
    /// Pure, deterministic weapon simulation: fire rate, trigger discipline, magazine/chamber, tactical and empty
    /// reloads, aim and lowered state, per-shot recoil and spread. Same inputs + state = same result on client and
    /// server. No physics (hit detection is done by the caller from <see cref="WeaponState.SpreadOffset"/>).
    /// </summary>
    public static class WeaponMotor
    {
        /// <summary>Weapon counts as raised (can fire) below this lowered amount.</summary>
        public const float ReadyThreshold = 0.2f;
        /// <summary>Delay between dry-fire clicks when holding the trigger on an empty auto weapon.</summary>
        public const float DryFireInterval = 0.25f;

        public static WeaponState Step(in WeaponState state, in WeaponInput input, in WeaponContext context,
            in WeaponStats stats, float deltaTime)
        {
            WeaponState s = state;
            s.JustFired = false;
            s.JustDryFired = false;
            s.JustStartedReload = false;
            s.JustInsertedMagazine = false;
            s.JustFinishedReload = false;
            s.JustToggledSafety = false;
            s.JustStartedAction = false;
            s.JustCompletedAction = false;
            s.JustCancelledAction = false;
            s.RecoilKick = Vector2.zero;
            s.SpreadOffset = Vector2.zero;

            s.Cooldown = Mathf.Max(0f, s.Cooldown - deltaTime);

            bool lower = context.Sprinting || context.Obstructed;
            s.Lowered = Mathf.MoveTowards(s.Lowered, lower ? 1f : 0f, deltaTime / stats.ReadyTime);

            if (input.SafetyToggle)
            {
                s.SafetyOn = !s.SafetyOn;
                s.JustToggledSafety = true;
            }

            // Hand actions (inspect, chamber check): anything that needs the weapon interrupts them.
            bool actionBlocked = false;
            if (s.Action != WeaponAction.None)
            {
                bool again = (s.Action == WeaponAction.Inspect && input.InspectPressed)
                             || (s.Action == WeaponAction.ChamberCheck && input.ChamberCheckPressed);
                if (again || input.AimHeld || input.TriggerPressed || input.ReloadPressed || lower)
                {
                    s.Action = WeaponAction.None;
                    s.ActionElapsed = 0f;
                    s.JustCancelledAction = true;
                    actionBlocked = true; // the press that cancelled must not also fire or restart
                }
                else
                {
                    s.ActionElapsed += deltaTime;
                    float duration = s.Action == WeaponAction.Inspect ? stats.InspectTime : stats.ChamberCheckTime;
                    if (s.ActionElapsed >= duration)
                    {
                        if (s.Action == WeaponAction.ChamberCheck) s.LastChamberCheckLoaded = s.Chambered;
                        s.Action = WeaponAction.None;
                        s.ActionElapsed = 0f;
                        s.JustCompletedAction = true;
                    }
                }
            }
            else if (!s.IsReloading && !lower && !input.AimHeld && (input.InspectPressed || input.ChamberCheckPressed))
            {
                s.Action = input.ChamberCheckPressed ? WeaponAction.ChamberCheck : WeaponAction.Inspect;
                s.ActionElapsed = 0f;
                s.JustStartedAction = true;
            }

            bool aim = input.AimHeld && !lower && s.Action == WeaponAction.None;
            s.Aim = Mathf.MoveTowards(s.Aim, aim ? 1f : 0f, deltaTime / stats.AimTime);

            if (s.IsReloading) AdvanceReload(ref s, stats, deltaTime);
            else if (input.ReloadPressed && s.Action == WeaponAction.None && CanReload(s, stats)) StartReload(ref s);

            bool wantsShot = stats.FireMode == FireMode.Semi ? input.TriggerPressed : input.TriggerHeld;
            // The safety blocks the trigger itself: no shot and no dry-fire click.
            bool canFire = !s.IsReloading && s.Action == WeaponAction.None && !actionBlocked && !s.SafetyOn
                           && !lower && s.Lowered < ReadyThreshold && s.Cooldown <= 0f;
            if (wantsShot && canFire)
            {
                if (s.Chambered) Fire(ref s, stats);
                else if (input.TriggerPressed || stats.FireMode == FireMode.Auto)
                {
                    s.JustDryFired = true;
                    s.Cooldown = DryFireInterval;
                }
            }

            return s;
        }

        public static bool CanReload(in WeaponState s, in WeaponStats stats)
            => !s.IsReloading && s.Reserve > 0 && s.Magazine < stats.MagazineCapacity;

        public static float ReloadDuration(in WeaponState s, in WeaponStats stats)
            => s.Reload == ReloadKind.Empty ? stats.EmptyReloadTime : stats.TacticalReloadTime;

        private static void StartReload(ref WeaponState s)
        {
            s.Reload = s.Chambered ? ReloadKind.Tactical : ReloadKind.Empty;
            s.ReloadElapsed = 0f;
            s.MagazineInserted = false;
            s.JustStartedReload = true;
        }

        private static void AdvanceReload(ref WeaponState s, in WeaponStats stats, float deltaTime)
        {
            s.ReloadElapsed += deltaTime;
            float duration = ReloadDuration(s, stats);

            if (!s.MagazineInserted && s.ReloadElapsed >= duration * stats.MagazineInsertPoint)
            {
                // Old magazine's rounds go back to the pool, a fresh one is filled from it.
                s.Reserve += s.Magazine;
                int take = Mathf.Min(stats.MagazineCapacity, s.Reserve);
                s.Reserve -= take;
                s.Magazine = take;
                s.MagazineInserted = true;
                s.JustInsertedMagazine = true;
            }

            if (s.ReloadElapsed >= duration)
            {
                if (!s.Chambered && s.Magazine > 0)
                {
                    // Slide release.
                    s.Magazine--;
                    s.Chambered = true;
                }
                s.Reload = ReloadKind.None;
                s.ReloadElapsed = 0f;
                s.MagazineInserted = false;
                s.JustFinishedReload = true;
            }
        }

        private static void Fire(ref WeaponState s, in WeaponStats stats)
        {
            s.Chambered = false;
            s.ShotCount++;
            s.JustFired = true;
            s.Cooldown = stats.FireInterval;

            // The action cycles the next round from the magazine.
            if (s.Magazine > 0)
            {
                s.Magazine--;
                s.Chambered = true;
            }

            float recoil = Mathf.Lerp(1f, stats.AimRecoilMultiplier, s.Aim);
            float vertical = stats.VerticalRecoil * (1f + stats.RecoilVariance * Signed(s, 1u)) * recoil;
            float horizontal = stats.HorizontalRecoil * Signed(s, 2u) * recoil;
            s.RecoilKick = new Vector2(horizontal, vertical);

            // Uniform point in a cone of the current spread.
            float cone = Mathf.Lerp(stats.HipSpread, stats.AimSpread, s.Aim);
            float angle = Hash01(s, 3u) * Mathf.PI * 2f;
            float radius = Mathf.Sqrt(Hash01(s, 4u)) * cone;
            s.SpreadOffset = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
        }

        /// <summary>Deterministic value in [0, 1) for the current shot.</summary>
        public static float Hash01(in WeaponState s, uint salt)
        {
            unchecked
            {
                uint x = s.ShotCount * 0x9E3779B1u ^ s.Seed * 0x85EBCA6Bu ^ salt * 0xC2B2AE35u;
                x ^= x >> 16;
                x *= 0x7FEB352Du;
                x ^= x >> 15;
                x *= 0x846CA68Bu;
                x ^= x >> 16;
                return (x & 0xFFFFFFu) / 16777216f;
            }
        }

        private static float Signed(in WeaponState s, uint salt) => Hash01(s, salt) * 2f - 1f;
    }
}
