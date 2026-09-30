using System.Linq;
using NUnit.Framework;
using Polykov.Weapons;

namespace Polykov.Inventory.Tests
{
    public class InventoryTests
    {
        private ItemDatabase _db;
        private Profile _p;

        [SetUp]
        public void SetUp()
        {
            _db = ItemDatabase.Default();
            _p = new Profile(_db);
        }

        private Item Stash(string id, int x, int y, bool rot = false, int count = 1)
        {
            Item i = _p.Create(id, count);
            Assert.AreEqual(MoveResult.Ok, _p.Move(i, MoveTarget.ToCell(_p.Stash, x, y, rot)));
            return i;
        }

        [Test]
        public void Grid_RejectsOverlapAndOutOfBounds()
        {
            Stash("m1911a1", 0, 0);                    // 2x1
            Item water = _p.Create("water");           // 1x2
            Assert.AreEqual(MoveResult.NoRoom, _p.Check(water, MoveTarget.ToCell(_p.Stash, 1, 0, false)));
            Assert.AreEqual(MoveResult.Ok, _p.Check(water, MoveTarget.ToCell(_p.Stash, 2, 0, false)));
            Assert.AreEqual(MoveResult.NoRoom, _p.Check(water, MoveTarget.ToCell(_p.Stash, 0, Profile.StashHeight - 1, false)));
            Assert.AreEqual(MoveResult.NoRoom, _p.Check(water, MoveTarget.ToCell(_p.Stash, Profile.StashWidth - 1, 0, true)));
        }

        [Test]
        public void Rotation_SwapsFootprint()
        {
            Item water = Stash("water", 0, 0, rot: true);   // now 2x1
            Assert.AreSame(water, _p.Stash.ItemAt(1, 0));
            Assert.IsNull(_p.Stash.ItemAt(0, 1));
        }

        [Test]
        public void MovingWithinGrid_IgnoresItsOwnCells()
        {
            Item gun = Stash("m1911a1", 0, 0);
            Assert.AreEqual(MoveResult.Ok, _p.Move(gun, MoveTarget.ToCell(_p.Stash, 1, 0, false)));
            Assert.AreSame(gun, _p.Stash.ItemAt(2, 0));
            Assert.IsNull(_p.Stash.ItemAt(0, 0));
            Assert.AreEqual(1, _p.Stash.Placements.Count);
        }

        [Test]
        public void Slots_AcceptOnlyMatchingCategory()
        {
            Item gun = Stash("m1911a1", 0, 0);
            Assert.AreEqual(MoveResult.WrongSlot, _p.Check(gun, MoveTarget.ToSlot(EquipSlot.Primary)));
            Assert.AreEqual(MoveResult.WrongSlot, _p.Check(gun, MoveTarget.ToSlot(EquipSlot.Headwear)));
            Assert.AreEqual(MoveResult.Ok, _p.Move(gun, MoveTarget.ToSlot(EquipSlot.Holster)));
            Assert.AreSame(gun, _p.Equipped(EquipSlot.Holster));
            Assert.AreEqual(0, _p.Stash.Placements.Count);
        }

        [Test]
        public void DroppingOnOccupiedSlot_Swaps()
        {
            Item cap = _p.Create("cap");
            _p.Equip(cap, EquipSlot.Headwear);
            Item helmet = Stash("helmet", 3, 3);
            Assert.AreEqual(MoveResult.Swapped, _p.Move(helmet, MoveTarget.ToSlot(EquipSlot.Headwear)));
            Assert.AreSame(helmet, _p.Equipped(EquipSlot.Headwear));
            Assert.AreSame(cap, _p.Stash.ItemAt(3, 3));
        }

        [Test]
        public void Stacks_MergeUpToMax()
        {
            Item a = Stash("ammo_45_fmj", 0, 0, count: 40);
            Item b = Stash("ammo_45_fmj", 1, 0, count: 30);
            Assert.AreEqual(MoveResult.Merged, _p.Move(b, MoveTarget.ToCell(_p.Stash, 0, 0, false)));
            Assert.AreEqual(50, a.Count);
            Assert.AreEqual(20, b.Count);
            Assert.AreSame(b, _p.Stash.ItemAt(1, 0));
            Item c = Stash("ammo_45_fmj", 2, 0, count: 5);
            Assert.AreEqual(MoveResult.Merged, _p.Move(c, MoveTarget.ToCell(_p.Stash, 1, 0, false)));
            Assert.AreEqual(25, b.Count);
            Assert.IsNull(c.Location);
            Assert.IsNull(_p.Stash.ItemAt(2, 0));
        }

        [Test]
        public void DifferentAmmo_DoesNotMerge()
        {
            Stash("ammo_45_fmj", 0, 0, count: 10);
            Item hp = Stash("ammo_45_hp", 1, 0, count: 10);
            Assert.AreEqual(MoveResult.NoRoom, _p.Check(hp, MoveTarget.ToCell(_p.Stash, 0, 0, false)));
        }

        [Test]
        public void Split_CreatesNewStackNearby()
        {
            Item a = Stash("ammo_45_fmj", 0, 0, count: 50);
            Item part = _p.Split(a, 20);
            Assert.IsNotNull(part);
            Assert.AreEqual(30, a.Count);
            Assert.AreEqual(20, part.Count);
            Assert.AreSame(_p.Stash, part.Location.Grid);
            Assert.IsNull(_p.Split(a, 30), "cannot split the whole stack");
        }

        [Test]
        public void Container_CannotGoInsideItself()
        {
            Item bag = Stash("backpack", 0, 0);
            Item sling = _p.Create("sling");
            Assert.AreEqual(MoveResult.Ok, _p.Move(sling, MoveTarget.ToCell(bag.Grids[0], 0, 0, false)));
            Assert.AreEqual(MoveResult.IntoItself, _p.Check(bag, MoveTarget.ToCell(bag.Grids[0], 3, 3, false)));
            Assert.AreEqual(MoveResult.IntoItself, _p.Check(bag, MoveTarget.ToCell(sling.Grids[0], 0, 0, false)));
        }

        [Test]
        public void SecureContainer_RefusesContainersAndIsNeverNested()
        {
            Item alpha = _p.Create("alpha");
            _p.Equip(alpha, EquipSlot.SecureContainer);
            Item sling = Stash("sling", 0, 0);
            Assert.AreEqual(MoveResult.NotAllowed, _p.Check(sling, MoveTarget.ToCell(alpha.Grids[0], 0, 0, false)));
            Item bag = Stash("backpack", 4, 0);
            Assert.AreEqual(MoveResult.NotAllowed, _p.Check(alpha, MoveTarget.ToCell(bag.Grids[0], 0, 0, false)));
            Item bandage = Stash("bandage", 9, 9);
            Assert.AreEqual(MoveResult.Ok, _p.Check(bandage, MoveTarget.ToCell(alpha.Grids[0], 1, 1, false)));
        }

        [Test]
        public void Weight_IncludesContents()
        {
            Item bag = Stash("backpack", 0, 0);
            _p.Move(_p.Create("water"), MoveTarget.ToCell(bag.Grids[0], 0, 0, false));
            _p.Move(_p.Create("ammo_45_fmj", 50), MoveTarget.ToCell(bag.Grids[0], 1, 0, false));
            Assert.AreEqual(1.2f + 0.6f + 50 * 0.015f, bag.TotalWeight, 1e-4f);
        }

        [Test]
        public void QuickMove_EquipsFromStash_ThenSendsBack()
        {
            Item helmet = Stash("helmet", 0, 0);
            Assert.IsTrue(_p.QuickMove(helmet));
            Assert.AreSame(helmet, _p.Equipped(EquipSlot.Headwear));
            Assert.IsTrue(_p.QuickMove(helmet));
            Assert.IsTrue(_p.InStash(helmet));
        }

        [Test]
        public void QuickMove_LootGoesToCarriedGrids()
        {
            Item bandage = Stash("bandage", 0, 0);
            Assert.IsTrue(_p.QuickMove(bandage));
            Assert.AreSame(_p.Pockets[0], bandage.Location.Grid);
        }

        [Test]
        public void InStash_SeesThroughContainers()
        {
            Item box = Stash("ammo_case", 0, 0);
            Item ammo = _p.Create("ammo_45_fmj", 50);
            _p.Move(ammo, MoveTarget.ToCell(box.Grids[0], 0, 0, false));
            Assert.IsTrue(_p.InStash(ammo));
            _p.Move(box, MoveTarget.ToSlot(EquipSlot.Backpack));
            Assert.AreEqual(MoveResult.WrongSlot, _p.Check(box, MoveTarget.ToSlot(EquipSlot.Backpack)));
        }

        [Test]
        public void Sort_PacksBiggestFirstWithoutLosingItems()
        {
            Stash("bandage", 9, 5);
            Stash("armor", 5, 10);
            Stash("water", 0, 20);
            int before = _p.AllItems().Count();
            _p.Sort(_p.Stash);
            Assert.AreEqual(before, _p.AllItems().Count());
            Assert.AreEqual("armor", _p.Stash.ItemAt(0, 0).Def.Id);
            Assert.LessOrEqual(_p.Stash.UsedRows(), 3);
        }

        [Test]
        public void Serializer_RoundTripsEverything()
        {
            Profile p = StarterKit.Create(_db, WeaponBuild.M1911Default, AttachmentCatalog.M1911());
            p.Nickname = "Cobos el Topo";
            p.Level = 7;
            string saved = ProfileSerializer.Save(p);
            Profile q = ProfileSerializer.Load(saved, _db);
            Assert.IsNotNull(q);
            Assert.AreEqual("Cobos el Topo", q.Nickname);
            Assert.AreEqual(7, q.Level);
            Assert.AreEqual(p.AllItems().Count(), q.AllItems().Count());
            Assert.AreEqual(p.Money, q.Money);
            Assert.AreEqual(p.CarriedWeight, q.CarriedWeight, 1e-4f);
            Assert.AreEqual(saved, ProfileSerializer.Save(q));
            Assert.AreEqual(WeaponBuild.M1911Default.Serialize(), q.Equipped(EquipSlot.Holster).Build);
        }

        [Test]
        public void Serializer_SkipsUnknownItemsAndRejectsGarbage()
        {
            Assert.IsNull(ProfileSerializer.Load("hello", _db));
            Profile q = ProfileSerializer.Load("POLYKOV-PROFILE 1\nitem 5 laser_sword 1 stash 0 0 0 -\nitem 6 bandage 1 stash 0 0 0 -\n", _db);
            Assert.AreEqual(1, q.AllItems().Count());
            Assert.AreSame(_db.Get("bandage"), q.Stash.ItemAt(0, 0).Def);
            Assert.AreEqual(7, q.Create("bandage").Uid);
        }

        [Test]
        public void Starter_HasPistolGearAndLooseParts()
        {
            Profile p = StarterKit.Create(_db, WeaponBuild.M1911Default, AttachmentCatalog.M1911());
            Assert.AreEqual("m1911a1", p.Equipped(EquipSlot.Holster).Def.Id);
            Assert.IsNotNull(p.Equipped(EquipSlot.Rig));
            Assert.IsTrue(WeaponParts.Owns(p, "suppressor_45"));
            Assert.IsTrue(WeaponParts.Owns(p, "barrel_threaded"));
            Assert.IsFalse(WeaponParts.Owns(p, "barrel_standard"), "mounted on the pistol, not loose");
            Assert.Greater(p.Money, 400000);
        }

        [Test]
        public void WeaponParts_MountConsumesAndReturnsParts()
        {
            var catalog = AttachmentCatalog.M1911();
            Profile p = StarterKit.Create(_db, WeaponBuild.M1911Default, catalog);
            Item gun = p.Equipped(EquipSlot.Holster);
            WeaponBuild suppressed = ArmorerModel.Select(WeaponBuild.M1911Default, catalog, AttachmentSlot.Muzzle, "suppressor_45");
            Assert.IsTrue(WeaponParts.Apply(p, gun, WeaponBuild.M1911Default, suppressed, out string reason), reason);
            Assert.AreEqual(suppressed.Serialize(), gun.Build);
            Assert.IsFalse(WeaponParts.Owns(p, "suppressor_45"));
            Assert.IsFalse(WeaponParts.Owns(p, "barrel_threaded"));
            Assert.IsTrue(WeaponParts.Owns(p, "barrel_standard"), "the replaced barrel goes back to the stash");
            // And back to factory.
            Assert.IsTrue(WeaponParts.Apply(p, gun, suppressed, WeaponBuild.M1911Default, out reason), reason);
            Assert.IsTrue(WeaponParts.Owns(p, "suppressor_45"));
        }

        [Test]
        public void WeaponParts_FailsWithoutThePart_AndChangesNothing()
        {
            var catalog = AttachmentCatalog.M1911();
            Profile p = StarterKit.Create(_db, WeaponBuild.M1911Default, catalog);
            p.Discard(WeaponParts.FindLoose(p, "suppressor_45"));
            Item gun = p.Equipped(EquipSlot.Holster);
            int count = p.AllItems().Count();
            WeaponBuild suppressed = ArmorerModel.Select(WeaponBuild.M1911Default, catalog, AttachmentSlot.Muzzle, "suppressor_45");
            Assert.IsFalse(WeaponParts.Apply(p, gun, WeaponBuild.M1911Default, suppressed, out string reason));
            StringAssert.Contains("Silenciador", reason);
            CollectionAssert.AreEqual(new[] { "Silenciador .45 ACP" }, WeaponParts.Missing(p, WeaponBuild.M1911Default, suppressed));
            Assert.AreEqual(count, p.AllItems().Count());
            Assert.AreEqual(WeaponBuild.M1911Default.Serialize(), gun.Build);
        }
    }
}
