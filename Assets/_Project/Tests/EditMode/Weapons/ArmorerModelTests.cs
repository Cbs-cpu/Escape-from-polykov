using System.Linq;
using NUnit.Framework;

namespace Polykov.Weapons.Tests
{
    public class ArmorerModelTests
    {
        private static AttachmentCatalog Catalog => AttachmentCatalog.M1911();
        private static WeaponBuild Factory => WeaponBuild.M1911Default;
        private static readonly WeaponStats Base = WeaponStats.M1911;

        [Test]
        public void MuzzleSlot_OffersNothingPlusTheSuppressor_AndBarrelOffersBothBarrels()
        {
            var muzzle = ArmorerModel.Options(Factory, Catalog, AttachmentSlot.Muzzle);
            Assert.AreEqual(2, muzzle.Count);
            Assert.IsNull(muzzle[0].Id);
            Assert.AreEqual(OptionState.Equipped, muzzle[0].State, "nothing is mounted at the factory");
            Assert.AreEqual("suppressor_45", muzzle[1].Id);
            Assert.AreEqual(OptionState.Available, muzzle[1].State);

            var barrel = ArmorerModel.Options(Factory, Catalog, AttachmentSlot.Barrel);
            Assert.AreEqual(new[] { "barrel_standard", "barrel_threaded" }, barrel.Select(o => o.Id).OrderBy(x => x).ToArray());
            Assert.AreEqual(OptionState.Equipped, barrel.First(o => o.Id == "barrel_standard").State);
        }

        [Test]
        public void RequiredSlots_HaveNoEmptyOption_AndCannotBeEmptied()
        {
            Assert.IsFalse(ArmorerModel.Options(Factory, Catalog, AttachmentSlot.Grips).Any(o => o.Id == null));
            Assert.AreEqual(Factory, ArmorerModel.Select(Factory, Catalog, AttachmentSlot.Grips, null));
            Assert.AreEqual(Factory, ArmorerModel.Select(Factory, Catalog, AttachmentSlot.Magazine, ""));
        }

        [Test]
        public void ChoosingTheSuppressor_AlsoMountsTheThreadedBarrel_AndSaysSo()
        {
            var suppressor = ArmorerModel.Options(Factory, Catalog, AttachmentSlot.Muzzle).First(o => o.Id == "suppressor_45");
            CollectionAssert.AreEqual(new[] { "barrel_threaded" }, suppressor.AlsoMounts.ToArray());

            WeaponBuild built = ArmorerModel.Select(Factory, Catalog, AttachmentSlot.Muzzle, "suppressor_45");
            Assert.AreEqual("suppressor_45", built.Muzzle);
            Assert.AreEqual("barrel_threaded", built.Barrel);
        }

        [Test]
        public void SwitchingBackToTheStandardBarrel_RemovesTheSuppressor_AndWarns()
        {
            WeaponBuild suppressed = ArmorerModel.Select(Factory, Catalog, AttachmentSlot.Muzzle, "suppressor_45");
            var standard = ArmorerModel.Options(suppressed, Catalog, AttachmentSlot.Barrel).First(o => o.Id == "barrel_standard");
            CollectionAssert.AreEqual(new[] { "suppressor_45" }, standard.Removes.ToArray());

            WeaponBuild after = ArmorerModel.Select(suppressed, Catalog, AttachmentSlot.Barrel, "barrel_standard");
            Assert.IsNull(after.Muzzle);
            Assert.AreEqual("barrel_standard", after.Barrel);
        }

        [Test]
        public void EmptyingTheMuzzle_KeepsTheThreadedBarrel()
        {
            WeaponBuild suppressed = ArmorerModel.Select(Factory, Catalog, AttachmentSlot.Muzzle, "suppressor_45");
            WeaponBuild after = ArmorerModel.Select(suppressed, Catalog, AttachmentSlot.Muzzle, null);
            Assert.IsNull(after.Muzzle);
            Assert.AreEqual("barrel_threaded", after.Barrel);
        }

        [Test]
        public void AttachmentInTheWrongSlot_OrUnknown_IsRejected()
        {
            Assert.AreEqual(Factory, ArmorerModel.Select(Factory, Catalog, AttachmentSlot.Grips, "suppressor_45"));
            Assert.AreEqual(Factory, ArmorerModel.Select(Factory, Catalog, AttachmentSlot.Muzzle, "laser_pointer"));
        }

        [Test]
        public void PartWithAnUnavailableRequirement_IsBlocked_WithAReason()
        {
            var suppressor = AttachmentRules.Neutral("ghost_suppressor", AttachmentSlot.Muzzle);
            suppressor.Requires = new[] { new SlotRequirement(AttachmentSlot.Barrel, "barrel_missing") };
            var catalog = new AttachmentCatalog(new[] { AttachmentRules.Neutral("barrel_standard", AttachmentSlot.Barrel), suppressor });
            var options = ArmorerModel.Options(new WeaponBuild { Barrel = "barrel_standard" }, catalog, AttachmentSlot.Muzzle);
            SlotOption ghost = options.First(o => o.Id == "ghost_suppressor");
            Assert.AreEqual(OptionState.Blocked, ghost.State);
            StringAssert.Contains("barrel_missing", ghost.Reason);
        }

        [Test]
        public void Stats_AtTheFactoryBuild_AreAllUnchanged()
        {
            Assert.IsTrue(ArmorerModel.Stats(Base, Factory, Factory, Catalog).All(r => r.Verdict == StatVerdict.Same));
        }

        [Test]
        public void Suppressor_LowersRecoilAndNoise_ButCostsErgonomicsWeightAndLength()
        {
            WeaponBuild suppressed = ArmorerModel.Select(Factory, Catalog, AttachmentSlot.Muzzle, "suppressor_45");
            StatRow[] rows = ArmorerModel.Stats(Base, Factory, suppressed, Catalog);
            StatVerdict Of(StatKind k) => rows.First(r => r.Kind == k).Verdict;
            Assert.AreEqual(StatVerdict.Better, Of(StatKind.Recoil));
            Assert.AreEqual(StatVerdict.Better, Of(StatKind.Loudness));
            Assert.AreEqual(StatVerdict.Better, Of(StatKind.MuzzleFlash));
            Assert.AreEqual(StatVerdict.Worse, Of(StatKind.Ergonomics));
            Assert.AreEqual(StatVerdict.Worse, Of(StatKind.Weight));
            Assert.AreEqual(StatVerdict.Worse, Of(StatKind.Length));
        }

        [Test]
        public void StatDeltas_MatchTheModifiers()
        {
            WeaponBuild suppressed = ArmorerModel.Select(Factory, Catalog, AttachmentSlot.Muzzle, "suppressor_45");
            StatRow weight = ArmorerModel.Stats(Base, Factory, suppressed, Catalog).First(r => r.Kind == StatKind.Weight);
            Assert.AreEqual(0.35f + 0.02f, weight.Delta, 1e-4f, "suppressor plus the threaded barrel");
        }
    }
}
