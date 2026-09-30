using System.Collections;
using Polykov.Inventory;
using Polykov.UI.Framework;
using Polykov.Weapons;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Polykov.Lobby
{
    /// <summary>
    /// Host of the lobby: builds the 3D stage (character on its stand, weapon display), loads/saves the profile and
    /// runs the engine-free <see cref="LobbyScreens"/> (Escape-from-Tarkov-like menus, inventory, armorer) on a
    /// <see cref="UiSurface"/>. Frames the camera for whatever the screens show and loads the raid scene.
    /// </summary>
    public sealed class LobbyController : MonoBehaviour
    {
        public const string LobbySceneName = "Lobby";

        [SerializeField] private WeaponDefinition definition;
        [SerializeField] private GameObject weaponModelPrefab;
        [SerializeField] private Material weaponMaterial;
        [SerializeField] private Material outlineMaterial;
        [Tooltip("AK-74N model with every modding variant in place (Art/Weapons/AK74N/AK74N.fbx).")]
        [SerializeField] private GameObject akModelPrefab;
        [SerializeField] private Material akMaterial;
        [SerializeField] private GameObject characterModel;
        [SerializeField] private RuntimeAnimatorController characterController;
        [SerializeField] private Material characterMaterial;
        [SerializeField] private string gameScene = "MovementTestArena";
        [SerializeField] private Camera stageCamera;

        private const float CharacterFov = 30f;
        private const float ArmorerFov = 28f;
        // Share of the screen width the weapon fills at zoom 1 in the armorer.
        private const float WeaponScreenShare = 0.36f;

        private LobbyStage _stage;
        private LobbyContext _ctx;
        private LobbyScreens _screens;
        private UiSurface _surface;
        private UnityItemIcons _icons;
        private Item _displayed;
        private bool _loading;

        // Camera state
        private float _yaw = -90f, _pitch = 8f, _zoom = 1f;
        private float _characterYaw = 165f;
        private Vector3 _cameraPosition, _lookAt;
        private float _fov = CharacterFov;
        private UiRect _characterArea;
        private float _uiWidth = 1920f, _uiHeight = 1080f;
        private bool _snapCamera = true;

        private void Awake()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (stageCamera == null) stageCamera = Camera.main;
            if (stageCamera != null)
            {
                stageCamera.clearFlags = CameraClearFlags.SolidColor;
                stageCamera.backgroundColor = new Color(UiTheme.Background.R, UiTheme.Background.G, UiTheme.Background.B);
            }

            _stage = new LobbyStage(definition, weaponModelPrefab, weaponMaterial, outlineMaterial, characterModel,
                characterController, characterMaterial);
            _stage.AddModularWeapon("ak74n", akModelPrefab, akMaterial);
            _stage.BuildFloor(characterMaterial, stageCamera != null ? stageCamera.backgroundColor : Color.black);
            _stage.BuildBackdrop(characterMaterial);
            BuildLights();

            // The pistol family comes from the asset (its stats, attachments and factory build); the rest use the code defaults.
            WeaponFamilies.Register(definition.ToFamily());
            var db = ItemDatabase.Default();
            Profile profile = ProfileStore.Load(db);
            if (profile == null)
            {
                WeaponBuild saved = WeaponBuildStore.Load(definition);
                if (!LoadoutRules.Validate(saved, _stage.Catalog).IsValid) saved = definition.DefaultBuild;
                profile = StarterKit.Create(db, saved, _stage.Catalog);
                ProfileStore.Save(profile);
            }

            if (StarterKit.GrantMissing(profile)) ProfileStore.Save(profile);

            _icons = new UnityItemIcons(definition, weaponModelPrefab, weaponMaterial, outlineMaterial, akModelPrefab, akMaterial);
            _ctx = new LobbyContext
            {
                Profile = profile, Db = db, Icons = _icons, SlotAnchor = SlotAnchor,
            };
            _screens = new LobbyScreens(_ctx);

            // The holster pistol is what the match uses.
            Item pistol = profile.Equipped(EquipSlot.Holster);
            if (pistol != null) WeaponBuildStore.Save(definition, WeaponParts.BuildOf(pistol, definition.DefaultBuild));
            ShowWeapon(pistol);

            _surface = gameObject.AddComponent<UiSurface>();
            _surface.Build += OnUi;
        }

        private void OnDestroy()
        {
            if (_surface != null) _surface.Build -= OnUi;
            _stage?.Destroy();
            _icons?.Dispose();
        }

        private void BuildLights()
        {
            // A warm key from above-front and a cool rim from behind, on top of the scene's directional light.
            Light key = AddLight("KeyLight", LightType.Spot, new Vector3(-1.4f, 3.2f, -2.2f), new Vector3(58f, 24f, 0f), new Color(1f, 0.93f, 0.8f), 5.5f, 9f, 55f);
            key.shadows = LightShadows.Soft;
            key.shadowStrength = 0.75f;
            AddLight("RimLight", LightType.Spot, new Vector3(1.6f, 2.4f, 2.4f), new Vector3(35f, 220f, 0f), new Color(0.55f, 0.7f, 1f), 3.2f, 8f, 60f);
            AddLight("WeaponKey", LightType.Spot, LobbyStage.WeaponOrigin + new Vector3(0.9f, 1.4f, -1.0f), new Vector3(50f, -40f, 0f), new Color(1f, 0.94f, 0.82f), 4.5f, 6f, 60f);
            AddLight("WeaponFill", LightType.Point, LobbyStage.WeaponOrigin + new Vector3(-0.9f, 0.4f, -0.8f), Vector3.zero, new Color(0.6f, 0.7f, 0.9f), 1.2f, 4f, 0f);
        }

        private Light AddLight(string name, LightType type, Vector3 position, Vector3 euler, Color color, float intensity, float range, float angle)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(euler));
            var light = go.AddComponent<Light>();
            light.type = type;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            if (type == LightType.Spot) light.spotAngle = angle;
            light.shadows = LightShadows.None;
            return light;
        }

        // ------------------------------------------------------------------ UI

        private void OnUi(Ui ui)
        {
            _ctx.Clock = System.DateTime.Now.ToString("HH:mm");
            _uiWidth = ui.Width;
            _uiHeight = ui.Height;
            _screens.Frame(ui);
            _characterArea = _screens.View == LobbyView.Character ? _screens.Inventory.CharacterArea : _screens.MainMenuCharacterArea(ui);

            LobbyRequests req = _screens.Requests;
            if (req.SaveProfile) ProfileStore.Save(_ctx.Profile);
            if (req.BuildChanged != null && req.BuildChanged == _ctx.Profile.Equipped(EquipSlot.Holster))
                WeaponBuildStore.Save(definition, WeaponParts.BuildOf(req.BuildChanged, definition.DefaultBuild));
            if (req.OrbitDrag.X != 0f || req.OrbitDrag.Y != 0f)
            {
                _yaw += req.OrbitDrag.X * 0.45f;
                _pitch = Mathf.Clamp(_pitch + req.OrbitDrag.Y * 0.3f, -30f, 70f);
            }
            if (req.OrbitZoom != 0f) _zoom = Mathf.Clamp(_zoom + req.OrbitZoom * 0.06f, 0.55f, 2f);
            if (req.EnterGame) EnterGame();
            req.Clear();

            // The 3D display follows the weapon being modded (or the pistol).
            Item shown = _screens.View == LobbyView.Armorer ? _screens.Armorer.Weapon : _ctx.Profile.Equipped(EquipSlot.Holster);
            if (shown != _displayed || (shown != null && shown.Build != _displayedBuild)) ShowWeapon(shown);
        }

        private string _displayedBuild;

        private void ShowWeapon(Item weapon)
        {
            _displayed = weapon;
            _displayedBuild = weapon?.Build;
            WeaponFamily family = _ctx.FamilyOf(weapon);
            _stage.Mount(family, weapon != null ? WeaponParts.BuildOf(weapon, family.FactoryBuild) : family.FactoryBuild);
        }

        private UiVec? SlotAnchor(AttachmentSlot slot)
        {
            if (stageCamera == null || _stage.Weapon == null || !_stage.ShowingModel || _screens == null || _screens.View != LobbyView.Armorer) return null;
            Vector3 screen = stageCamera.WorldToScreenPoint(_stage.AnchorFor(slot));
            if (screen.z <= 0f) return null;
            return _surface.FromScreen(screen);
        }

        // ------------------------------------------------------------------ camera

        private void LateUpdate()
        {
            if (stageCamera == null) return;
            LobbyView view = _screens.View;
            if (view == LobbyView.Armorer)
            {
                // Framed on the current build: a suppressor moves the pivot forward and the camera back so it all fits.
                Vector3 pivot = _stage.WeaponPivot;
                float halfWidth = Mathf.Tan(ArmorerFov * 0.5f * Mathf.Deg2Rad) * Mathf.Max(stageCamera.aspect, 1f);
                float distance = _stage.WeaponSize / (2f * halfWidth * WeaponScreenShare) * _zoom;
                _lookAt = pivot;
                _cameraPosition = pivot + Quaternion.Euler(_pitch, _yaw, 0f) * new Vector3(0f, 0f, -distance);
                _fov = ArmorerFov;
            }
            else
            {
                StageFraming.Shot shot = StageFraming.Character(_characterArea, _uiWidth, _uiHeight, CharacterFov);
                _cameraPosition = new Vector3(shot.PosX, shot.PosY, shot.PosZ);
                _lookAt = new Vector3(shot.LookX, shot.LookY, shot.LookZ);
                _fov = shot.Fov;
                // Face the camera.
                float target = Mathf.Atan2(_cameraPosition.x, _cameraPosition.z) * Mathf.Rad2Deg;
                _characterYaw = Mathf.MoveTowardsAngle(_characterYaw, target, 120f * Time.deltaTime);
                _stage.SetCharacterYaw(_characterYaw);
            }

            float t = _snapCamera ? 1f : 1f - Mathf.Exp(-9f * Time.unscaledDeltaTime);
            _snapCamera = false;
            Transform c = stageCamera.transform;
            c.position = Vector3.Lerp(c.position, _cameraPosition, t);
            c.rotation = Quaternion.Slerp(c.rotation, Quaternion.LookRotation(_lookAt - _cameraPosition, Vector3.up), t);
            stageCamera.fieldOfView = Mathf.Lerp(stageCamera.fieldOfView, _fov, t);
            if (view == LobbyView.Armorer) _stage.PlaceBackdrop(c.position);
        }

        // ------------------------------------------------------------------ scene flow

        private void EnterGame()
        {
            // What goes into the raid: the equipped primary (Tarkov: long gun in hands), else the holster pistol.
            Item inHands = _ctx.Profile.Equipped(EquipSlot.Primary) ?? _ctx.Profile.Equipped(EquipSlot.Holster);
            if (inHands != null)
            {
                WeaponFamily family = _ctx.FamilyOf(inHands);
                RaidLoadout.Set(family.Id, WeaponParts.BuildOf(inHands, family.FactoryBuild));
            }
            else RaidLoadout.Clear();
            if (_loading) return;
            if (!Application.CanStreamedLevelBeLoaded(gameScene))
            {
                Debug.LogError("La escena '" + gameScene + "' no está en Build Settings (File > Build Profiles > Scene List).");
                return;
            }
            _loading = true;
            ProfileStore.Save(_ctx.Profile);
            StartCoroutine(LoadGame());
        }

        private IEnumerator LoadGame()
        {
            AsyncOperation op = SceneManager.LoadSceneAsync(gameScene);
            while (op != null && !op.isDone) yield return null;
        }
    }
}
