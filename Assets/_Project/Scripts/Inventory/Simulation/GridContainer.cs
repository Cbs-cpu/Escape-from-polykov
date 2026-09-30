using System.Collections.Generic;

namespace Polykov.Inventory
{
    /// <summary>A rectangular grid of cells holding items that cover w x h cells (optionally rotated 90 degrees).</summary>
    public sealed class GridContainer
    {
        public readonly int Width, Height;
        /// <summary>Container item owning this grid (null for the stash and pockets).</summary>
        public readonly Item Owner;
        public readonly int Index;
        /// <summary>Friendly name for grids without an owner ("stash", "pocket0"...), used by the save format.</summary>
        public string Tag;

        private readonly List<Placement> _placements = new List<Placement>();
        public IReadOnlyList<Placement> Placements => _placements;

        public GridContainer(int width, int height, Item owner = null, int index = 0)
        {
            Width = width;
            Height = height;
            Owner = owner;
            Index = index;
        }

        public static int FootprintW(ItemDef def, bool rotated) => rotated ? def.Height : def.Width;
        public static int FootprintH(ItemDef def, bool rotated) => rotated ? def.Width : def.Height;

        /// <summary>Item covering cell (x, y), or null.</summary>
        public Item ItemAt(int x, int y)
        {
            foreach (Placement p in _placements)
                if (x >= p.X && x < p.X + p.W && y >= p.Y && y < p.Y + p.H) return p.Item;
            return null;
        }

        public bool TryGetPlacement(Item item, out Placement placement)
        {
            foreach (Placement p in _placements)
                if (p.Item == item) { placement = p; return true; }
            placement = default;
            return false;
        }

        /// <summary>Fits inside the grid and overlaps nothing but <paramref name="ignore"/>.</summary>
        public bool CanPlace(ItemDef def, int x, int y, bool rotated, Item ignore = null)
        {
            int w = FootprintW(def, rotated), h = FootprintH(def, rotated);
            if (x < 0 || y < 0 || x + w > Width || y + h > Height) return false;
            foreach (Placement p in _placements)
            {
                if (p.Item == ignore) continue;
                if (x < p.X + p.W && p.X < x + w && y < p.Y + p.H && p.Y < y + h) return false;
            }
            return true;
        }

        /// <summary>First free spot scanning rows top to bottom (upright first, then rotated).</summary>
        public bool FindFree(ItemDef def, out int x, out int y, out bool rotated, Item ignore = null)
        {
            for (int pass = 0; pass < 2; pass++)
            {
                bool rot = pass == 1;
                if (rot && def.Width == def.Height) break;
                for (int yy = 0; yy < Height; yy++)
                    for (int xx = 0; xx < Width; xx++)
                        if (CanPlace(def, xx, yy, rot, ignore)) { x = xx; y = yy; rotated = rot; return true; }
            }
            x = y = 0;
            rotated = false;
            return false;
        }

        internal void Add(Item item, int x, int y, bool rotated)
        {
            _placements.Add(new Placement { Item = item, X = x, Y = y, Rotated = rotated });
            item.Location = ItemLocation.InGrid(this, x, y, rotated);
        }

        internal void Remove(Item item)
        {
            for (int i = 0; i < _placements.Count; i++)
                if (_placements[i].Item == item) { _placements.RemoveAt(i); break; }
        }

        internal void Clear() => _placements.Clear();

        /// <summary>Number of rows actually used (the stash UI shows a few empty rows below).</summary>
        public int UsedRows()
        {
            int rows = 0;
            foreach (Placement p in _placements) rows = System.Math.Max(rows, p.Y + p.H);
            return rows;
        }
    }
}
