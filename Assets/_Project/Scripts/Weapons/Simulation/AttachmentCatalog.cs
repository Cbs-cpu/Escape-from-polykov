using System.Collections.Generic;

namespace Polykov.Weapons
{
    /// <summary>Lookup of the attachments available to a weapon, by id.</summary>
    public sealed class AttachmentCatalog
    {
        private readonly Dictionary<string, AttachmentRules> _byId = new Dictionary<string, AttachmentRules>();
        private readonly List<AttachmentRules> _all = new List<AttachmentRules>();

        public AttachmentCatalog(IEnumerable<AttachmentRules> rules = null)
        {
            if (rules == null) return;
            foreach (var r in rules) Add(r);
        }

        public IReadOnlyList<AttachmentRules> All => _all;

        /// <summary>Adds (or replaces) an attachment. Entries without an id are ignored.</summary>
        public void Add(AttachmentRules rules)
        {
            if (string.IsNullOrEmpty(rules.Id)) return;
            if (_byId.ContainsKey(rules.Id)) _all.RemoveAll(r => r.Id == rules.Id);
            _byId[rules.Id] = rules;
            _all.Add(rules);
        }

        public bool TryGet(string id, out AttachmentRules rules)
        {
            if (id != null && _byId.TryGetValue(id, out rules)) return true;
            rules = default;
            return false;
        }

        public bool Contains(string id) => id != null && _byId.ContainsKey(id);

        /// <summary>The M1911 attachment set (same numbers as the shipped assets).</summary>
        public static AttachmentCatalog M1911()
        {
            var std = AttachmentRules.Neutral("barrel_standard", AttachmentSlot.Barrel);
            var threaded = AttachmentRules.Neutral("barrel_threaded", AttachmentSlot.Barrel);
            threaded.WeightAddKg = 0.02f;
            threaded.LengthAddM = 0.01f;

            var suppressor = AttachmentRules.Neutral("suppressor_45", AttachmentSlot.Muzzle);
            suppressor.Requires = new[] { new SlotRequirement(AttachmentSlot.Barrel, "barrel_threaded") };
            suppressor.ErgonomicsAdd = -8f;
            suppressor.RecoilMultiplier = 0.9f;
            suppressor.WeightAddKg = 0.35f;
            suppressor.LengthAddM = 0.16f;
            suppressor.LoudnessMultiplier = 0.25f;
            suppressor.MuzzleFlashMultiplier = 0.15f;

            var wood = AttachmentRules.Neutral("grips_wood", AttachmentSlot.Grips);
            var mag = AttachmentRules.Neutral("magazine_7", AttachmentSlot.Magazine);
            return new AttachmentCatalog(new[] { std, threaded, suppressor, wood, mag });
        }

        /// <summary>The AK-74N attachment set (ids shared with the art). No cross-slot requirements: every AK barrel is threaded.</summary>
        public static AttachmentCatalog AK74N()
        {
            var c = new AttachmentCatalog();
            // Muzzle
            c.Add(Ak("ak_muzzle_brake", AttachmentSlot.Muzzle, 0f, 1f, 0.03f, 0f, 1f, 1f));
            c.Add(Ak("ak_flash_hider", AttachmentSlot.Muzzle, 0f, 1.03f, 0.05f, 0.01f, 1f, 0.3f));
            c.Add(Ak("ak_compensator", AttachmentSlot.Muzzle, -2f, 0.88f, 0.1f, 0.02f, 1.15f, 1.2f));
            c.Add(Ak("ak_thread_protector", AttachmentSlot.Muzzle, 1f, 1.12f, 0.01f, -0.03f, 1.05f, 1.4f));
            c.Add(Ak("ak_suppressor", AttachmentSlot.Muzzle, -10f, 0.88f, 0.5f, 0.2f, 0.25f, 0.1f));
            // Barrel
            c.Add(Ak("ak_barrel_standard", AttachmentSlot.Barrel));
            c.Add(Ak("ak_barrel_long", AttachmentSlot.Barrel, -6f, 0.95f, 0.4f, 0.105f));
            c.Add(Ak("ak_barrel_threaded", AttachmentSlot.Barrel, 4f, 1.08f, -0.25f, -0.1f, 1.1f, 1.4f));
            c.Add(Ak("ak_barrel_competition", AttachmentSlot.Barrel, -3f, 0.93f, 0.3f, 0f));
            c.Add(Ak("ak_barrel_night", AttachmentSlot.Barrel, 0f, 1f, 0.02f, 0f));
            // Handguard
            c.Add(Ak("ak_hg_polymer", AttachmentSlot.Handguard));
            c.Add(Ak("ak_hg_wood", AttachmentSlot.Handguard, 1f, 1f, 0.05f));
            c.Add(Ak("ak_hg_tactical", AttachmentSlot.Handguard, 5f, 0.97f, 0.2f));
            c.Add(Ak("ak_hg_optic", AttachmentSlot.Handguard, 1f, 1f, 0.1f));
            // Dust cover
            c.Add(Ak("ak_cover_standard", AttachmentSlot.DustCover));
            c.Add(Ak("ak_cover_serrated", AttachmentSlot.DustCover, 0f, 1f, 0.02f));
            c.Add(Ak("ak_cover_light", AttachmentSlot.DustCover, 1f, 1f, -0.1f));
            c.Add(Ak("ak_cover_rail", AttachmentSlot.DustCover, 0f, 1f, 0.12f));
            c.Add(Ak("ak_cover_modern", AttachmentSlot.DustCover, 2f, 1f, 0.05f));
            // Grips
            c.Add(Ak("ak_grip_polymer", AttachmentSlot.Grips));
            c.Add(Ak("ak_grip_wood", AttachmentSlot.Grips, 1f, 1f, 0.03f));
            c.Add(Ak("ak_grip_textured", AttachmentSlot.Grips, 3f));
            c.Add(Ak("ak_grip_ergo", AttachmentSlot.Grips, 6f, 1f, 0.02f));
            // Stock
            c.Add(Ak("ak_stock_polymer", AttachmentSlot.Stock));
            c.Add(Ak("ak_stock_wood", AttachmentSlot.Stock, 0f, 1f, 0.2f));
            c.Add(Ak("ak_stock_wire", AttachmentSlot.Stock, 3f, 1.12f, -0.3f, -0.05f));
            c.Add(Ak("ak_stock_tele", AttachmentSlot.Stock, 4f, 0.92f, 0.15f, 0.02f));
            // Magazine
            c.Add(Ak("ak_mag_30", AttachmentSlot.Magazine));
            c.Add(Ak("ak_mag_45", AttachmentSlot.Magazine, -6f, 1f, 0.25f));
            c.Add(Ak("ak_mag_clear", AttachmentSlot.Magazine, 1f, 1f, -0.05f));
            c.Add(Ak("ak_mag_steel", AttachmentSlot.Magazine, -1f, 1f, 0.1f));
            return c;
        }

        private static AttachmentRules Ak(string id, AttachmentSlot slot, float ergo = 0f, float recoil = 1f, float weightKg = 0f,
            float lengthM = 0f, float loud = 1f, float flash = 1f)
        {
            AttachmentRules r = AttachmentRules.Neutral(id, slot);
            r.ErgonomicsAdd = ergo;
            r.RecoilMultiplier = recoil;
            r.WeightAddKg = weightKg;
            r.LengthAddM = lengthM;
            r.LoudnessMultiplier = loud;
            r.MuzzleFlashMultiplier = flash;
            return r;
        }
    }
}
