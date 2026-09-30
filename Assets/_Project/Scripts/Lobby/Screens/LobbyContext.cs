using System;
using Polykov.Inventory;
using Polykov.UI.Framework;
using Polykov.Weapons;

namespace Polykov.Lobby
{
    /// <summary>Icons for items: rendered from the item's 3D model by the host when it has one, else a pictogram.</summary>
    public interface IItemIcons
    {
        /// <summary>Icon for the item's footprint (rotated = drawn turned 90 degrees). Never null.</summary>
        UiImage Get(Item item, bool rotated);
    }

    /// <summary>Pictograms only (offline previews and hosts without renderable models).</summary>
    public sealed class PictogramIcons : IItemIcons
    {
        public UiImage Get(Item item, bool rotated) => ItemPictograms.Get(item.Def, rotated);
    }

    /// <summary>Which 3D shot the host camera should frame for the current screen.</summary>
    public enum LobbyView : byte { MainMenu, Character, Armorer }

    /// <summary>Everything the lobby screens need from the game, as plain data (no engine types).</summary>
    public sealed class LobbyContext
    {
        public Profile Profile;
        public ItemDatabase Db;
        /// <summary>Weapon family by id (<c>ItemDef.WeaponId</c>); hosts may override it to serve families built from their own assets.</summary>
        public Func<string, WeaponFamily> Family = WeaponFamilies.ById;
        public IItemIcons Icons = new PictogramIcons();
        /// <summary>Screen point of a modding slot on the 3D weapon (armorer lines); null if not visible.</summary>
        public Func<AttachmentSlot, UiVec?> SlotAnchor = _ => null;
        /// <summary>Screen rect the 3D weapon occupies in the armorer (for the orbit area / clicks). </summary>
        public string Clock = "12:00";
        public string Version = "v0.1 · Fase 1";

        /// <summary>The family of a weapon item (the M1911 one if the weapon's family is unknown, so screens never see null).</summary>
        public WeaponFamily FamilyOf(Item weapon)
        {
            WeaponFamily f = weapon != null && weapon.Def.WeaponId != null ? Family(weapon.Def.WeaponId) : null;
            return f ?? Family(WeaponFamilies.M1911Id) ?? WeaponFamilies.M1911();
        }
    }

    /// <summary>What the screens asked the host to do this frame (the host clears it after acting).</summary>
    public sealed class LobbyRequests
    {
        public bool EnterGame;
        /// <summary>The inventory changed: save the profile.</summary>
        public bool SaveProfile;
        /// <summary>A weapon's build changed (mount it on the 3D display, persist it for the match).</summary>
        public Item BuildChanged;
        /// <summary>Armorer orbit input (pixels dragged, wheel steps) inside the free area around the weapon.</summary>
        public UiVec OrbitDrag;
        public float OrbitZoom;

        public void Clear()
        {
            EnterGame = SaveProfile = false;
            BuildChanged = null;
            OrbitDrag = default;
            OrbitZoom = 0f;
        }
    }

    public static class Format
    {
        /// <summary>1234567 -> "1 234 567".</summary>
        public static string Thousands(int value)
        {
            string s = Math.Abs(value).ToString();
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                if (i > 0 && (s.Length - i) % 3 == 0) sb.Append(' ');
                sb.Append(s[i]);
            }
            return (value < 0 ? "-" : "") + sb;
        }

        public static string Kg(float kg) => kg.ToString("0.00") + " kg";
    }
}
