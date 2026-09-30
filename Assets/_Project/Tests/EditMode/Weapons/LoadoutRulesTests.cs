using NUnit.Framework;

namespace Polykov.Weapons.Tests
{
    public class LoadoutRulesTests
    {
        private static AttachmentCatalog Catalog => AttachmentCatalog.M1911();
        private static WeaponBuild Default => WeaponBuild.M1911Default;

        [Test]
        public void DefaultBuild_IsValid()
        {
            var v = LoadoutRules.Validate(Default, Catalog);
            Assert.IsTrue(v.IsValid, string.Join("; ", v.Reasons));
            Assert.IsNull(Default.Muzzle);
        }

        [Test]
        public void Suppressor_WithoutThreadedBarrel_IsInvalidWithReason()
        {
            var b = Default.With(AttachmentSlot.Muzzle, "suppressor_45");
            var v = LoadoutRules.Validate(b, Catalog);
            Assert.IsFalse(v.IsValid);
            Assert.AreEqual(1, v.Reasons.Count);
            StringAssert.Contains("barrel_threaded", v.Reasons[0]);
        }

        [Test]
        public void EquipSuppressor_ForcesThreadedBarrel()
        {
            var b = LoadoutRules.Equip(Default, "suppressor_45", Catalog);
            Assert.AreEqual("suppressor_45", b.Muzzle);
            Assert.AreEqual("barrel_threaded", b.Barrel);
            Assert.IsTrue(LoadoutRules.Validate(b, Catalog).IsValid);
        }

        [Test]
        public void SwapToStandardBarrel_RemovesSuppressor()
        {
            var b = LoadoutRules.Equip(Default, "suppressor_45", Catalog);
            b = LoadoutRules.Equip(b, "barrel_standard", Catalog);
            Assert.IsNull(b.Muzzle);
            Assert.AreEqual("barrel_standard", b.Barrel);
            Assert.IsTrue(LoadoutRules.Validate(b, Catalog).IsValid);
        }

        [Test]
        public void RemovingBarrel_RemovesSuppressor()
        {
            var b = LoadoutRules.Equip(Default, "suppressor_45", Catalog);
            b = LoadoutRules.Remove(b, AttachmentSlot.Barrel, Catalog);
            Assert.IsNull(b.Muzzle);
            Assert.IsNull(b.Barrel);
        }

        [Test]
        public void EffectiveStats_WithSuppressor()
        {
            var baseStats = WeaponStats.M1911;
            var plain = LoadoutRules.EffectiveStats(baseStats, Default, Catalog);
            var sup = LoadoutRules.EffectiveStats(baseStats, LoadoutRules.Equip(Default, "suppressor_45", Catalog), Catalog);
            Assert.AreEqual(baseStats.VerticalRecoil, plain.Stats.VerticalRecoil, 1e-5f);
            Assert.AreEqual(1f, plain.Loudness, 1e-5f);
            Assert.Less(sup.Stats.VerticalRecoil, plain.Stats.VerticalRecoil);
            Assert.Less(sup.Stats.HorizontalRecoil, plain.Stats.HorizontalRecoil);
            Assert.Greater(sup.WeightKg, plain.WeightKg);
            Assert.Greater(sup.LengthM, plain.LengthM);
            Assert.Less(sup.Loudness, plain.Loudness);
            Assert.Less(sup.MuzzleFlash, plain.MuzzleFlash);
            Assert.Less(sup.Ergonomics, plain.Ergonomics);
        }

        [Test]
        public void IncompatibleSlot_IsRejected()
        {
            var wrong = Default.With(AttachmentSlot.Grips, "barrel_threaded");
            Assert.IsFalse(LoadoutRules.Validate(wrong, Catalog).IsValid);
            // Equip always uses the part's own slot, so it never lands in the wrong one.
            var b = LoadoutRules.Equip(Default, "barrel_threaded", Catalog);
            Assert.AreEqual("barrel_threaded", b.Barrel);
            Assert.AreEqual("grips_wood", b.Grips);
        }

        [Test]
        public void BuildString_RoundTrips()
        {
            var b = LoadoutRules.Equip(Default, "suppressor_45", Catalog);
            Assert.IsTrue(WeaponBuild.TryParse(b.Serialize(), out var back));
            Assert.AreEqual(b, back);
            Assert.IsTrue(WeaponBuild.TryParse(Default.Serialize(), out var d));
            Assert.AreEqual(Default, d);
            Assert.IsNull(d.Muzzle);
        }

        [Test]
        public void UnknownIdsAndBadStrings_AreHandled()
        {
            Assert.AreEqual(Default, LoadoutRules.Equip(Default, "nope", Catalog));
            Assert.AreEqual(Default, LoadoutRules.Equip(Default, null, Catalog));
            var b = Default.With(AttachmentSlot.Muzzle, "ghost");
            var v = LoadoutRules.Validate(b, Catalog);
            Assert.IsFalse(v.IsValid);
            StringAssert.Contains("ghost", v.Reasons[0]);
            var fx = LoadoutRules.EffectiveStats(WeaponStats.M1911, b, Catalog);
            Assert.AreEqual(1f, fx.Loudness, 1e-5f);

            Assert.IsFalse(WeaponBuild.TryParse(null, out _));
            Assert.IsFalse(WeaponBuild.TryParse("garbage", out _));
            Assert.AreEqual(Default, WeaponBuild.ParseOr("a|b", Default));
        }
    }
}
