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
    }
}
