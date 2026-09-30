using UnityEngine;

namespace Polykov.Weapons
{
    /// <summary>
    /// Pure, deterministic weapon rules: trigger, cadence, chamber/magazine accounting, reload timeline,
    /// equip/holster and aim. No Unity objects, no randomness except the seeded spread helper.
    /// The client runs it for instant feedback; the server runs it to validate.
    /// </summary>
    public static class WeaponSimulation
    {
        // Normalized reload timelines (fractions of the reload duration).
        public const float TacticalMagOut = 0.22f;
        public const float TacticalMagIn = 0.62f;
        public const float EmptyMagOut = 0.16f;
        public const float EmptyMagIn = 0.50f;
        public const float EmptySlideRelease = 0.76f;

        public static WeaponState Step(in WeaponState state, in WeaponCommand cmd, in WeaponTuning tuning, float dt,
            out WeaponEvents events)
        {
            events = WeaponEvents.None;
            WeaponState s = state;
            s.Cooldown = Mathf.Max(0f, s.Cooldown - dt);
            s.Bloom = Mathf.Max(0f, s.Bloom - tuning.BloomRecovery * dt);
            s.SprintRecovery = cmd.Sprinting ? tuning.SprintToFireTime : Mathf.Max(0f, s.SprintRecovery - dt);

            // ---- equip / holster
            if (cmd.ToggleEquip)
            {
                if (s.Action == WeaponAction.Holstered || s.Action == WeaponAction.Holstering)
                {
                    s.ActionProgress = s.Action == WeaponAction.Holstering ? 1f - s.ActionProgress : 0f;
                    s.Action = WeaponAction.Equipping;
                    events |= WeaponEvents.EquipStarted;
                }
                else if (s.Action == WeaponAction.Ready || s.Action == WeaponAction.Equipping)
                {
                    s.ActionProgress = s.Action == WeaponAction.Equipping ? 1f - s.ActionProgress : 0f;
                    s.Action = WeaponAction.Holstering;
                    events |= WeaponEvents.HolsterStarted;
                }
            }

            // ---- reload start
            if (cmd.Reload && s.Action == WeaponAction.Ready && s.Reserve > 0
                && (s.AmmoInMagazine < tuning.MagazineCapacity || !s.HasMagazine))
            {
                s.Action = WeaponAction.Reloading;
                s.ActionProgress = 0f;
                s.ReloadKind = s.Chambered ? ReloadKind.Tactical : ReloadKind.Empty;
                events |= WeaponEvents.ReloadStarted;
            }

            // ---- timed actions
            switch (s.Action)
            {
                case WeaponAction.Equipping:
                    s.ActionProgress += dt / tuning.EquipTime;
                    if (s.ActionProgress >= 1f)
                    {
                        s.ActionProgress = 1f;
                        s.Action = WeaponAction.Ready;
                        events |= WeaponEvents.EquipCompleted;
                    }
                    break;
                case WeaponAction.Holstering:
                    s.ActionProgress += dt / tuning.HolsterTime;
                    if (s.ActionProgress >= 1f)
                    {
                        s.ActionProgress = 1f;
                        s.Action = WeaponAction.Holstered;
                        events |= WeaponEvents.HolsterCompleted;
                    }
                    break;
                case WeaponAction.Reloading:
                    AdvanceReload(ref s, tuning, dt, ref events);
                    break;
            }

            // ---- trigger
            bool pull = tuning.FireMode == FireMode.Auto ? cmd.TriggerHeld || cmd.FirePressed : cmd.FirePressed;
            bool canFire = s.Action == WeaponAction.Ready && s.Cooldown <= 0f && s.SprintRecovery <= 0f && !cmd.Sprinting;
            if (canFire && pull)
            {
                if (s.Chambered)
                {
                    events |= WeaponEvents.Fired;
                    s.ShotIndex++;
                    s.Cooldown = tuning.FireInterval;
                    s.Bloom = Mathf.Min(tuning.MaxBloom, s.Bloom + tuning.ShotBloom);
                    if (s.HasMagazine && s.AmmoInMagazine > 0)
                    {
                        s.AmmoInMagazine--;
                    }
                    else
                    {
                        s.Chambered = false;
                        s.SlideLocked = true;
                        events |= WeaponEvents.SlideLockedBack;
                    }
                }
                else if (cmd.FirePressed)
                {
                    events |= WeaponEvents.DryFired;
                    s.Cooldown = tuning.FireInterval;
                }
            }

            // ---- aim
            float aimTarget = cmd.Aim && s.Action == WeaponAction.Ready && !cmd.Sprinting ? 1f : 0f;
            s.Aim = Mathf.MoveTowards(s.Aim, aimTarget, dt / tuning.AimTime);
            return s;
        }

        private static void AdvanceReload(ref WeaponState s, in WeaponTuning tuning, float dt, ref WeaponEvents events)
        {
            bool empty = s.ReloadKind == ReloadKind.Empty;
            float duration = empty ? tuning.EmptyReloadTime : tuning.TacticalReloadTime;
            float before = s.ActionProgress;
            float after = Mathf.Min(1f, before + dt / duration);
            s.ActionProgress = after;

            if (Crossed(before, after, empty ? EmptyMagOut : TacticalMagOut))
            {
                s.Reserve += s.AmmoInMagazine;
                s.AmmoInMagazine = 0;
                s.HasMagazine = false;
                events |= WeaponEvents.MagazineOut;
            }
            if (Crossed(before, after, empty ? EmptyMagIn : TacticalMagIn))
            {
                int take = Mathf.Min(tuning.MagazineCapacity, s.Reserve);
                s.Reserve -= take;
                s.AmmoInMagazine = take;
                s.HasMagazine = true;
                events |= WeaponEvents.MagazineIn;
            }
            if (empty && Crossed(before, after, EmptySlideRelease) && s.HasMagazine && s.AmmoInMagazine > 0)
            {
                s.AmmoInMagazine--;
                s.Chambered = true;
                s.SlideLocked = false;
                events |= WeaponEvents.SlideReleased;
            }
            if (after >= 1f)
            {
                s.Action = WeaponAction.Ready;
                events |= WeaponEvents.ReloadCompleted;
            }
        }

        /// <summary>True when a trigger pull this step would produce a shot or a dry fire.</summary>
        public static bool CanPull(in WeaponState state, bool sprinting)
            => state.Action == WeaponAction.Ready && state.Cooldown <= 0f && state.SprintRecovery <= 0f && !sprinting;

        private static bool Crossed(float before, float after, float marker) => before < marker && after >= marker;

        /// <summary>Current cone half-angle in degrees.</summary>
        public static float Spread(in WeaponState state, in WeaponCommand cmd, in WeaponTuning tuning)
        {
            float aim = state.Aim;
            float spread = Mathf.Lerp(tuning.HipSpread, tuning.AimSpread, aim);
            spread += tuning.MovementSpread * Mathf.Clamp01(cmd.MoveFactor) * (1f - 0.6f * aim);
            if (!cmd.Grounded) spread += tuning.AirborneSpread;
            spread += state.Bloom * (1f - 0.5f * aim);
            return spread;
        }

        /// <summary>
        /// Deterministic point in the unit disk for a given shot index, so client and server agree on spread.
        /// Returns degrees (x = yaw, y = pitch) scaled by the cone half-angle.
        /// </summary>
        public static Vector2 SpreadOffset(uint seed, float coneDegrees)
        {
            uint h1 = Hash(seed * 2u + 1u);
            uint h2 = Hash(seed * 2u + 2u);
            float r = Mathf.Sqrt(h1 / (float)uint.MaxValue);
            float a = h2 / (float)uint.MaxValue * Mathf.PI * 2f;
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (r * coneDegrees);
        }

        private static uint Hash(uint x)
        {
            x ^= x >> 16;
            x *= 0x7feb352du;
            x ^= x >> 15;
            x *= 0x846ca68bu;
            x ^= x >> 16;
            return x;
        }
    }
}
