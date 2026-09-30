using System.Collections.Generic;
using Polykov.Weapons;

namespace Polykov.Inventory
{
    /// <summary>
    /// Links the armorer to the inventory (Tarkov rule): a part can be mounted only if you own it loose in your
    /// stash/containers; mounting consumes it and the part it replaces goes back to the stash.
    /// </summary>
    public static class WeaponParts
    {
        public static WeaponBuild BuildOf(Item weapon, WeaponBuild fallback) => WeaponBuild.ParseOr(weapon.Build, fallback);

        /// <summary>A loose item carrying this armorer part, or null.</summary>
        public static Item FindLoose(Profile p, string attachmentId)
        {
            if (string.IsNullOrEmpty(attachmentId)) return null;
            foreach (Item i in p.AllItems())
                if (i.Def.AttachmentId == attachmentId && i.Location != null && !i.Location.IsSlot) return i;
            return null;
        }

        public static bool Owns(Profile p, string attachmentId) => FindLoose(p, attachmentId) != null;

        /// <summary>
        /// Mounts <paramref name="next"/> on <paramref name="weapon"/>: parts that appear are taken from the inventory,
        /// parts that disappear become items in the stash. All or nothing; <paramref name="reason"/> says why not.
        /// </summary>
        public static bool Apply(Profile p, Item weapon, WeaponBuild current, WeaponBuild next, out string reason)
        {
            reason = null;
            var added = new List<string>();
            var removed = new List<string>();
            for (int i = 0; i < WeaponBuild.SlotCount; i++)
            {
                var slot = (AttachmentSlot)i;
                string a = current.Get(slot), b = next.Get(slot);
                if (a == b) continue;
                if (!string.IsNullOrEmpty(a)) removed.Add(a);
                if (!string.IsNullOrEmpty(b)) added.Add(b);
            }

            var taken = new List<Item>();
            foreach (string id in added)
            {
                Item part = null;
                foreach (Item i in p.AllItems())
                    if (i.Def.AttachmentId == id && i.Location != null && !i.Location.IsSlot && !taken.Contains(i)) { part = i; break; }
                if (part == null)
                {
                    ItemDef def = p.Db.ForAttachment(id);
                    reason = "No tienes " + (def != null ? def.Name : id) + ".";
                    return false;
                }
                taken.Add(part);
            }

            // Returned parts must all fit in the stash (space freed by the taken parts counts).
            var returned = new List<Item>();
            var takenFrom = new List<ItemLocation>();
            foreach (Item t in taken) { takenFrom.Add(t.Location); p.Detach(t); }
            foreach (string id in removed)
            {
                ItemDef def = p.Db.ForAttachment(id);
                if (def == null) continue;
                Item back = p.Create(def.Id);
                if (!p.StoreInStash(back))
                {
                    foreach (Item r in returned) p.Detach(r);
                    for (int i = 0; i < taken.Count; i++) Restore(p, taken[i], takenFrom[i]);
                    reason = "No hay sitio en el alijo para " + def.Name + ".";
                    return false;
                }
                returned.Add(back);
            }
            weapon.Build = next.Serialize();
            return true;
        }

        private static void Restore(Profile p, Item item, ItemLocation where)
        {
            if (where.IsSlot) p.Attach(item, MoveTarget.ToSlot(where.Slot));
            else p.Attach(item, MoveTarget.ToCell(where.Grid, where.X, where.Y, where.Rotated));
        }
    }
}
