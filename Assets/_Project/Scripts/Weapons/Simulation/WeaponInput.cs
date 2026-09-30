namespace Polykov.Weapons
{
    /// <summary>One tick of weapon intent. Sent to the server in Phase 2 together with MovementInput.</summary>
    public readonly struct WeaponInput
    {
        public readonly bool TriggerHeld;
        /// <summary>Trigger went down since the previous tick (edge, latched so fast clicks are never lost).</summary>
        public readonly bool TriggerPressed;
        public readonly bool AimHeld;
        /// <summary>Reload was pressed since the previous tick (edge).</summary>
        public readonly bool ReloadPressed;

        public WeaponInput(bool triggerHeld, bool triggerPressed, bool aimHeld, bool reloadPressed)
        {
            TriggerHeld = triggerHeld;
            TriggerPressed = triggerPressed;
            AimHeld = aimHeld;
            ReloadPressed = reloadPressed;
        }
    }

    /// <summary>What the rest of the character allows the weapon to do this tick (from movement and environment).</summary>
    public readonly struct WeaponContext
    {
        /// <summary>Sprinting: weapon lowered, no firing, no aiming.</summary>
        public readonly bool Sprinting;
        /// <summary>Muzzle blocked by a wall or similar: weapon pulled back, no firing.</summary>
        public readonly bool Obstructed;

        public WeaponContext(bool sprinting, bool obstructed)
        {
            Sprinting = sprinting;
            Obstructed = obstructed;
        }

        public static WeaponContext Ready => new WeaponContext(false, false);
    }
}
