using UnityEngine;

namespace Polykov.Weapons
{
    /// <summary>
    /// Saves the player's weapon build (attachments per slot) in PlayerPrefs, one key per weapon definition.
    /// The lobby armorer writes it; <see cref="PlayerWeapon"/> reads it when the match starts.
    /// </summary>
    public static class WeaponBuildStore
    {
        public static string KeyFor(WeaponDefinition definition)
        {
            string n = definition.name;
            if (n.StartsWith("WD_")) n = n.Substring(3);
            return "weaponBuild." + n.ToLowerInvariant();
        }

        public static WeaponBuild Load(WeaponDefinition definition)
            => WeaponBuild.ParseOr(PlayerPrefs.GetString(KeyFor(definition), null), definition.DefaultBuild);

        public static void Save(WeaponDefinition definition, WeaponBuild build)
        {
            PlayerPrefs.SetString(KeyFor(definition), build.Serialize());
            PlayerPrefs.Save();
        }
    }
}
