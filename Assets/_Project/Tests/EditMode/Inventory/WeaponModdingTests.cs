using System.Linq;
using NUnit.Framework;
using Polykov.Weapons;

namespace Polykov.Inventory.Tests
{
    public class WeaponModdingTests
    {
        private ItemDatabase _db;
        private Profile _p;
        private WeaponFamily _ak, _colt;
        private Item _rifle;

        [SetUp]
        public void SetUp()
        {
            _db = ItemDatabase.Default();
            _p = StarterKit.Create(_db, WeaponBuild.M1911Default, AttachmentCatalog.M1911());
            _ak = WeaponFamilies.AK74N();
            _colt = WeaponFamilies.M1911();
            _rifle = _p.Equipped(EquipSlot.Primary);
        }

        private Item Loose(string id) => WeaponParts.FindLoose(_p, id);

        [Test]
        public void Starter_EquipsFactoryAk_WithMagsInRigAndSpareParts()
        {
            Assert.IsNotNull(_rifle);
            Assert.AreEqual("ak74n", _rifle.Def.Id);
            Assert.AreEqual(_ak.FactoryBuild.Serialize(), _rifle.Build);
            Assert.AreEqual(2, _p.Equipped(EquipSlot.Rig).Grids.SelectMany(g => g.Placements).Count(pl => pl.Item.Def.Id == "ak_mag_30"));
            foreach (AttachmentRules r in _ak.Catalog.All)
            {
                bool factory = _ak.FactoryBuild.Has(r.Id);
                Item spare = _p.AllItems().FirstOrDefault(i => i.Def.AttachmentId == r.Id && _p.InStash(i));
                Assert.AreEqual(!factory, spare != null, r.Id);
            }
        }

        [Test]
        public void Mount_FromGrid_SwapsAndReturnsOldPartToTheSameCell()
        {
            Item wood = Loose("ak_hg_wood");
            GridContainer grid = wood.Location.Grid;
            int x = wood.Location.X, y = wood.Location.Y;
            Assert.IsTrue(WeaponParts.TryMountItem(_p, _rifle, wood, _ak, out string reason), reason);
            Assert.AreEqual("ak_hg_wood", WeaponParts.BuildOf(_rifle, _ak.FactoryBuild).Handguard);
            Assert.IsNull(wood.Location, "the dragged item is consumed");
            Item back = grid.ItemAt(x, y);
            Assert.IsNotNull(back);
            Assert.AreEqual("ak_hg_polymer", back.Def.Id);
            Assert.AreEqual(x, back.Location.X);
            Assert.AreEqual(y, back.Location.Y);
        }

        [Test]
        public void Mount_UsesThatExactItem()
        {
            // Two loose 45-round magazines: mounting the second one must consume the second one.
            Item a = _p.Create("ak_mag_45");
            Item b = _p.Create("ak_mag_45");
            Assert.IsTrue(_p.StoreInStash(a));
            Assert.IsTrue(_p.StoreInStash(b));
            Assert.IsTrue(WeaponParts.TryMountItem(_p, _rifle, b, _ak, out string reason), reason);
            Assert.IsNull(b.Location);
            Assert.IsNotNull(a.Location);
        }

        [Test]
        public void Mount_ReplacedPartGoesToTheStash_WhenItDoesNotFitWhereTheDraggedOneWas()
        {
            // A 2x1 wire stock in a 2x3 bag replaces the 3x1 polymer stock: it fits neither upright nor turned (blocked).
            Item bag = _p.Create("sling");
            Assert.IsTrue(_p.StoreInStash(bag));
            Item stock = _p.Create("ak_stock_wire");
            Assert.AreEqual(MoveResult.Ok, _p.Move(stock, MoveTarget.ToCell(bag.Grids[0], 0, 0, false)));
            Assert.AreEqual(MoveResult.Ok, _p.Move(_p.Create("bandage"), MoveTarget.ToCell(bag.Grids[0], 0, 1, false)));
            Assert.IsTrue(WeaponParts.TryMountItem(_p, _rifle, stock, _ak, out string reason), reason);
            Item old = Loose("ak_stock_polymer");
            Assert.IsNotNull(old);
            Assert.IsTrue(old.Location.Grid == _p.Stash, "back in the stash");
            Assert.AreEqual("ak_stock_wire", WeaponParts.BuildOf(_rifle, _ak.FactoryBuild).Stock);
        }

        [Test]
        public void Mount_SameSizeSwapKeepsTheCell_EvenInsideAContainer()
        {
            Item grip = _p.Create("ak_grip_wood");
            Assert.IsTrue(_p.Store(grip, new[] { _p.Pockets[2] }));
            Assert.IsTrue(WeaponParts.TryMountItem(_p, _rifle, grip, _ak, out string reason), reason);
            Assert.AreEqual("ak_grip_polymer", _p.Pockets[2].ItemAt(0, 0).Def.Id);
        }

        [Test]
        public void Mount_RejectsPartsOfOtherFamilies()
        {
            string before = _rifle.Build;
            Item foreign = Loose("suppressor_45");
            Assert.IsNotNull(foreign);
            Assert.IsFalse(WeaponParts.TryMountItem(_p, _rifle, foreign, _ak, out string reason));
            Assert.AreEqual("No es compatible con AK-74N.", reason);
            Assert.IsFalse(WeaponParts.CanMount(_p, _rifle, foreign, _ak, out _));
            Assert.AreEqual(before, _rifle.Build);
            Assert.IsNotNull(foreign.Location);

            Item akPart = Loose("ak_suppressor");
            Item pistol = _p.Equipped(EquipSlot.Holster);
            Assert.IsFalse(WeaponParts.TryMountItem(_p, pistol, akPart, _colt, out reason));
            StringAssert.Contains("M1911A1", reason);
        }

        [Test]
        public void Mount_RejectsNonPartsAndAlreadyMountedIds()
        {
            Item bandage = _p.Create("bandage");
            Assert.IsTrue(_p.StoreInStash(bandage));
            Assert.IsFalse(WeaponParts.TryMountItem(_p, _rifle, bandage, _ak, out _));
            Item wood = Loose("ak_hg_wood");
            Assert.IsTrue(WeaponParts.TryMountItem(_p, _rifle, wood, _ak, out _));
            Item polymer = Loose("ak_hg_polymer");
            Assert.IsTrue(WeaponParts.TryMountItem(_p, _rifle, polymer, _ak, out _));
            Assert.AreEqual("ak_hg_polymer", WeaponParts.BuildOf(_rifle, _ak.FactoryBuild).Handguard);
            Item another = _p.Create("ak_hg_polymer");
            Assert.IsTrue(_p.StoreInStash(another));
            Assert.IsFalse(WeaponParts.TryMountItem(_p, _rifle, another, _ak, out string reason));
            StringAssert.Contains("montada", reason);
        }

        [Test]
        public void Detach_RequiredSlotIsRefused()
        {
            string before = _rifle.Build;
            int count = _p.AllItems().Count();
            Assert.IsFalse(WeaponParts.TryDetach(_p, _rifle, AttachmentSlot.Barrel, _ak, null, out string reason));
            StringAssert.Contains("no se puede dejar vacía", reason);
            Assert.AreEqual(before, _rifle.Build);
            Assert.AreEqual(count, _p.AllItems().Count());
        }

        [Test]
        public void Detach_SuppressorGoesToTheStash_OrToTheDropCell()
        {
            Item sup = Loose("ak_suppressor");
            Assert.IsTrue(WeaponParts.TryMountItem(_p, _rifle, sup, _ak, out string reason), reason);
            Assert.AreEqual("ak_suppressor", WeaponParts.BuildOf(_rifle, _ak.FactoryBuild).Muzzle);

            Assert.IsTrue(WeaponParts.TryDetach(_p, _rifle, AttachmentSlot.Muzzle, _ak, null, out reason), reason);
            Assert.IsNull(WeaponParts.BuildOf(_rifle, _ak.FactoryBuild).Muzzle);
            Item back = Loose("ak_suppressor");
            Assert.IsNotNull(back);
            Assert.IsTrue(_p.InStash(back));

            // Mount again, then detach onto a chosen free cell of the stash.
            Assert.IsTrue(WeaponParts.TryMountItem(_p, _rifle, back, _ak, out reason), reason);
            Assert.IsTrue(_p.Stash.CanPlace(_db.Get("ak_suppressor"), 6, 35, false));
            var target = MoveTarget.ToCell(_p.Stash, 6, 35, false);
            Assert.IsTrue(WeaponParts.TryDetach(_p, _rifle, AttachmentSlot.Muzzle, _ak, target, out reason), reason);
            Item placed = _p.Stash.ItemAt(6, 35);
            Assert.IsNotNull(placed);
            Assert.AreEqual("ak_suppressor", placed.Def.Id);
            Assert.IsFalse(WeaponParts.TryDetach(_p, _rifle, AttachmentSlot.Muzzle, _ak, null, out _), "nothing left to detach");
        }

        [Test]
        public void Items_EveryCatalogPartHasAnItem_AndEveryPartItemHasACatalogEntry()
        {
            foreach (WeaponFamily f in new[] { _colt, _ak })
                foreach (AttachmentRules r in f.Catalog.All)
                {
                    ItemDef def = _db.ForAttachment(r.Id);
                    Assert.IsNotNull(def, f.Id + ": " + r.Id);
                    Assert.AreEqual(r.Id, def.Id, "item id == attachment id");
                    Assert.IsTrue(def.Category == ItemCategory.WeaponPart || def.Category == ItemCategory.Magazine, r.Id);
                    if (r.Slot == AttachmentSlot.Magazine) Assert.Greater(def.Capacity, 0, r.Id);
                }
            foreach (ItemDef def in _db.All.Where(d => d.AttachmentId != null))
                Assert.IsTrue(_colt.Catalog.Contains(def.AttachmentId) || _ak.Catalog.Contains(def.AttachmentId), def.Id);
            foreach (ItemDef weapon in _db.All.Where(d => d.Category == ItemCategory.Weapon))
                Assert.IsNotNull(WeaponFamilies.ById(weapon.WeaponId), weapon.Id);
        }

        [Test]
        public void SavedAkSurvivesTheProfileRoundTrip()
        {
            Item wood = Loose("ak_hg_wood");
            Assert.IsTrue(WeaponParts.TryMountItem(_p, _rifle, wood, _ak, out _));
            Profile q = ProfileSerializer.Load(ProfileSerializer.Save(_p), _db);
            Assert.AreEqual(_rifle.Build, q.Equipped(EquipSlot.Primary).Build);
            Assert.AreEqual("ak_hg_wood", WeaponParts.BuildOf(q.Equipped(EquipSlot.Primary), _ak.FactoryBuild).Handguard);
        }
    
        [Test]
        public void GrantMissingGivesAnOldProfileTheAkOnce()
        {
            var p = new Profile(_db);
            Assert.IsTrue(StarterKit.GrantMissing(p));
            int aks = 0;
            foreach (Item i in p.AllItems()) if (i.Def.WeaponId == "ak74n") aks++;
            Assert.AreEqual(1, aks);
            Assert.IsNotNull(p.Equipped(EquipSlot.Primary));
            Assert.IsFalse(StarterKit.GrantMissing(p));
        }
    }
}
