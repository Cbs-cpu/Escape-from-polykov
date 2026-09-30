using Polykov.Input;
using Polykov.Inventory;
using Polykov.Lobby;
using Polykov.Player;
using Polykov.UI.Framework;
using Polykov.Weapons;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Polykov.UI
{
    /// <summary>
    /// In-raid inventory (Tab), Tarkov style: the lobby's <see cref="InventoryView"/> with the stash locked. The game
    /// keeps running; the cursor is freed and gameplay input blocked through <see cref="PlayerCursor.OverlayOpen"/>.
    /// Tab or Esc closes it (Esc does not also open the pause menu). Saves the profile on close if it changed.
    /// </summary>
    [DefaultExecutionOrder(-300)]
    public sealed class RaidInventory : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerCursor cursor;
        [Tooltip("Optional: registers the pistol family built from this asset, so weapons show their real stats.")]
        [SerializeField] private WeaponDefinition definition;

        /// <summary>Raised on close when the equipped Primary/Holster weapon or its build changed.</summary>
        public event System.Action<Profile> LoadoutChanged;

        public bool IsOpen { get; private set; }
        public Profile Profile => _ctx?.Profile;

        private LobbyContext _ctx;
        private LobbyRequests _req;
        private InventoryView _view;
        private UiSurface _surface;
        private bool _dirty;
        private Item _primary, _holster;
        private string _primaryBuild, _holsterBuild;

        private void Start()
        {
            LoadoutChanged += RemountWeaponInHands;
            if (definition != null) WeaponFamilies.Register(definition.ToFamily());
            var db = ItemDatabase.Default();
            Profile profile = ProfileStore.Load(db);
            if (profile == null)
            {
                WeaponBuild build = definition != null ? definition.DefaultBuild : default;
                profile = StarterKit.Create(db, build, WeaponFamilies.ById(WeaponFamilies.M1911Id).Catalog);
                ProfileStore.Save(profile);
            }
            if (StarterKit.GrantMissing(profile)) ProfileStore.Save(profile);

            _ctx = new LobbyContext { Profile = profile, Db = db, Icons = new BakedItemIcons() };
            _req = new LobbyRequests();
            _view = new InventoryView(_ctx, _req) { RaidMode = true, OpenArmorer = _ => { } };
            _surface = gameObject.AddComponent<UiSurface>();
            _surface.Depth = -5;
            _surface.Build += OnUi;
            Snapshot();
        }

        private void OnDestroy()
        {
            if (_surface != null) _surface.Build -= OnUi;
            if (IsOpen && cursor != null) cursor.OverlayOpen = false;
        }

        private void Update()
        {
            if (_ctx == null) return;
            var kb = Keyboard.current;
            if (kb == null) return;
            if (!IsOpen)
            {
                if (input.InventoryPressedThisFrame && !cursor.Paused) Open();
            }
            else if (input.InventoryPressedThisFrame || kb.escapeKey.wasPressedThisFrame)
            {
                Close();
            }
        }

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            cursor.OverlayOpen = true;
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            cursor.OverlayOpen = false;
            if (_dirty)
            {
                ProfileStore.Save(_ctx.Profile);
                _dirty = false;
            }
            if (LoadoutDiffers())
            {
                Snapshot();
                LoadoutChanged?.Invoke(_ctx.Profile);
            }
        }

        private void OnUi(Ui ui)
        {
            if (!IsOpen) return;
            ui.Fill(new UiRect(0f, 0f, ui.Width, ui.Height), UiColor.Hex(0x000000, 0.72f));
            var area = new UiRect(12f, 60f, ui.Width - 24f, ui.Height - 84f);
            ui.Label(new UiRect(24f, 14f, ui.Width - 48f, 36f), "INVENTARIO", UiTheme.SizeLabel, UiFont.Bold, UiAlign.Left, UiTheme.TextBright);
            _view.Frame(ui, area);
            if (_req.SaveProfile) _dirty = true;
            _req.Clear();
            // Esc closes the inventory only after the view consumed it (open windows / menus / drags take it first).
            if (ui.Input.KeyPressed(UiKey.Escape)) ui.Input.ConsumeKey(UiKey.Escape);
        }

        private void Snapshot()
        {
            _primary = _ctx.Profile.Equipped(EquipSlot.Primary);
            _holster = _ctx.Profile.Equipped(EquipSlot.Holster);
            _primaryBuild = _primary?.Build;
            _holsterBuild = _holster?.Build;
        }

        private bool LoadoutDiffers()
        {
            Item p = _ctx.Profile.Equipped(EquipSlot.Primary), h = _ctx.Profile.Equipped(EquipSlot.Holster);
            return p != _primary || h != _holster || p?.Build != _primaryBuild || h?.Build != _holsterBuild;
        }
    
        /// <summary>
        /// Parts swapped in the inventory show up on the weapon in hands (same weapon family). Switching to a
        /// different weapon needs the weapon-swap step and is not done here.
        /// </summary>
        private void RemountWeaponInHands(Profile profile)
        {
            PlayerWeapon weapon = GetComponent<PlayerWeapon>();
            if (weapon == null || weapon.Definition == null) return;
            foreach (EquipSlot slot in new[] { EquipSlot.Primary, EquipSlot.Holster })
            {
                Item item = profile.Equipped(slot);
                if (item == null || item.Def.WeaponId != weapon.Definition.FamilyId) continue;
                WeaponFamily family = WeaponFamilies.ById(item.Def.WeaponId);
                weapon.ApplyBuild(WeaponParts.BuildOf(item, family != null ? family.FactoryBuild : weapon.Definition.DefaultBuild));
                return;
            }
        }
    }
}
