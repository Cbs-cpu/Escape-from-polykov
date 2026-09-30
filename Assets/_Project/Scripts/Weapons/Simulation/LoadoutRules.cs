using System.Collections.Generic;

namespace Polykov.Weapons
{
    /// <summary>Base handling numbers that attachments modify (WeaponStats has no ergonomics/weight/length).</summary>
    public struct WeaponBaseline
    {
        public float Ergonomics;
        public float WeightKg;
        public float LengthM;

        public static WeaponBaseline M1911 => new WeaponBaseline { Ergonomics = 60f, WeightKg = 1.1f, LengthM = 0.216f };
        public static WeaponBaseline AK74N => new WeaponBaseline { Ergonomics = 40f, WeightKg = 3.3f, LengthM = 0.943f };
    }

    /// <summary>WeaponStats with the build's modifiers applied, plus the derived handling and audiovisual numbers.</summary>
    public struct EffectiveWeaponStats
    {
        public WeaponStats Stats;
        public float Ergonomics;
        public float WeightKg;
        public float LengthM;
        /// <summary>1 = unsuppressed gunshot.</summary>
        public float Loudness;
        /// <summary>1 = unsuppressed muzzle flash.</summary>
        public float MuzzleFlash;
    }

    public struct LoadoutValidation
    {
        public bool IsValid;
        public IReadOnlyList<string> Reasons;
    }

    /// <summary>Pure attachment rules: compatibility, dependency resolution and stat modifiers.</summary>
    public static class LoadoutRules
    {
        public static LoadoutValidation Validate(WeaponBuild build, AttachmentCatalog catalog)
        {
            var reasons = new List<string>();
            for (int i = 0; i < WeaponBuild.SlotCount; i++)
            {
                var slot = (AttachmentSlot)i;
                var id = build.Get(slot);
                if (id == null) continue;
                if (catalog == null || !catalog.TryGet(id, out var rules))
                {
                    reasons.Add($"Unknown attachment '{id}' in {slot}.");
                    continue;
                }
                if (rules.Slot != slot)
                    reasons.Add($"'{id}' does not fit the {slot} slot (it is a {rules.Slot} part).");
                foreach (var req in Requirements(rules))
                    if (build.Get(req.Slot) != req.AttachmentId)
                        reasons.Add($"'{id}' requires '{req.AttachmentId}' in {req.Slot}.");
            }
            return new LoadoutValidation { IsValid = reasons.Count == 0, Reasons = reasons };
        }

        /// <summary>
        /// Equips an attachment in its own slot, equipping the parts it requires and removing parts that lose their requirements.
        /// Unknown ids or unsatisfiable requirements return the build unchanged.
        /// </summary>
        public static WeaponBuild Equip(WeaponBuild build, string attachmentId, AttachmentCatalog catalog)
        {
            if (catalog == null || !catalog.TryGet(attachmentId, out var rules)) return build;

            var result = build.With(rules.Slot, attachmentId);
            foreach (var req in Requirements(rules))
            {
                if (req.Slot == rules.Slot) return build;
                result = result.With(req.Slot, req.AttachmentId);
            }
            result = Prune(result, catalog);
            return result.Get(rules.Slot) == attachmentId ? result : build;
        }

        /// <summary>Empties a slot; parts that depended on it are removed too.</summary>
        public static WeaponBuild Remove(WeaponBuild build, AttachmentSlot slot, AttachmentCatalog catalog)
            => Prune(build.With(slot, null), catalog);

        public static EffectiveWeaponStats EffectiveStats(WeaponStats baseStats, WeaponBuild build, AttachmentCatalog catalog)
            => EffectiveStats(baseStats, build, catalog, WeaponBaseline.M1911);

        public static EffectiveWeaponStats EffectiveStats(WeaponStats baseStats, WeaponBuild build, AttachmentCatalog catalog, WeaponBaseline baseline)
        {
            float recoil = 1f, loud = 1f, flash = 1f;
            var e = new EffectiveWeaponStats
            {
                Ergonomics = baseline.Ergonomics,
                WeightKg = baseline.WeightKg,
                LengthM = baseline.LengthM,
            };
            for (int i = 0; i < WeaponBuild.SlotCount; i++)
            {
                var id = build.Get((AttachmentSlot)i);
                if (id == null || catalog == null || !catalog.TryGet(id, out var r)) continue;
                e.Ergonomics += r.ErgonomicsAdd;
                e.WeightKg += r.WeightAddKg;
                e.LengthM += r.LengthAddM;
                recoil *= r.Recoil;
                loud *= r.Loudness;
                flash *= r.MuzzleFlash;
            }
            baseStats.VerticalRecoil *= recoil;
            baseStats.HorizontalRecoil *= recoil;
            e.Stats = baseStats;
            e.Loudness = loud;
            e.MuzzleFlash = flash;
            return e;
        }

        private static SlotRequirement[] Requirements(AttachmentRules rules)
            => rules.Requires ?? System.Array.Empty<SlotRequirement>();

        /// <summary>Removes parts whose requirements are unmet, until stable.</summary>
        private static WeaponBuild Prune(WeaponBuild build, AttachmentCatalog catalog)
        {
            bool changed = true;
            while (changed)
            {
                changed = false;
                for (int i = 0; i < WeaponBuild.SlotCount; i++)
                {
                    var slot = (AttachmentSlot)i;
                    var id = build.Get(slot);
                    if (id == null || !catalog.TryGet(id, out var r)) continue;
                    foreach (var req in Requirements(r))
                    {
                        if (build.Get(req.Slot) == req.AttachmentId) continue;
                        build = build.With(slot, null);
                        changed = true;
                        break;
                    }
                }
            }
            return build;
        }
    }
}
