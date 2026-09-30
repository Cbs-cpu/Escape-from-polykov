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

        /// <summary>Recoil of the shot fired this tick (degrees, x = yaw, y = pitch up).</summary>
        public Vector2 RecoilKick;
        /// <summary>Spread offset of the shot fired this tick (degrees, x = yaw, y = pitch up).</summary>
        public Vector2 SpreadOffset;

        public bool IsReloading => Reload != ReloadKind.None;
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
