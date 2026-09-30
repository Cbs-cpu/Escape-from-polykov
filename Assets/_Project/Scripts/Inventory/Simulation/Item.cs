using System.Collections.Generic;

namespace Polykov.Inventory
{
    /// <summary>One item instance: a stack, a weapon with its build, or a container with its own grids.</summary>
    public sealed class Item
    {
        public readonly int Uid;
        public readonly ItemDef Def;
        public int Count;
        /// <summary>Weapons: the mounted build (<c>WeaponBuild.Serialize()</c> format).</summary>
        public string Build;
        /// <summary>Inner grids for containers (same order as <see cref="ItemDef.Grids"/>), empty otherwise.</summary>
        public readonly GridContainer[] Grids;
        /// <summary>Where the item is now (null = nowhere, e.g. just created or discarded).</summary>
        public ItemLocation Location;

        public Item(int uid, ItemDef def, int count = 1)
        {
            Uid = uid;
            Def = def;
            Count = count < 1 ? 1 : count;
            Grids = new GridContainer[def.Grids.Length];
            for (int i = 0; i < Grids.Length; i++) Grids[i] = new GridContainer(def.Grids[i].Width, def.Grids[i].Height, this, i);
        }

        /// <summary>Own weight plus everything inside.</summary>
        public float TotalWeight
        {
            get
            {
                float w = Def.WeightKg * (Def.Stackable ? Count : 1);
                foreach (GridContainer g in Grids)
                    foreach (Placement p in g.Placements) w += p.Item.TotalWeight;
                return w;
            }
        }

        /// <summary>True if <paramref name="other"/> is this item or is (recursively) inside it.</summary>
        public bool Contains(Item other)
        {
            if (other == this) return true;
            foreach (GridContainer g in Grids)
                foreach (Placement p in g.Placements)
                    if (p.Item.Contains(other)) return true;
            return false;
        }

        public IEnumerable<Item> Children()
        {
            foreach (GridContainer g in Grids)
                foreach (Placement p in g.Placements) yield return p.Item;
        }

        public override string ToString() => $"{Def.Id}#{Uid}x{Count}";
    }

    /// <summary>Where an item sits: an equipment slot, or a cell of a grid.</summary>
    public sealed class ItemLocation
    {
        public readonly GridContainer Grid;
        public readonly int X, Y;
        public readonly bool Rotated;
        public readonly bool IsSlot;
        public readonly EquipSlot Slot;

        private ItemLocation(GridContainer grid, int x, int y, bool rotated, bool isSlot, EquipSlot slot)
        {
            Grid = grid; X = x; Y = y; Rotated = rotated; IsSlot = isSlot; Slot = slot;
        }

        public static ItemLocation InGrid(GridContainer grid, int x, int y, bool rotated) => new ItemLocation(grid, x, y, rotated, false, default);
        public static ItemLocation InSlot(EquipSlot slot) => new ItemLocation(null, 0, 0, false, true, slot);
    }

    public struct Placement
    {
        public Item Item;
        public int X, Y;
        public bool Rotated;
        public int W => Rotated ? Item.Def.Height : Item.Def.Width;
        public int H => Rotated ? Item.Def.Width : Item.Def.Height;
    }
}
