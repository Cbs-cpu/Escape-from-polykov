using System;

namespace Polykov.Weapons
{
    public enum WeaponAction : byte
    {
        Holstered = 0,
        Equipping = 1,
        Ready = 2,
        Holstering = 3,
        Reloading = 4,
    }

    public enum ReloadKind : byte
    {
        Tactical = 0,
        Empty = 1,
    }

    /// <summary>
    /// Runtime state of one weapon. Authoritative on the server in multiplayer; never stored in a ScriptableObject.
    /// </summary>
    [Serializable]
    public struct WeaponState
    {
        public WeaponAction Action;
        /// <summary>Normalized progress (0..1) of the current timed action (equip, holster, reload).</summary>
        public float ActionProgress;
        public ReloadKind ReloadKind;

        public int AmmoInMagazine;
        public bool HasMagazine;
        public bool Chambered;
        public int Reserve;
        /// <summary>Slide held open on an empty magazine.</summary>
        public bool SlideLocked;

        public float Cooldown;
        public float SprintRecovery;
        /// <summary>0 = hip, 1 = fully aimed down sights.</summary>
        public float Aim;
        public float Bloom;
        /// <summary>Monotonic shot counter; seeds deterministic spread shared by client and server.</summary>
        public uint ShotIndex;

        public int TotalLoaded => AmmoInMagazine + (Chambered ? 1 : 0);

        public static WeaponState CreateLoaded(in WeaponTuning tuning, bool equipped)
        {
            return new WeaponState
            {
                Action = equipped ? WeaponAction.Ready : WeaponAction.Holstered,
                ActionProgress = 1f,
                AmmoInMagazine = tuning.MagazineCapacity,
                HasMagazine = true,
                Chambered = true,
                Reserve = tuning.StartingReserve,
            };
        }
    }

    /// <summary>One step of weapon intent from the owning player. What the client will send to the server.</summary>
    public readonly struct WeaponCommand
    {
        public readonly bool TriggerHeld;
        /// <summary>A trigger pull to honor this step (fresh press, possibly buffered by the client).</summary>
        public readonly bool FirePressed;
        public readonly bool Aim;
        public readonly bool Reload;
        public readonly bool ToggleEquip;
        public readonly bool Sprinting;
        public readonly bool Grounded;
        /// <summary>Planar speed divided by run speed (0..~1.6).</summary>
        public readonly float MoveFactor;

        public WeaponCommand(bool triggerHeld, bool firePressed, bool aim, bool reload, bool toggleEquip, bool sprinting,
            bool grounded, float moveFactor)
        {
            TriggerHeld = triggerHeld;
            FirePressed = firePressed;
            Aim = aim;
            Reload = reload;
            ToggleEquip = toggleEquip;
            Sprinting = sprinting;
            Grounded = grounded;
            MoveFactor = moveFactor;
        }
    }

    [Flags]
    public enum WeaponEvents
    {
        None = 0,
        Fired = 1 << 0,
        DryFired = 1 << 1,
        ReloadStarted = 1 << 2,
        MagazineOut = 1 << 3,
        MagazineIn = 1 << 4,
        SlideReleased = 1 << 5,
        ReloadCompleted = 1 << 6,
        EquipStarted = 1 << 7,
        EquipCompleted = 1 << 8,
        HolsterStarted = 1 << 9,
        HolsterCompleted = 1 << 10,
        SlideLockedBack = 1 << 11,
    }
}
