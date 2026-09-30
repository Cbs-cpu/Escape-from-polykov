using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Polykov.Inventory
{
    /// <summary>
    /// Plain-text save format (one item per line, parents before children) so saves diff well and survive catalogue
    /// changes: unknown item ids are skipped instead of breaking the load.
    /// <code>
    /// POLYKOV-PROFILE 1
    /// nick Operador
    /// level 1
    /// item &lt;uid&gt; &lt;defId&gt; &lt;count&gt; &lt;where&gt; &lt;x&gt; &lt;y&gt; &lt;rot 0|1&gt; &lt;build or -&gt;
    /// </code>
    /// where = stash | pocket0..3 | slot:&lt;EquipSlot&gt; | in:&lt;uid&gt;:&lt;gridIndex&gt;
    /// </summary>
    public static class ProfileSerializer
    {
        private const string Header = "POLYKOV-PROFILE 1";

        public static string Save(Profile p)
        {
            var sb = new StringBuilder();
            sb.Append(Header).Append('\n');
            sb.Append("nick ").Append(p.Nickname.Replace('\n', ' ')).Append('\n');
            sb.Append("level ").Append(p.Level.ToString(CultureInfo.InvariantCulture)).Append('\n');
            sb.Append("xp ").Append(p.Experience.ToString(CultureInfo.InvariantCulture)).Append('\n');
            foreach (EquipSlot slot in (EquipSlot[])Enum.GetValues(typeof(EquipSlot)))
            {
                Item item = p.Equipped(slot);
                if (item != null) Write(sb, item, "slot:" + slot, 0, 0, false);
            }
            foreach (GridContainer g in p.Pockets) WriteGrid(sb, g, g.Tag);
            WriteGrid(sb, p.Stash, p.Stash.Tag);
            return sb.ToString();
        }

        private static void WriteGrid(StringBuilder sb, GridContainer grid, string where)
        {
            foreach (Placement pl in grid.Placements) Write(sb, pl.Item, where, pl.X, pl.Y, pl.Rotated);
        }

        private static void Write(StringBuilder sb, Item item, string where, int x, int y, bool rot)
        {
            sb.Append("item ").Append(item.Uid).Append(' ').Append(item.Def.Id).Append(' ').Append(item.Count).Append(' ')
              .Append(where).Append(' ').Append(x).Append(' ').Append(y).Append(' ').Append(rot ? '1' : '0').Append(' ')
              .Append(string.IsNullOrEmpty(item.Build) ? "-" : item.Build.Replace(' ', '_')).Append('\n');
            for (int i = 0; i < item.Grids.Length; i++) WriteGrid(sb, item.Grids[i], "in:" + item.Uid + ":" + i);
        }

        /// <summary>Loads a profile; returns null if the text is not a profile at all.</summary>
        public static Profile Load(string text, ItemDatabase db)
        {
            if (string.IsNullOrEmpty(text)) return null;
            string[] lines = text.Replace("\r", "").Split('\n');
            if (lines.Length == 0 || lines[0].Trim() != Header) return null;
            var p = new Profile(db);
            var byUid = new Dictionary<int, Item>();
            for (int n = 1; n < lines.Length; n++)
            {
                string line = lines[n];
                if (line.StartsWith("nick ", StringComparison.Ordinal)) { p.Nickname = line.Substring(5); continue; }
                if (line.StartsWith("level ", StringComparison.Ordinal)) { int.TryParse(line.Substring(6), out p.Level); continue; }
                if (line.StartsWith("xp ", StringComparison.Ordinal)) { int.TryParse(line.Substring(3), out p.Experience); continue; }
                if (!line.StartsWith("item ", StringComparison.Ordinal)) continue;
                string[] f = line.Split(' ');
                if (f.Length < 9) continue;
                ItemDef def = db.Get(f[2]);
                if (def == null || !int.TryParse(f[1], out int uid) || !int.TryParse(f[3], out int count)) continue;
                int.TryParse(f[5], out int x);
                int.TryParse(f[6], out int y);
                bool rot = f[7] == "1";
                Item item = p.CreateWithUid(uid, def, Math.Min(Math.Max(1, count), def.MaxStack));
                if (f[8] != "-") item.Build = f[8];
                if (!Place(p, item, f[4], x, y, rot, byUid)) p.StoreInStash(item);
                byUid[uid] = item;
            }
            return p;
        }

        private static bool Place(Profile p, Item item, string where, int x, int y, bool rot, Dictionary<int, Item> byUid)
        {
            if (where.StartsWith("slot:", StringComparison.Ordinal))
                return Enum.TryParse(where.Substring(5), out EquipSlot slot) && p.Equip(item, slot);
            GridContainer grid;
            if (where.StartsWith("in:", StringComparison.Ordinal))
            {
                string[] parts = where.Split(':');
                if (parts.Length != 3 || !int.TryParse(parts[1], out int owner) || !int.TryParse(parts[2], out int index)) return false;
                if (!byUid.TryGetValue(owner, out Item parent) || index < 0 || index >= parent.Grids.Length) return false;
                grid = parent.Grids[index];
            }
            else grid = p.RootGrid(where);
            if (grid == null || !grid.CanPlace(item.Def, x, y, rot)) return false;
            p.Attach(item, MoveTarget.ToCell(grid, x, y, rot));
            return true;
        }
    }
}
