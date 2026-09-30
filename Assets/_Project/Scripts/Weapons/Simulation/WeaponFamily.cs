using System.Collections.Generic;

namespace Polykov.Weapons
{
    /// <summary>
    /// Pure data of one weapon type for modding and inventory: its attachment set, factory build, handling baseline,
    /// gameplay stats and the modding slots it has (in the order the armorer lists them).
    /// </summary>
    public sealed class WeaponFamily
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly AttachmentCatalog Catalog;
        public readonly WeaponBuild FactoryBuild;
        public readonly WeaponBaseline Baseline;
        public readonly WeaponStats BaseStats;
        public readonly AttachmentSlot[] Slots;

        public WeaponFamily(string id, string displayName, AttachmentCatalog catalog, WeaponBuild factoryBuild,
            WeaponBaseline baseline, WeaponStats baseStats, AttachmentSlot[] slots)
        {
            Id = id;
            DisplayName = displayName;
            Catalog = catalog;
            FactoryBuild = factoryBuild;
            Baseline = baseline;
            BaseStats = baseStats;
            Slots = slots;
        }

        public bool HasSlot(AttachmentSlot slot)
        {
            for (int i = 0; i < Slots.Length; i++)
                if (Slots[i] == slot) return true;
            return false;
        }
    }

    /// <summary>The weapon families of the game, by id ("m1911", "ak74n").</summary>
    public static class WeaponFamilies
    {
        public const string M1911Id = "m1911";
        public const string AK74NId = "ak74n";

        private static readonly Dictionary<string, WeaponFamily> Registry = new Dictionary<string, WeaponFamily>();

        static WeaponFamilies()
        {
            Register(M1911());
            Register(AK74N());
        }

        /// <summary>Adds or replaces a family (hosts register the M1911 built from their own assets this way).</summary>
        public static void Register(WeaponFamily family)
        {
            if (family != null && !string.IsNullOrEmpty(family.Id)) Registry[family.Id] = family;
        }

        /// <summary>The family with this id, or null.</summary>
        public static WeaponFamily ById(string id) => id != null && Registry.TryGetValue(id, out WeaponFamily f) ? f : null;

        public static IEnumerable<WeaponFamily> All => Registry.Values;

        public static WeaponFamily M1911() => new WeaponFamily(M1911Id, "M1911A1", AttachmentCatalog.M1911(), WeaponBuild.M1911Default,
            WeaponBaseline.M1911, WeaponStats.M1911,
            new[] { AttachmentSlot.Muzzle, AttachmentSlot.Barrel, AttachmentSlot.Grips, AttachmentSlot.Magazine });

        public static WeaponFamily AK74N() => new WeaponFamily(AK74NId, "AK-74N", AttachmentCatalog.AK74N(), WeaponBuild.AK74NDefault,
            WeaponBaseline.AK74N, WeaponStats.AK74N,
            new[]
            {
                AttachmentSlot.Muzzle, AttachmentSlot.Barrel, AttachmentSlot.Handguard, AttachmentSlot.DustCover,
                AttachmentSlot.Grips, AttachmentSlot.Stock, AttachmentSlot.Magazine,
            });
    }
}
