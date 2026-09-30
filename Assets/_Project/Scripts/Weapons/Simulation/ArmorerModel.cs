using System.Collections.Generic;

namespace Polykov.Weapons
{
    public enum OptionState : byte
    {
        Equipped = 0,
        Available = 1,
        /// <summary>Cannot be mounted (its requirements cannot be satisfied); see the reason.</summary>
        Blocked = 2,
    }

    /// <summary>One entry of the parts list of a slot (an attachment, or "nothing" for optional slots).</summary>
    public struct SlotOption
    {
        /// <summary>Attachment id; null = the empty option.</summary>
        public string Id;
        public OptionState State;
        public string Reason;
        /// <summary>Other parts this choice mounts automatically (e.g. the suppressor's threaded barrel).</summary>
        public IReadOnlyList<string> AlsoMounts;
        /// <summary>Parts that would be removed by this choice (dependents of what it replaces).</summary>
        public IReadOnlyList<string> Removes;
    }

    public enum StatKind : byte { Ergonomics, Recoil, Weight, Length, Loudness, MuzzleFlash }

    public enum StatVerdict : byte { Same, Better, Worse }

    public struct StatRow
    {
        public StatKind Kind;
        public float Factory;
        public float Current;
        public StatVerdict Verdict;
        public float Delta => Current - Factory;
    }

    /// <summary>
    /// What the armorer screen shows and can do, as pure logic on top of <see cref="LoadoutRules"/>: the parts list of
    /// each slot with states and consequences, the choice itself, and the stat table with factory-vs-current verdicts.
    /// </summary>
    public static class ArmorerModel
    {
        /// <summary>Slots can be left empty only when they are optional (a muzzle device); the rest are required.</summary>
        public static bool IsOptional(AttachmentSlot slot) => slot == AttachmentSlot.Muzzle;

        public static List<SlotOption> Options(WeaponBuild build, AttachmentCatalog catalog, AttachmentSlot slot)
        {
            var list = new List<SlotOption>();
            if (IsOptional(slot))
            {
                list.Add(new SlotOption
                {
                    Id = null,
                    State = build.Get(slot) == null ? OptionState.Equipped : OptionState.Available,
                    Removes = Consequences(build, catalog, slot, null),
                    AlsoMounts = System.Array.Empty<string>(),
                });
            }

            foreach (AttachmentRules rules in catalog.All)
            {
                if (rules.Slot != slot) continue;
                WeaponBuild after = LoadoutRules.Equip(build, rules.Id, catalog);
                // Equip resolves dependencies but tolerates requirements missing from the catalog: those builds are invalid.
                bool mounts = after.Get(slot) == rules.Id && LoadoutRules.Validate(after, catalog).IsValid;
                list.Add(new SlotOption
                {
                    Id = rules.Id,
                    State = build.Get(slot) == rules.Id ? OptionState.Equipped : mounts ? OptionState.Available : OptionState.Blocked,
                    Reason = mounts ? null : BlockReason(rules, catalog),
                    AlsoMounts = mounts ? AutoMounted(build, after, slot) : System.Array.Empty<string>(),
                    Removes = mounts ? Removed(build, after, slot) : System.Array.Empty<string>(),
                });
            }
            return list;
        }

        /// <summary>Applies a choice. Required slots ignore "nothing"; unknown or blocked choices leave the build unchanged.</summary>
        public static WeaponBuild Select(WeaponBuild build, AttachmentCatalog catalog, AttachmentSlot slot, string id)
        {
            if (string.IsNullOrEmpty(id))
                return IsOptional(slot) ? LoadoutRules.Remove(build, slot, catalog) : build;
            if (!catalog.TryGet(id, out AttachmentRules rules) || rules.Slot != slot) return build;
            WeaponBuild after = LoadoutRules.Equip(build, id, catalog);
            return LoadoutRules.Validate(after, catalog).IsValid ? after : build;
        }

        /// <summary>Parts that disappear if <paramref name="id"/> (null = empty) is chosen for <paramref name="slot"/>.</summary>
        public static IReadOnlyList<string> Consequences(WeaponBuild build, AttachmentCatalog catalog, AttachmentSlot slot, string id)
            => Removed(build, Select(build, catalog, slot, id), slot);

        public static StatRow[] Stats(WeaponFamily family, WeaponBuild current)
            => Stats(family.BaseStats, family.Baseline, family.FactoryBuild, current, family.Catalog);

        public static StatRow[] Stats(WeaponStats baseStats, WeaponBuild factory, WeaponBuild current, AttachmentCatalog catalog)
            => Stats(baseStats, WeaponBaseline.M1911, factory, current, catalog);

        public static StatRow[] Stats(WeaponStats baseStats, WeaponBaseline baseline, WeaponBuild factory, WeaponBuild current, AttachmentCatalog catalog)
        {
            EffectiveWeaponStats f = LoadoutRules.EffectiveStats(baseStats, factory, catalog, baseline);
            EffectiveWeaponStats c = LoadoutRules.EffectiveStats(baseStats, current, catalog, baseline);
            return new[]
            {
                Row(StatKind.Ergonomics, f.Ergonomics, c.Ergonomics, higherIsBetter: true),
                Row(StatKind.Recoil, f.Stats.VerticalRecoil, c.Stats.VerticalRecoil, higherIsBetter: false),
                Row(StatKind.Weight, f.WeightKg, c.WeightKg, higherIsBetter: false),
                Row(StatKind.Length, f.LengthM * 100f, c.LengthM * 100f, higherIsBetter: false),
                Row(StatKind.Loudness, f.Loudness * 100f, c.Loudness * 100f, higherIsBetter: false),
                Row(StatKind.MuzzleFlash, f.MuzzleFlash * 100f, c.MuzzleFlash * 100f, higherIsBetter: false),
            };
        }

        private static StatRow Row(StatKind kind, float factory, float current, bool higherIsBetter)
        {
            float delta = current - factory;
            StatVerdict verdict = System.Math.Abs(delta) < 1e-4f ? StatVerdict.Same
                : (delta > 0f) == higherIsBetter ? StatVerdict.Better : StatVerdict.Worse;
            return new StatRow { Kind = kind, Factory = factory, Current = current, Verdict = verdict };
        }

        private static string BlockReason(AttachmentRules rules, AttachmentCatalog catalog)
        {
            if (rules.Requires != null)
                foreach (SlotRequirement req in rules.Requires)
                    if (!catalog.Contains(req.AttachmentId)) return $"Requiere {req.AttachmentId}, que no está disponible.";
            return "Incompatible con la configuración actual.";
        }

        private static List<string> AutoMounted(WeaponBuild before, WeaponBuild after, AttachmentSlot own)
        {
            var list = new List<string>();
            for (int i = 0; i < WeaponBuild.SlotCount; i++)
            {
                var s = (AttachmentSlot)i;
                if (s == own) continue;
                string id = after.Get(s);
                if (id != null && id != before.Get(s)) list.Add(id);
            }
            return list;
        }

        private static List<string> Removed(WeaponBuild before, WeaponBuild after, AttachmentSlot own)
        {
            var list = new List<string>();
            for (int i = 0; i < WeaponBuild.SlotCount; i++)
            {
                var s = (AttachmentSlot)i;
                if (s == own) continue;
                string id = before.Get(s);
                if (id != null && after.Get(s) == null) list.Add(id);
            }
            return list;
        }
    }
}
