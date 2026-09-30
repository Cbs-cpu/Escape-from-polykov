using System;
using System.Collections.Generic;

namespace Polykov.Inventory
{
    public enum MoveResult : byte { Ok, Merged, Swapped, NoRoom, WrongSlot, IntoItself, NotAllowed }

    /// <summary>Where a dragged item is dropped: an equipment slot or a cell of a grid.</summary>
    public struct MoveTarget
    {
        public bool IsSlot;
        public EquipSlot Slot;
        public GridContainer Grid;
        public int X, Y;
        public bool Rotated;

        public static MoveTarget ToSlot(EquipSlot slot) => new MoveTarget { IsSlot = true, Slot = slot };
        public static MoveTarget ToCell(GridContainer grid, int x, int y, bool rotated)
            => new MoveTarget { Grid = grid, X = x, Y = y, Rotated = rotated };
    }

    /// <summary>
    /// The player's persistent inventory (Tarkov model): equipment slots, four 1x1 pockets and the stash. Pure rules,
    /// no engine types; the lobby UI and (later) the server drive it with <see cref="Check"/> / <see cref="Move"/>.
    /// </summary>
    public sealed class Profile
    {
        public const int StashWidth = 10;
        public const int StashHeight = 40;
        public static readonly int SlotCount = Enum.GetValues(typeof(EquipSlot)).Length;

        public readonly ItemDatabase Db;
        public readonly GridContainer Stash;
        public readonly GridContainer[] Pockets;
        public string Nickname = "Operador";
        public int Level = 1;
        public int Experience;

        private readonly Item[] _equipment;
        private int _nextUid = 1;

        public Profile(ItemDatabase db)
        {
            Db = db;
            Stash = new GridContainer(StashWidth, StashHeight) { Tag = "stash" };
            Pockets = new GridContainer[4];
            for (int i = 0; i < 4; i++) Pockets[i] = new GridContainer(1, 1) { Tag = "pocket" + i };
            _equipment = new Item[SlotCount];
        }

        public Item Equipped(EquipSlot slot) => _equipment[(int)slot];

        /// <summary>A new item (not placed anywhere yet).</summary>
        public Item Create(string defId, int count = 1)
        {
            ItemDef def = Db.Get(defId) ?? throw new ArgumentException("Unknown item " + defId);
            return new Item(_nextUid++, def, Math.Min(count, def.MaxStack));
        }

        internal Item CreateWithUid(int uid, ItemDef def, int count)
        {
            _nextUid = Math.Max(_nextUid, uid + 1);
            return new Item(uid, def, count);
        }

        // ------------------------------------------------------------------ queries

        /// <summary>Grids carried into a raid, in quick-move order: rig, pockets, backpack, secure container.</summary>
        public IEnumerable<GridContainer> CarriedGrids()
        {
            foreach (EquipSlot s in new[] { EquipSlot.Rig })
                if (Equipped(s) != null) foreach (GridContainer g in Equipped(s).Grids) yield return g;
            foreach (GridContainer g in Pockets) yield return g;
            foreach (EquipSlot s in new[] { EquipSlot.Backpack, EquipSlot.SecureContainer })
                if (Equipped(s) != null) foreach (GridContainer g in Equipped(s).Grids) yield return g;
        }

        public IEnumerable<Item> EquippedItems()
        {
            foreach (Item i in _equipment) if (i != null) yield return i;
        }

        /// <summary>Every item, top-level and nested.</summary>
        public IEnumerable<Item> AllItems()
        {
            var stack = new Stack<Item>();
            foreach (Item i in _equipment) if (i != null) stack.Push(i);
            foreach (GridContainer g in Pockets) foreach (Placement p in g.Placements) stack.Push(p.Item);
            foreach (Placement p in Stash.Placements) stack.Push(p.Item);
            while (stack.Count > 0)
            {
                Item i = stack.Pop();
                yield return i;
                foreach (Item c in i.Children()) stack.Push(c);
            }
        }

        public int Money
        {
            get
            {
                int sum = 0;
                foreach (Item i in AllItems()) if (i.Def.Category == ItemCategory.Money) sum += i.Count;
                return sum;
            }
        }

        /// <summary>Weight carried into a raid (equipment and pockets, with contents).</summary>
        public float CarriedWeight
        {
            get
            {
                float w = 0f;
                foreach (Item i in _equipment) if (i != null) w += i.TotalWeight;
                foreach (GridContainer g in Pockets) foreach (Placement p in g.Placements) w += p.Item.TotalWeight;
                return w;
            }
        }

        /// <summary>True if the item is in the stash (directly or inside a container stored in the stash).</summary>
        public bool InStash(Item item)
        {
            ItemLocation loc = item.Location;
            while (loc != null && !loc.IsSlot)
            {
                if (loc.Grid == Stash) return true;
                if (loc.Grid.Owner == null) return false;
                loc = loc.Grid.Owner.Location;
            }
            return false;
        }

        // ------------------------------------------------------------------ moves

        /// <summary>What <see cref="Move"/> would do, without changing anything.</summary>
        public MoveResult Check(Item item, MoveTarget target)
        {
            if (target.IsSlot)
            {
                if (!item.Def.Fits(target.Slot)) return MoveResult.WrongSlot;
                Item occupant = Equipped(target.Slot);
                if (occupant == null || occupant == item) return MoveResult.Ok;
                return CanSwap(item, occupant) ? MoveResult.Swapped : MoveResult.NoRoom;
            }
            GridContainer grid = target.Grid;
            if (grid == null) return MoveResult.NotAllowed;
            if (grid.Owner != null && item.Contains(grid.Owner)) return MoveResult.IntoItself;
            if (!AllowedIn(item, grid)) return MoveResult.NotAllowed;
            Item under = grid.ItemAt(target.X, target.Y);
            if (under != null && under != item && item.Def.Stackable && under.Def == item.Def && under.Count < under.Def.MaxStack)
                return MoveResult.Merged;
            return grid.CanPlace(item.Def, target.X, target.Y, target.Rotated, item) ? MoveResult.Ok : MoveResult.NoRoom;
        }

        public MoveResult Move(Item item, MoveTarget target)
        {
            MoveResult check = Check(item, target);
            switch (check)
            {
                case MoveResult.Ok:
                    Detach(item);
                    Attach(item, target);
                    return check;
                case MoveResult.Merged:
                {
                    Item under = target.Grid.ItemAt(target.X, target.Y);
                    int moved = Math.Min(item.Count, under.Def.MaxStack - under.Count);
                    under.Count += moved;
                    item.Count -= moved;
                    if (item.Count <= 0) Detach(item);
                    return check;
                }
                case MoveResult.Swapped:
                {
                    Item occupant = Equipped(target.Slot);
                    ItemLocation from = item.Location;
                    Detach(item);
                    Detach(occupant);
                    Attach(item, target);
                    if (from.IsSlot) Attach(occupant, MoveTarget.ToSlot(from.Slot));
                    else PlaceSwapped(occupant, from);
                    return check;
                }
                default:
                    return check;
            }
        }

        private bool CanSwap(Item item, Item occupant)
        {
            ItemLocation from = item.Location;
            if (from == null) return false;
            if (from.IsSlot) return occupant.Def.Fits(from.Slot);
            if (from.Grid.Owner != null && occupant.Contains(from.Grid.Owner)) return false;
            if (!AllowedIn(occupant, from.Grid)) return false;
            return from.Grid.CanPlace(occupant.Def, from.X, from.Y, false, item) || from.Grid.CanPlace(occupant.Def, from.X, from.Y, true, item);
        }

        private static void PlaceSwapped(Item occupant, ItemLocation where)
        {
            bool upright = where.Grid.CanPlace(occupant.Def, where.X, where.Y, false);
            where.Grid.Add(occupant, where.X, where.Y, !upright);
        }

        /// <summary>Secure containers never go inside other items; containers cannot hold themselves.</summary>
        private static bool AllowedIn(Item item, GridContainer grid)
        {
            if (grid.Owner == null) return true;
            if (item.Def.Category == ItemCategory.SecureContainer) return false;
            // A secure container only takes small items (Tarkov: no backpacks, rigs or long guns).
            if (grid.Owner.Def.Category == ItemCategory.SecureContainer && (item.Def.IsContainer || item.Def.LongGun)) return false;
            return true;
        }

        /// <summary>Puts a loose (unplaced) item into the first grid of <paramref name="grids"/> with room.</summary>
        public bool Store(Item item, IEnumerable<GridContainer> grids)
        {
            foreach (GridContainer g in grids)
            {
                if (g.Owner != null && item.Contains(g.Owner)) continue;
                if (!AllowedIn(item, g)) continue;
                if (item.Def.Stackable && TopUpStacks(item, g) && item.Count == 0) return true;
                if (g.FindFree(item.Def, out int x, out int y, out bool rot, item))
                {
                    Detach(item);
                    g.Add(item, x, y, rot);
                    return true;
                }
            }
            return false;
        }

        public bool StoreInStash(Item item) => Store(item, new[] { Stash });

        /// <summary>Fills partial stacks of the same kind in <paramref name="grid"/>; returns true if anything moved.</summary>
        private bool TopUpStacks(Item item, GridContainer grid)
        {
            bool moved = false;
            foreach (Placement p in grid.Placements)
            {
                if (p.Item == item || p.Item.Def != item.Def || p.Item.Count >= item.Def.MaxStack) continue;
                int n = Math.Min(item.Count, item.Def.MaxStack - p.Item.Count);
                p.Item.Count += n;
                item.Count -= n;
                moved = true;
                if (item.Count == 0) { Detach(item); break; }
            }
            return moved;
        }

        /// <summary>Equips an unplaced/stash item into a slot directly (no swap). Used by the starter kit and quick move.</summary>
        public bool Equip(Item item, EquipSlot slot)
        {
            if (!item.Def.Fits(slot) || Equipped(slot) != null) return false;
            Detach(item);
            _equipment[(int)slot] = item;
            item.Location = ItemLocation.InSlot(slot);
            return true;
        }

        /// <summary>
        /// Ctrl+click: from the stash, equip into an empty matching slot or else carry it (rig, pockets, backpack);
        /// from the character, send it to the stash.
        /// </summary>
        public bool QuickMove(Item item)
        {
            if (InStash(item))
            {
                foreach (EquipSlot slot in (EquipSlot[])Enum.GetValues(typeof(EquipSlot)))
                    if (Equipped(slot) == null && item.Def.Fits(slot)) return Equip(item, slot);
                return Store(item, CarriedGrids());
            }
            return StoreInStash(item);
        }

        /// <summary>Splits <paramref name="amount"/> off a stack into a new item placed in the same grid (or the stash).</summary>
        public Item Split(Item item, int amount)
        {
            if (!item.Def.Stackable || amount <= 0 || amount >= item.Count) return null;
            Item part = new Item(_nextUid++, item.Def, amount);
            GridContainer home = item.Location != null && !item.Location.IsSlot ? item.Location.Grid : Stash;
            if (!home.FindFree(part.Def, out int x, out int y, out bool rot))
            {
                if (home == Stash || !Stash.FindFree(part.Def, out x, out y, out rot)) return null;
                home = Stash;
            }
            item.Count -= amount;
            home.Add(part, x, y, rot);
            return part;
        }

        public void Discard(Item item) => Detach(item);

        /// <summary>Rearranges a grid: biggest items first, then by category and name, each in the first free spot.</summary>
        public void Sort(GridContainer grid)
        {
            var items = new List<Placement>(grid.Placements);
            items.Sort((a, b) =>
            {
                int area = (b.Item.Def.Width * b.Item.Def.Height).CompareTo(a.Item.Def.Width * a.Item.Def.Height);
                if (area != 0) return area;
                int cat = a.Item.Def.Category.CompareTo(b.Item.Def.Category);
                return cat != 0 ? cat : string.CompareOrdinal(a.Item.Def.Name, b.Item.Def.Name);
            });
            grid.Clear();
            var failed = new List<Placement>();
            foreach (Placement p in items)
            {
                if (grid.FindFree(p.Item.Def, out int x, out int y, out bool rot)) grid.Add(p.Item, x, y, rot);
                else failed.Add(p);
            }
            // Cannot happen with the same items, but never lose anything.
            foreach (Placement p in failed) grid.Add(p.Item, p.X, p.Y, p.Rotated);
        }

        // ------------------------------------------------------------------ low level

        internal void Attach(Item item, MoveTarget target)
        {
            if (target.IsSlot)
            {
                _equipment[(int)target.Slot] = item;
                item.Location = ItemLocation.InSlot(target.Slot);
            }
            else
            {
                target.Grid.Add(item, target.X, target.Y, target.Rotated);
            }
        }

        internal void Detach(Item item)
        {
            ItemLocation loc = item.Location;
            if (loc == null) return;
            if (loc.IsSlot) { if (_equipment[(int)loc.Slot] == item) _equipment[(int)loc.Slot] = null; }
            else loc.Grid.Remove(item);
            item.Location = null;
        }

        /// <summary>Grid by save-format tag ("stash", "pocket0".."pocket3").</summary>
        internal GridContainer RootGrid(string tag)
        {
            if (tag == Stash.Tag) return Stash;
            foreach (GridContainer g in Pockets) if (g.Tag == tag) return g;
            return null;
        }
    }
}
