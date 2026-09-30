using System.Linq;
using NUnit.Framework;

namespace Polykov.Weapons.Tests
{
    public class WeaponFamilyTests
    {
        [Test]
        public void Registry_FindsBothFamilies()
        {
            Assert.AreEqual("M1911A1", WeaponFamilies.ById("m1911").DisplayName);
            Assert.AreEqual("AK-74N", WeaponFamilies.ById("ak74n").DisplayName);
            Assert.IsNull(WeaponFamilies.ById("nope"));
            Assert.IsNull(WeaponFamilies.ById(null));
        }

        [Test]
        public void M1911Family_KeepsTheFourSlotsAndBaseline()
        {
            WeaponFamily f = WeaponFamilies.M1911();
            CollectionAssert.AreEqual(new[] { AttachmentSlot.Muzzle, AttachmentSlot.Barrel, AttachmentSlot.Grips, AttachmentSlot.Magazine }, f.Slots);
            Assert.AreEqual(60f, f.Baseline.Ergonomics);
            Assert.IsTrue(LoadoutRules.Validate(f.FactoryBuild, f.Catalog).IsValid);
        }

        [Test]
        public void AkFactoryBuild_Validates_AndMatchesBaseline()
        {
            WeaponFamily f = WeaponFamilies.AK74N();
            Assert.IsTrue(LoadoutRules.Validate(f.FactoryBuild, f.Catalog).IsValid);
            Assert.AreEqual(7, f.Slots.Length);
            foreach (AttachmentSlot slot in f.Slots) Assert.IsNotNull(f.FactoryBuild.Get(slot), slot.ToString());
            EffectiveWeaponStats e = LoadoutRules.EffectiveStats(f.BaseStats, f.FactoryBuild, f.Catalog, f.Baseline);
            Assert.AreEqual(40f, e.Ergonomics, 1e-4f);
            Assert.AreEqual(3.33f, e.WeightKg, 1e-3f);        // 3.3 kg + the 0.03 kg muzzle brake
            Assert.AreEqual(0.943f, e.LengthM, 1e-4f);
            Assert.AreEqual(FireMode.Auto, f.BaseStats.FireMode);
        }

        [Test]
        public void AkCatalog_EveryPartBelongsToAFamilySlot_AndOnlyMuzzleIsOptional()
        {
            WeaponFamily f = WeaponFamilies.AK74N();
            foreach (AttachmentRules r in f.Catalog.All) Assert.IsTrue(f.HasSlot(r.Slot), r.Id);
            foreach (AttachmentSlot slot in f.Slots)
                Assert.AreEqual(slot == AttachmentSlot.Muzzle, ArmorerModel.IsOptional(slot), slot.ToString());
            Assert.AreEqual(31, f.Catalog.All.Count);
        }

        [Test]
        public void AkSuppressor_AppliesItsModifiers_WithoutRequirements()
        {
            WeaponFamily f = WeaponFamilies.AK74N();
            WeaponBuild b = ArmorerModel.Select(f.FactoryBuild, f.Catalog, AttachmentSlot.Muzzle, "ak_suppressor");
            Assert.AreEqual("ak_suppressor", b.Muzzle);
            Assert.AreEqual(f.FactoryBuild.Barrel, b.Barrel);
            EffectiveWeaponStats e = LoadoutRules.EffectiveStats(f.BaseStats, b, f.Catalog, f.Baseline);
            Assert.AreEqual(30f, e.Ergonomics, 1e-4f);
            Assert.AreEqual(0.25f, e.Loudness, 1e-4f);
            StatRow[] rows = ArmorerModel.Stats(f, b);
            Assert.AreEqual(StatVerdict.Worse, rows.First(r => r.Kind == StatKind.Ergonomics).Verdict);
            Assert.AreEqual(StatVerdict.Better, rows.First(r => r.Kind == StatKind.Loudness).Verdict);
        }

        [Test]
        public void Build_ParsesLegacyFourPartForm()
        {
            Assert.IsTrue(WeaponBuild.TryParse("suppressor_45|barrel_threaded|grips_wood|magazine_7", out WeaponBuild b));
            Assert.AreEqual("suppressor_45", b.Muzzle);
            Assert.AreEqual("magazine_7", b.Magazine);
            Assert.IsNull(b.Handguard);
            Assert.IsNull(b.Stock);
            Assert.IsNull(b.DustCover);
            Assert.IsTrue(WeaponBuild.TryParse("|barrel_standard|grips_wood|magazine_7", out b));
            Assert.AreEqual(WeaponBuild.M1911Default, b);
            Assert.IsFalse(WeaponBuild.TryParse("a|b|c", out _));
            Assert.IsFalse(WeaponBuild.TryParse("a|b|c|d|e", out _));
        }

        [Test]
        public void Build_SevenPartRoundTrip()
        {
            WeaponBuild ak = WeaponBuild.AK74NDefault;
            string text = ak.Serialize();
            Assert.AreEqual(7, text.Split('|').Length);
            Assert.IsTrue(WeaponBuild.TryParse(text, out WeaponBuild back));
            Assert.AreEqual(ak, back);
            Assert.AreEqual(text, back.Serialize());
            Assert.AreEqual(7, WeaponBuild.M1911Default.Serialize().Split('|').Length);
            WeaponBuild edited = ak.With(AttachmentSlot.Stock, "ak_stock_wire").With(AttachmentSlot.Muzzle, null);
            Assert.AreEqual("ak_stock_wire", edited.Stock);
            Assert.IsNull(edited.Muzzle);
            Assert.AreNotEqual(ak, edited);
        }
    }
}
