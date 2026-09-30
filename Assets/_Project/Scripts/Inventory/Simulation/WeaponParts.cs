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

        /// <summary>Names of the parts <paramref name="next"/> adds that are not in the inventory (empty = can mount).</summary>
        public static List<string> Missing(Profile p, WeaponBuild current, WeaponBuild next)
        {
            var missing = new List<string>();
            for (int i = 0; i < WeaponBuild.SlotCount; i++)
            {
                var slot = (AttachmentSlot)i;
                string b = next.Get(slot);
                if (string.IsNullOrEmpty(b) || b == current.Get(slot) || Owns(p, b)) continue;
                ItemDef def = p.Db.ForAttachment(b);
                missing.Add(def != null ? def.Name : b);
            }
            return missing;
        }

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

        // ------------------------------------------------------------------ drag & drop (Tarkov style)

        /// <summary>
        /// Whether <paramref name="part"/> (that exact item) could be mounted on <paramref name="weapon"/> right now:
        /// it belongs to the family, is in the inventory and any parts it drags along are owned too. Changes nothing.
        /// </summary>
        public static bool CanMount(Profile p, Item weapon, Item part, WeaponFamily family, out string reason)
            => Plan(p, weapon, part, family, out _, out _, out _, out _, out reason);

        /// <summary>
        /// Mounts that exact item instance in its slot. The part it replaces (and any dependents that disappear) become
        /// new items: the replaced one where the dragged part was if it fits there, else in the stash. All or nothing.
        /// </summary>
        public static bool TryMountItem(Profile p, Item weapon, Item part, WeaponFamily family, out string reason)
        {
            if (!Plan(p, weapon, part, family, out AttachmentSlot slot, out WeaponBuild current, out WeaponBuild next,
                    out List<Item> taken, out reason))
                return false;

            var removed = new List<string>();
            string replaced = current.Get(slot);
            if (replaced != null) removed.Add(replaced);
            for (int i = 0; i < WeaponBuild.SlotCount; i++)
            {
                var s = (AttachmentSlot)i;
                string a = current.Get(s);
                if (s != slot && a != null && next.Get(s) != a) removed.Add(a);
            }

            var takenFrom = new List<ItemLocation>();
            foreach (Item t in taken) takenFrom.Add(t.Location);
            ItemLocation home = part.Location;
            foreach (Item t in taken) p.Detach(t);

            var created = new List<Item>();
            foreach (string id in removed)
            {
                ItemDef def = p.Db.ForAttachment(id);
                if (def == null) continue;
                Item back = p.Create(def.Id);
                bool placed = id == replaced && PlaceAt(p, back, home);
                if (!placed) placed = p.StoreInStash(back);
                if (!placed)
                {
                    foreach (Item c in created) p.Detach(c);
                    for (int i = 0; i < taken.Count; i++) Restore(p, taken[i], takenFrom[i]);
                    reason = "No hay sitio en el alijo para " + def.Name + ".";
                    return false;
                }
                created.Add(back);
            }
            weapon.Build = next.Serialize();
            reason = null;
            return true;
        }

        /// <summary>
        /// Takes the part out of an optional slot (a muzzle device) and creates it as an item at <paramref name="target"/>
        /// (a grid cell) or, if it is null or does not fit, in the stash. Required slots refuse. All or nothing.
        /// </summary>
        public static bool TryDetach(Profile p, Item weapon, AttachmentSlot slot, WeaponFamily family, MoveTarget? target, out string reason)
        {
            reason = null;
            if (weapon == null || family == null || weapon.Def.Category != ItemCategory.Weapon) { reason = "Eso no es un arma."; return false; }
            if (!family.HasSlot(slot)) { reason = "Ese arma no tiene esa ranura."; return false; }
            if (!ArmorerModel.IsOptional(slot)) { reason = "Esta ranura no se puede dejar vacía."; return false; }
            WeaponBuild current = BuildOf(weapon, family.FactoryBuild);
            string id = current.Get(slot);
            if (id == null) { reason = "No hay nada montado ahí."; return false; }
            WeaponBuild next = ArmorerModel.Select(current, family.Catalog, slot, null);
            if (next.Get(slot) != null) { reason = "No se puede quitar esa pieza."; return false; }

            var removed = new List<string> { id };
            for (int i = 0; i < WeaponBuild.SlotCount; i++)
            {
                var s = (AttachmentSlot)i;
                string a = current.Get(s);
                if (s != slot && a != null && next.Get(s) == null) removed.Add(a);
            }

            var created = new List<Item>();
            foreach (string partId in removed)
            {
                ItemDef def = p.Db.ForAttachment(partId);
                if (def == null) continue;
                Item item = p.Create(def.Id);
                bool placed = partId == id && target.HasValue && !target.Value.IsSlot && target.Value.Grid != null
                    && p.Check(item, target.Value) == MoveResult.Ok;
                if (placed) p.Attach(item, target.Value);
                else placed = p.StoreInStash(item);
                if (!placed)
                {
                    foreach (Item c in created) p.Detach(c);
                    reason = "No hay sitio en el alijo para " + def.Name + ".";
                    return false;
                }
                created.Add(item);
            }
            weapon.Build = next.Serialize();
            return true;
        }

        /// <summary>Shared validation of mounting one exact item; also gathers the extra loose parts the mount drags along.</summary>
        private static bool Plan(Profile p, Item weapon, Item part, WeaponFamily family, out AttachmentSlot slot, out WeaponBuild current,
            out WeaponBuild next, out List<Item> taken, out string reason)
        {
            slot = default;
            current = next = default;
            taken = null;
            if (weapon == null || part == null || family == null || weapon.Def.Category != ItemCategory.Weapon)
            {
                reason = "Eso no es un arma.";
                return false;
            }
            string id = part.Def.AttachmentId;
            if (string.IsNullOrEmpty(id)) { reason = "Eso no es una pieza de arma."; return false; }
            if (!family.Catalog.TryGet(id, out AttachmentRules rules) || !family.HasSlot(rules.Slot))
            {
                reason = "No es compatible con " + family.DisplayName + ".";
                return false;
            }
            if (part.Location == null || part.Location.IsSlot) { reason = "La pieza tiene que estar en tu inventario."; return false; }
            slot = rules.Slot;
            current = BuildOf(weapon, family.FactoryBuild);
            if (current.Get(slot) == id) { reason = "Ya está montada."; return false; }
            next = ArmorerModel.Select(current, family.Catalog, slot, id);
            if (next.Get(slot) != id) { reason = "No se puede montar con la configuración actual."; return false; }

            taken = new List<Item> { part };
            for (int i = 0; i < WeaponBuild.SlotCount; i++)
            {
                var s = (AttachmentSlot)i;
                string b = next.Get(s);
                if (s == slot || string.IsNullOrEmpty(b) || b == current.Get(s)) continue;
                Item extra = null;
                foreach (Item it in p.AllItems())
                    if (it.Def.AttachmentId == b && it.Location != null && !it.Location.IsSlot && !taken.Contains(it)) { extra = it; break; }
                if (extra == null)
                {
                    ItemDef def = p.Db.ForAttachment(b);
                    reason = "No tienes " + (def != null ? def.Name : b) + ".";
                    return false;
                }
                taken.Add(extra);
            }
            reason = null;
            return true;
        }

        /// <summary>Puts a new item at the spot of the removed one: same cell (same turn, else the other) or not at all.</summary>
        private static bool PlaceAt(Profile p, Item item, ItemLocation where)
        {
            if (where == null || where.IsSlot || where.Grid == null) return false;
            for (int pass = 0; pass < 2; pass++)
            {
                var target = MoveTarget.ToCell(where.Grid, where.X, where.Y, pass == 0 ? where.Rotated : !where.Rotated);
                if (p.Check(item, target) != MoveResult.Ok) continue;
                p.Attach(item, target);
                return true;
            }
            return false;
        }

        private static void Restore(Profile p, Item item, ItemLocation where)
        {
            if (where.IsSlot) p.Attach(item, MoveTarget.ToSlot(where.Slot));
            else p.Attach(item, MoveTarget.ToCell(where.Grid, where.X, where.Y, where.Rotated));
        }
    }
}
