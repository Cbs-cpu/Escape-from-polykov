using System;
using UnityEngine;

namespace Polykov.Weapons
{
    public enum ReloadKind : byte
    {
        None = 0,
        /// <summary>Round still chambered: swap magazine only.</summary>
        Tactical = 1,
        /// <summary>Weapon empty: swap magazine and release the slide.</summary>
        Empty = 2,
    }

    /// <summary>Hand manipulation that occupies the weapon (no firing while it runs).</summary>
    public enum WeaponAction : byte
    {
        None = 0,
        /// <summary>Turning the weapon to look at it.</summary>
        Inspect = 1,
        /// <summary>Pulling the slide back slightly to see if a round is chambered.</summary>
        ChamberCheck = 2,
    }

    /// <summary>Runtime state of one weapon. Replicated in multiplayer; never stored in a ScriptableObject.</summary>
    [Serializable]
    public struct WeaponState
    {
        public int Magazine;
        public bool Chambered;
        /// <summary>Loose spare rounds carried for this weapon (inventory replaces this in a later phase).</summary>
        public int Reserve;

        /// <summary>Seconds until the action has cycled and can fire again.</summary>
        public float Cooldown;
        public ReloadKind Reload;
        /// <summary>Seconds elapsed in the current reload.</summary>
        public float ReloadElapsed;
        /// <summary>The new magazine has been seated during the current reload.</summary>
        public bool MagazineInserted;

        /// <summary>0 = hip, 1 = fully aimed down sights.</summary>
        public float Aim;
        /// <summary>Weapon lowered (sprint or obstruction), 0 = ready, 1 = fully lowered.</summary>
        public float Lowered;

        /// <summary>Manual safety engaged: the trigger is blocked.</summary>
        public bool SafetyOn;
        public WeaponAction Action;
        /// <summary>Seconds elapsed in the current action.</summary>
        public float ActionElapsed;
        /// <summary>What the last completed chamber check found.</summary>
        public bool LastChamberCheckLoaded;

        /// <summary>Total shots fired; with <see cref="Seed"/> drives deterministic spread and recoil.</summary>
        public uint ShotCount;
        /// <summary>Per-weapon random seed (server-assigned in multiplayer).</summary>
        public uint Seed;

        // One-tick events for presentation (animation, audio, VFX, camera).
        public bool JustFired;
        public bool JustDryFired;
        public bool JustStartedReload;
        public bool JustInsertedMagazine;
        public bool JustFinishedReload;
        public bool JustToggledSafety;
        public bool JustStartedAction;
        /// <summary>The action ran to completion this tick (chamber check result is valid).</summary>
        public bool JustCompletedAction;
        /// <summary>The action was interrupted this tick (aim, fire, reload, sprint...).</summary>
        public bool JustCancelledAction;

        /// <summary>Recoil of the shot fired this tick (degrees, x = yaw, y = pitch up).</summary>
        public Vector2 RecoilKick;
        /// <summary>Spread offset of the shot fired this tick (degrees, x = yaw, y = pitch up).</summary>
        public Vector2 SpreadOffset;

        public bool IsReloading => Reload != ReloadKind.None;
        public bool IsBusy => IsReloading || Action != WeaponAction.None;
        public int RoundsLoaded => Magazine + (Chambered ? 1 : 0);
        /// <summary>Slide locked back (empty and not chambered) — the M1911 look after the last shot.</summary>
        public bool SlideLocked => !Chambered && Magazine == 0;

        /// <summary>Ready to fire: full magazine, plus a round in the chamber if the weapon allows it.</summary>
        public static WeaponState Loaded(in WeaponStats stats, int reserve, uint seed = 0) => new WeaponState
        {
            Magazine = stats.ChambersRound ? stats.MagazineCapacity : stats.MagazineCapacity - 1,
            Chambered = true,
            Reserve = reserve,
            Seed = seed,
        };
    }
}
