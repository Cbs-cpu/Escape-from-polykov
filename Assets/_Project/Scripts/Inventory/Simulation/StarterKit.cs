using Polykov.Weapons;

namespace Polykov.Inventory
{
    /// <summary>The profile a new player starts with (and the lobby falls back to if the save is missing).</summary>
    public static class StarterKit
    {
        public static Profile Create(ItemDatabase db, WeaponBuild pistolBuild, AttachmentCatalog catalog)
        {
            var p = new Profile(db);

            Item pistol = p.Create("m1911a1");
            pistol.Build = pistolBuild.Serialize();
            p.Equip(pistol, EquipSlot.Holster);

            // The AK-74N in its factory configuration.
            WeaponFamily ak = WeaponFamilies.AK74N();
            Item rifle = p.Create("ak74n");
            rifle.Build = ak.FactoryBuild.Serialize();
            p.Equip(rifle, EquipSlot.Primary);
            p.Equip(p.Create("cap"), EquipSlot.Headwear);
            p.Equip(p.Create("armband"), EquipSlot.Armband);
            p.Equip(p.Create("knife"), EquipSlot.Sheath);

            Item rig = p.Create("rig");
            p.Equip(rig, EquipSlot.Rig);
            p.Store(p.Create("magazine_7"), new[] { rig.Grids[0] });
            p.Store(p.Create("magazine_7"), new[] { rig.Grids[1] });
            p.Store(p.Create("ak_mag_30"), new[] { rig.Grids[2] });
            p.Store(p.Create("ak_mag_30"), new[] { rig.Grids[3] });
            p.Store(p.Create("ammo_45_fmj", 28), new[] { rig.Grids[4] });
            p.Store(p.Create("bandage"), new[] { rig.Grids[5] });

            p.Store(p.Create("ai2"), new[] { p.Pockets[0] });
            p.Store(p.Create("crackers"), new[] { p.Pockets[1] });

            Item bag = p.Create("backpack");
            p.Equip(bag, EquipSlot.Backpack);
            p.Store(p.Create("water"), bag.Grids);
            p.Store(p.Create("tushonka"), bag.Grids);
            p.Store(p.Create("splint"), bag.Grids);

            Item alpha = p.Create("alpha");
            p.Equip(alpha, EquipSlot.SecureContainer);
            p.Store(p.Create("roubles", 25000), alpha.Grids);

            // Stash: every armorer part not mounted on the pistol, plus loot to play with.
            foreach (AttachmentRules rules in catalog.All)
                if (!pistolBuild.Has(rules.Id) && db.ForAttachment(rules.Id) != null && rules.Slot != AttachmentSlot.Magazine)
                    p.StoreInStash(p.Create(db.ForAttachment(rules.Id).Id));
            // One of every AK part that is not mounted in the factory build.
            foreach (AttachmentRules rules in ak.Catalog.All)
            {
                ItemDef part = db.ForAttachment(rules.Id);
                if (part != null && !ak.FactoryBuild.Has(rules.Id)) p.StoreInStash(p.Create(part.Id));
            }
            foreach (string id in new[] { "armor", "helmet", "headset", "balaclava", "glasses", "sling", "carkit", "water",
                         "gunpowder", "bolts", "wires", "cigs", "key_dorm", "splint", "bandage", "bandage", "tushonka", "magazine_7" })
                p.StoreInStash(p.Create(id));
            p.StoreInStash(p.Create("ammo_45_fmj", 50));
            p.StoreInStash(p.Create("ammo_45_fmj", 50));
            p.StoreInStash(p.Create("ammo_45_hp", 30));
            p.StoreInStash(p.Create("ammo_545_ps", 60));
            p.StoreInStash(p.Create("ammo_545_ps", 60));
            p.StoreInStash(p.Create("ammo_545_bp", 60));
            p.Store(p.Create("ammo_545_ps", 30), bag.Grids);
            p.StoreInStash(p.Create("roubles", 461200));
            Item box = p.Create("ammo_case");
            p.StoreInStash(box);
            p.Store(p.Create("ammo_45_fmj", 50), box.Grids);
            p.Store(p.Create("ammo_45_fmj", 50), box.Grids);
            return p;
        }
    
        /// <summary>
        /// Brings a profile saved before the AK-74N existed up to date: if the player owns no AK, one is given (equipped
        /// as Primary when the slot is free, else in the stash) with a spare magazine, 5.45 ammo and one of every
        /// non-factory part. Returns true if anything was added (the host saves the profile).
        /// </summary>
        public static bool GrantMissing(Profile p)
        {
            foreach (Item i in p.AllItems())
                if (i.Def.WeaponId == "ak74n") return false;
            WeaponFamily ak = WeaponFamilies.AK74N();
            Item rifle = p.Create("ak74n");
            rifle.Build = ak.FactoryBuild.Serialize();
            if (p.Equipped(EquipSlot.Primary) != null || !p.Equip(rifle, EquipSlot.Primary)) p.StoreInStash(rifle);
            p.StoreInStash(p.Create("ak_mag_30"));
            p.StoreInStash(p.Create("ammo_545_ps", 60));
            foreach (AttachmentRules rules in ak.Catalog.All)
            {
                ItemDef part = p.Db.ForAttachment(rules.Id);
                if (part != null && !ak.FactoryBuild.Has(rules.Id)) p.StoreInStash(p.Create(part.Id));
            }
            return true;
        }
    }
}
