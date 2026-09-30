using System;

namespace Polykov.Weapons
{
    /// <summary>
    /// Which attachment (by id) sits in each slot. Empty/null = nothing. Value semantics: every change returns a new build.
    /// Compact string form (PlayerPrefs / network): the seven ids joined by '|' in <see cref="AttachmentSlot"/> order.
    /// The legacy four-id form (Muzzle|Barrel|Grips|Magazine) still parses; the missing slots are empty.
    /// </summary>
    [Serializable]
    public struct WeaponBuild : IEquatable<WeaponBuild>
    {
        public const int SlotCount = 7;
        private const int LegacySlotCount = 4;
        private const char Separator = '|';

        public string Muzzle;
        public string Barrel;
        public string Grips;
        public string Magazine;
        public string Handguard;
        public string Stock;
        public string DustCover;

        /// <summary>Factory M1911: standard barrel, wood grips, 7-round magazine, no muzzle device.</summary>
        public static WeaponBuild M1911Default => new WeaponBuild
        {
            Barrel = "barrel_standard",
            Grips = "grips_wood",
            Magazine = "magazine_7",
        };

        /// <summary>Factory AK-74N: muzzle brake, standard barrel, polymer furniture, standard dust cover, 30-round magazine.</summary>
        public static WeaponBuild AK74NDefault => new WeaponBuild
        {
            Muzzle = "ak_muzzle_brake",
            Barrel = "ak_barrel_standard",
            Handguard = "ak_hg_polymer",
            DustCover = "ak_cover_standard",
            Grips = "ak_grip_polymer",
            Stock = "ak_stock_polymer",
            Magazine = "ak_mag_30",
        };

        public string Get(AttachmentSlot slot)
        {
            string id;
            switch (slot)
            {
                case AttachmentSlot.Muzzle: id = Muzzle; break;
                case AttachmentSlot.Barrel: id = Barrel; break;
                case AttachmentSlot.Grips: id = Grips; break;
                case AttachmentSlot.Magazine: id = Magazine; break;
                case AttachmentSlot.Handguard: id = Handguard; break;
                case AttachmentSlot.Stock: id = Stock; break;
                case AttachmentSlot.DustCover: id = DustCover; break;
                default: id = null; break;
            }
            return string.IsNullOrEmpty(id) ? null : id;
        }

        /// <summary>Copy with <paramref name="slot"/> set to <paramref name="id"/> (null/empty clears it).</summary>
        public WeaponBuild With(AttachmentSlot slot, string id)
        {
            var b = this;
            if (string.IsNullOrEmpty(id)) id = null;
            switch (slot)
            {
                case AttachmentSlot.Muzzle: b.Muzzle = id; break;
                case AttachmentSlot.Barrel: b.Barrel = id; break;
                case AttachmentSlot.Grips: b.Grips = id; break;
                case AttachmentSlot.Magazine: b.Magazine = id; break;
                case AttachmentSlot.Handguard: b.Handguard = id; break;
                case AttachmentSlot.Stock: b.Stock = id; break;
                case AttachmentSlot.DustCover: b.DustCover = id; break;
            }
            return b;
        }

        public bool Has(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            for (int i = 0; i < SlotCount; i++)
                if (Get((AttachmentSlot)i) == id) return true;
            return false;
        }

        public string Serialize()
        {
            var parts = new string[SlotCount];
            for (int i = 0; i < SlotCount; i++) parts[i] = Get((AttachmentSlot)i) ?? string.Empty;
            return string.Join(Separator.ToString(), parts);
        }

        /// <summary>Parses <see cref="Serialize"/> output. Null, empty or malformed input yields false and an empty build.</summary>
        public static bool TryParse(string text, out WeaponBuild build)
        {
            build = default;
            if (string.IsNullOrEmpty(text)) return false;
            var parts = text.Split(Separator);
            if (parts.Length != SlotCount && parts.Length != LegacySlotCount) return false;
            for (int i = 0; i < parts.Length; i++) build = build.With((AttachmentSlot)i, parts[i].Trim());
            return true;
        }

        /// <summary>Parses, falling back to <paramref name="fallback"/> when the text is unusable.</summary>
        public static WeaponBuild ParseOr(string text, WeaponBuild fallback)
            => TryParse(text, out var b) ? b : fallback;

        public bool Equals(WeaponBuild o)
        {
            for (int i = 0; i < SlotCount; i++)
                if (Get((AttachmentSlot)i) != o.Get((AttachmentSlot)i)) return false;
            return true;
        }

        public override bool Equals(object obj) => obj is WeaponBuild o && Equals(o);
        public override int GetHashCode() => Serialize().GetHashCode();
        public override string ToString() => Serialize();
        public static bool operator ==(WeaponBuild a, WeaponBuild b) => a.Equals(b);
        public static bool operator !=(WeaponBuild a, WeaponBuild b) => !a.Equals(b);
    }
}
