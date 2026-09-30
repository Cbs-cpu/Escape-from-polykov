namespace Polykov.Weapons
{
    /// <summary>
    /// What the player carries into the match, handed from the lobby to the raid scene (static: survives the scene
    /// load, no engine types). In multiplayer the server sends this after validating the profile.
    /// </summary>
    public static class RaidLoadout
    {
        /// <summary>Weapon family in hands at spawn (the equipped primary, else the holster), or null = scene default.</summary>
        public static string FamilyId;
        public static WeaponBuild Build;

        public static void Set(string familyId, WeaponBuild build)
        {
            FamilyId = familyId;
            Build = build;
        }

        public static void Clear() => FamilyId = null;
    }
}
