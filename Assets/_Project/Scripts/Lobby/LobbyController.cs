using System.Collections;
using System.Collections.Generic;
using Polykov.Weapons;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Polykov.Lobby
{
    /// <summary>
    /// The lobby: main screen (character, equipment, Enter game / Armorer) and the weapon armorer, in the style of the
    /// Escape from Tarkov menus. Logic lives in <see cref="ArmorerModel"/>; this class is the view and the scene flow.
    /// The chosen build is saved (<see cref="WeaponBuildStore"/>) and applied by <c>PlayerWeapon</c> in the match.
    /// </summary>
    public sealed class LobbyController : MonoBehaviour
    {
        public const string LobbySceneName = "Lobby";

        [SerializeField] private WeaponDefinition definition;
        [SerializeField] private GameObject weaponModelPrefab;
        [SerializeField] private Material weaponMaterial;
        [SerializeField] private Material outlineMaterial;
        [SerializeField] private GameObject characterModel;
        [SerializeField] private RuntimeAnimatorController characterController;
        [SerializeField] private Material characterMaterial;
        [SerializeField] private string gameScene = "MovementTestArena";
        [SerializeField] private Camera stageCamera;

        private enum Screen { Main, Armorer }

        private LobbyStage _stage;
        private Screen _screen = Screen.Main;
        private WeaponBuild _build;
        private WeaponBuild _factory;
        private AttachmentSlot _selectedSlot = AttachmentSlot.Muzzle;
        private bool _slotChosen;

        // Camera
        private float _yaw = -90f, _pitch = 8f, _distance = 0.75f;
        private float _characterYaw = 165f;
        private Vector3 _cameraPosition, _lookAt;
        private float _fov = 30f;
        private Rect _orbitArea;

        // Flow
        private float _fade;
        private bool _loading;
        private string _message;
        private float _messageUntil;
        private float _savedFlash;

        private static readonly Vector3 MainCameraPosition = new Vector3(0f, 1.32f, -3.55f);
        private static readonly Vector3 MainLookAt = new Vector3(0f, 0.98f, 0f);

        private void Awake()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (stageCamera == null) stageCamera = Camera.main;
            if (stageCamera != null)
            {
                stageCamera.clearFlags = CameraClearFlags.SolidColor;
                stageCamera.backgroundColor = LobbyTheme.Background;
                stageCamera.fieldOfView = _fov;
            }

            _stage = new LobbyStage(definition, weaponModelPrefab, weaponMaterial, outlineMaterial, characterModel,
                characterController, characterMaterial);
            _factory = definition.DefaultBuild;
            _build = WeaponBuildStore.Load(definition);
            if (!LoadoutRules.Validate(_build, _stage.Catalog).IsValid) _build = _factory;
            _stage.Mount(_build);
            BuildLights();

            _cameraPosition = MainCameraPosition;
            _lookAt = MainLookAt;
            ApplyCamera(1f);
        }

        private void OnDestroy() => _stage?.Destroy();

        private void BuildLights()
        {
            // A warm key from above-front and a cool rim from behind, on top of the scene's directional light.
            AddLight("KeyLight", LightType.Spot, new Vector3(-1.4f, 3.2f, -2.2f), new Vector3(58f, 24f, 0f), new Color(1f, 0.93f, 0.8f), 5.5f, 9f, 55f);
            AddLight("RimLight", LightType.Spot, new Vector3(1.6f, 2.4f, 2.4f), new Vector3(35f, 220f, 0f), new Color(0.55f, 0.7f, 1f), 3.2f, 8f, 60f);
            AddLight("WeaponKey", LightType.Spot, LobbyStage.WeaponOrigin + new Vector3(0.9f, 1.4f, -1.0f), new Vector3(50f, -40f, 0f), new Color(1f, 0.94f, 0.82f), 4.5f, 6f, 60f);
            AddLight("WeaponFill", LightType.Point, LobbyStage.WeaponOrigin + new Vector3(-0.9f, 0.4f, -0.8f), Vector3.zero, new Color(0.6f, 0.7f, 0.9f), 1.2f, 4f, 0f);
        }

        private void AddLight(string name, LightType type, Vector3 position, Vector3 euler, Color color, float intensity, float range, float angle)
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
        }

        // ------------------------------------------------------------------------------------------------ camera

        private void LateUpdate()
        {
            if (stageCamera == null) return;
            if (_screen == Screen.Main)
            {
                _characterYaw = Mathf.MoveTowardsAngle(_characterYaw, 165f, 90f * Time.deltaTime);
                _stage.SetCharacterYaw(_characterYaw);
                _cameraPosition = MainCameraPosition;
                _lookAt = MainLookAt;
                _fov = 30f;
            }
            else
            {
                Vector3 pivot = _stage.WeaponPivot;
                _lookAt = pivot;
                _cameraPosition = pivot + Quaternion.Euler(_pitch, _yaw, 0f) * new Vector3(0f, 0f, -_distance);
                _fov = 28f;
            }
            ApplyCamera(1f - Mathf.Exp(-9f * Time.unscaledDeltaTime));
        }

        private void ApplyCamera(float t)
        {
            Transform c = stageCamera.transform;
            c.position = Vector3.Lerp(c.position, _cameraPosition, t);
            Quaternion look = Quaternion.LookRotation(_lookAt - _cameraPosition, Vector3.up);
            c.rotation = Quaternion.Slerp(c.rotation, look, t);
            stageCamera.fieldOfView = Mathf.Lerp(stageCamera.fieldOfView, _fov, t);
        }

        // ------------------------------------------------------------------------------------------------ IMGUI

        private void OnGUI()
        {
            float scale = Mathf.Max(1f, UnityEngine.Screen.height / 1080f);
            Matrix4x4 saved = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float w = UnityEngine.Screen.width / scale, h = UnityEngine.Screen.height / scale;

            if (_screen == Screen.Main) DrawMain(w, h);
            else DrawArmorer(w, h, scale);

            DrawTopBar(w);
            DrawOverlay(w, h);
            HandleKeys();
            GUI.matrix = saved;
        }

        private void HandleKeys()
        {
            Event e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape && _screen == Screen.Armorer && !_loading)
            {
                _screen = Screen.Main;
                e.Use();
            }
        }

        private void DrawTopBar(float w)
        {
            LobbyTheme.Fill(new Rect(0, 0, w, 64f), new Color(0.05f, 0.052f, 0.055f, 0.97f));
            LobbyTheme.Fill(new Rect(0, 64f, w, 1f), LobbyTheme.Border);
            GUI.Label(new Rect(28f, 12f, 600f, 40f), "ESCAPE FROM POLYKOV", LobbyTheme.Title);
            string where = _screen == Screen.Main ? "MENÚ PRINCIPAL" : "ARMERO  ·  " + definition.DisplayName;
            LobbyTheme.Text_(new Rect(w - 520f, 12f, 490f, 40f), where, LobbyTheme.Right, LobbyTheme.Accent);
        }

        // ------------------------------------------------------------------------------------------------ main screen

        private void DrawMain(float w, float h)
        {
            float x = 40f, y = 110f, bw = 330f, bh = 68f, gap = 12f;
            if (LobbyTheme.Button(new Rect(x, y, bw, bh + 8f), "ENTRAR AL JUEGO", ButtonKind.Primary, "Arena de pruebas")) EnterGame();
            y += bh + 8f + gap;
            if (LobbyTheme.Button(new Rect(x, y, bw, bh), "ARMERO", ButtonKind.Normal, "Modificar la " + definition.DisplayName))
            {
                _screen = Screen.Armorer;
                _slotChosen = false;
            }
            y += bh + gap * 2f;
            foreach (string locked in new[] { "PERSONAJE", "ALIJO", "COMERCIANTES", "MISIONES" })
            {
                LobbyTheme.Button(new Rect(x, y, bw, 52f), locked, ButtonKind.Locked, "Próximamente");
                y += 52f + gap;
            }

            DrawEquipment(w, h);
            LobbyTheme.Text_(new Rect(40f, h - 44f, 700f, 28f), "Nivel 1  ·  Sin conexión  ·  v0.1 (Fase 1)", LobbyTheme.Small, LobbyTheme.TextDim);
        }

        private void DrawEquipment(float w, float h)
        {
            // Slots around the character (the character itself is rendered by the 3D camera in the middle).
            string[] left = { "CABEZA", "CASCO", "CARA", "TORSO" };
            string[] right = { "CHALECO", "MOCHILA", "ARMA PRINCIPAL", "CUERPO A CUERPO" };
            float top = 150f, sh = 86f, gap = 14f, sw = 210f;
            for (int i = 0; i < left.Length; i++) Slot(new Rect(w * 0.5f - 420f, top + i * (sh + gap), sw, sh), left[i], null);
            for (int i = 0; i < right.Length; i++) Slot(new Rect(w * 0.5f + 210f, top + i * (sh + gap), sw, sh), right[i], null);
            // The pistol is the one slot with something in it.
            string label = definition.DisplayName + (_build.Muzzle != null ? "  +  silenciador" : string.Empty);
            var pistol = new Rect(w * 0.5f - 105f, h - 190f, 210f, sh + 20f);
            Slot(pistol, "PISTOLA", label);
            if (pistol.Contains(Event.current.mousePosition) && GUI.Button(pistol, GUIContent.none, GUIStyle.none)) _screen = Screen.Armorer;
        }

        private static void Slot(Rect r, string name, string content)
        {
            LobbyTheme.Fill(r, new Color(0.07f, 0.075f, 0.08f, 0.82f));
            LobbyTheme.Frame(r, content == null ? new Color(0.2f, 0.21f, 0.215f) : LobbyTheme.Accent);
            LobbyTheme.Text_(new Rect(r.x + 10f, r.y + 6f, r.width - 20f, 20f), name, LobbyTheme.Small, LobbyTheme.TextDim);
            if (content != null)
                LobbyTheme.Text_(new Rect(r.x + 10f, r.y + r.height * 0.4f, r.width - 20f, 40f), content, LobbyTheme.Label, LobbyTheme.Text);
            else
                LobbyTheme.Text_(new Rect(r.x + 10f, r.y + r.height * 0.5f, r.width - 20f, 24f), "vacío · bloqueado", LobbyTheme.Small, new Color(0.33f, 0.33f, 0.32f));
        }

        // ------------------------------------------------------------------------------------------------ armorer

        private void DrawArmorer(float w, float h, float scale)
        {
            const float leftW = 360f, rightW = 400f;
            _orbitArea = new Rect(leftW + 20f, 80f, w - leftW - rightW - 40f, h - 100f);
            HandleOrbit();

            DrawSlotsAndLines(w, h, scale);
            DrawStatsPanel(new Rect(28f, 92f, leftW, 470f));
            DrawPartsPanel(new Rect(w - rightW - 28f, 92f, rightW, h - 190f));

            // Bottom buttons
            if (LobbyTheme.Button(new Rect(28f, h - 84f, 200f, 52f), "< VOLVER")) _screen = Screen.Main;
            if (LobbyTheme.Button(new Rect(240f, h - 84f, 220f, 52f), "RESTABLECER", ButtonKind.Normal)) SetBuild(_factory);
            if (Time.unscaledTime < _savedFlash)
                LobbyTheme.Text_(new Rect(480f, h - 84f, 300f, 52f), "Configuración guardada", LobbyTheme.Small, LobbyTheme.Good);
            LobbyTheme.Text_(new Rect(_orbitArea.x, h - 40f, _orbitArea.width, 24f), "Arrastra para girar · Rueda para acercar · Esc para volver",
                LobbyTheme.Small, LobbyTheme.TextDim);
        }

        private void HandleOrbit()
        {
            Event e = Event.current;
            if (!_orbitArea.Contains(e.mousePosition)) return;
            if (e.type == EventType.MouseDrag && e.button == 0)
            {
                _yaw += e.delta.x * 0.45f;
                _pitch = Mathf.Clamp(_pitch + e.delta.y * 0.3f, -30f, 70f);
                e.Use();
            }
            else if (e.type == EventType.ScrollWheel)
            {
                float min = _stage.WeaponSize * 0.9f, max = _stage.WeaponSize * 3.2f;
                _distance = Mathf.Clamp(_distance + e.delta.y * 0.04f * _stage.WeaponSize, min, max);
                e.Use();
            }
        }

        private void DrawSlotsAndLines(float w, float h, float scale)
        {
            AttachmentSlot[] slots = { AttachmentSlot.Muzzle, AttachmentSlot.Barrel, AttachmentSlot.Grips, AttachmentSlot.Magazine };
            // Fixed places around the weapon: muzzle top right, barrel top left, grips bottom left, magazine bottom right.
            Vector2 center = new Vector2(_orbitArea.center.x, _orbitArea.center.y);
            Vector2[] boxCenters =
            {
                new Vector2(_orbitArea.xMax - 130f, _orbitArea.y + 60f),
                new Vector2(_orbitArea.x + 130f, _orbitArea.y + 60f),
                new Vector2(_orbitArea.x + 130f, _orbitArea.yMax - 90f),
                new Vector2(_orbitArea.xMax - 130f, _orbitArea.yMax - 90f),
            };

            for (int i = 0; i < slots.Length; i++)
            {
                AttachmentSlot slot = slots[i];
                Vector3 screen = stageCamera.WorldToScreenPoint(_stage.Weapon.AnchorFor(slot));
                var anchor = new Vector2(screen.x / scale, (UnityEngine.Screen.height - screen.y) / scale);
                bool selected = _slotChosen && slot == _selectedSlot;
                var box = new Rect(boxCenters[i].x - 120f, boxCenters[i].y - 30f, 240f, 60f);
                Vector2 edge = new Vector2(Mathf.Clamp(anchor.x, box.x, box.xMax), Mathf.Clamp(anchor.y, box.y, box.yMax));
                if (screen.z > 0f)
                {
                    LobbyTheme.Line(box.center, anchor, selected ? LobbyTheme.Accent : new Color(0.55f, 0.55f, 0.52f, 0.8f), selected ? 2f : 1.2f);
                    LobbyTheme.Fill(new Rect(anchor.x - 4f, anchor.y - 4f, 8f, 8f), selected ? LobbyTheme.Accent : LobbyTheme.Text);
                }
                if (LobbyTheme.Button(box, AttachmentLabels.Slot(slot), selected ? ButtonKind.Selected : ButtonKind.Normal,
                        AttachmentLabels.Part(_build.Get(slot))))
                {
                    _selectedSlot = slot;
                    _slotChosen = true;
                }
            }
        }

        private void DrawStatsPanel(Rect r)
        {
            LobbyTheme.PanelBox(r);
            GUI.Label(new Rect(r.x + 18f, r.y + 10f, r.width - 36f, 32f), "CARACTERÍSTICAS", LobbyTheme.Center == null ? LobbyTheme.Label : StatsHeader);
            LobbyTheme.Fill(new Rect(r.x + 18f, r.y + 46f, r.width - 36f, 1f), LobbyTheme.Border);

            StatRow[] rows = ArmorerModel.Stats(definition.Stats, _factory, _build, _stage.Catalog);
            float y = r.y + 58f;
            foreach (StatRow row in rows)
            {
                LobbyTheme.Text_(new Rect(r.x + 18f, y, 170f, 30f), AttachmentLabels.Stat(row.Kind), LobbyTheme.Label, LobbyTheme.Text);
                Color c = row.Verdict == StatVerdict.Better ? LobbyTheme.Good : row.Verdict == StatVerdict.Worse ? LobbyTheme.Bad : LobbyTheme.TextDim;
                LobbyTheme.Text_(new Rect(r.x + 180f, y, 100f, 30f), AttachmentLabels.Format(row.Kind, row.Current), LobbyTheme.Right, c);
                if (row.Verdict != StatVerdict.Same)
                {
                    string delta = (row.Delta > 0f ? "+" : "") + AttachmentLabels.Format(row.Kind, row.Delta);
                    LobbyTheme.Text_(new Rect(r.x + 240f, y, r.width - 258f, 30f), delta, LobbyTheme.Right, c);
                }
                y += 34f;
            }
            LobbyTheme.Fill(new Rect(r.x + 18f, y + 6f, r.width - 36f, 1f), LobbyTheme.Border);
            LobbyTheme.Text_(new Rect(r.x + 18f, y + 14f, r.width - 36f, 60f),
                "Verde: mejora respecto a la configuración de fábrica.\nRojo: empeora.", LobbyTheme.Small, LobbyTheme.TextDim);
        }

        private static GUIStyle _statsHeader;
        private static GUIStyle StatsHeader
        {
            get
            {
                if (_statsHeader != null) return _statsHeader;
                _statsHeader = new GUIStyle(LobbyTheme.Label) { fontSize = 17, fontStyle = FontStyle.Bold };
                _statsHeader.normal.textColor = LobbyTheme.Accent;
                return _statsHeader;
            }
        }

        private void DrawPartsPanel(Rect r)
        {
            LobbyTheme.PanelBox(r);
            string title = _slotChosen ? "PIEZAS · " + AttachmentLabels.Slot(_selectedSlot) : "PIEZAS";
            GUI.Label(new Rect(r.x + 18f, r.y + 10f, r.width - 36f, 32f), title, StatsHeader);
            LobbyTheme.Fill(new Rect(r.x + 18f, r.y + 46f, r.width - 36f, 1f), LobbyTheme.Border);
            if (!_slotChosen)
            {
                LobbyTheme.Text_(new Rect(r.x + 18f, r.y + 60f, r.width - 36f, 60f), "Elige un slot del arma para ver las piezas compatibles.",
                    LobbyTheme.Small, LobbyTheme.TextDim);
                return;
            }

            List<SlotOption> options = ArmorerModel.Options(_build, _stage.Catalog, _selectedSlot);
            float y = r.y + 60f;
            foreach (SlotOption option in options)
            {
                bool blocked = option.State == OptionState.Blocked;
                ButtonKind kind = option.State == OptionState.Equipped ? ButtonKind.Selected : blocked ? ButtonKind.Blocked : ButtonKind.Normal;
                string subtitle = option.State == OptionState.Equipped ? "MONTADO"
                    : blocked ? option.Reason : NoteFor(option);
                var box = new Rect(r.x + 18f, y, r.width - 36f, 66f);
                if (LobbyTheme.Button(box, AttachmentLabels.Part(option.Id), kind, subtitle) && option.State == OptionState.Available)
                    SetBuild(ArmorerModel.Select(_build, _stage.Catalog, _selectedSlot, option.Id));
                y += 76f;
            }
        }

        private static string NoteFor(SlotOption option)
        {
            var notes = new List<string>();
            if (option.AlsoMounts != null && option.AlsoMounts.Count > 0)
                notes.Add("monta también: " + string.Join(", ", Names(option.AlsoMounts)));
            if (option.Removes != null && option.Removes.Count > 0)
                notes.Add("quita: " + string.Join(", ", Names(option.Removes)));
            return notes.Count == 0 ? "Disponible" : string.Join(" · ", notes);
        }

        private static IEnumerable<string> Names(IReadOnlyList<string> ids)
        {
            foreach (string id in ids) yield return AttachmentLabels.Part(id);
        }

        private void SetBuild(WeaponBuild build)
        {
            if (build == _build) return;
            _build = build;
            _stage.Mount(_build);
            WeaponBuildStore.Save(definition, _build);
            _savedFlash = Time.unscaledTime + 1.8f;
        }

        // ------------------------------------------------------------------------------------------------ scene flow

        private void EnterGame()
        {
            if (_loading) return;
            if (!Application.CanStreamedLevelBeLoaded(gameScene))
            {
                _message = "La escena '" + gameScene + "' no está en Build Settings (File > Build Profiles > Scene List).";
                _messageUntil = Time.unscaledTime + 5f;
                return;
            }
            StartCoroutine(LoadGame());
        }

        private IEnumerator LoadGame()
        {
            _loading = true;
            while (_fade < 1f)
            {
                _fade = Mathf.MoveTowards(_fade, 1f, Time.unscaledDeltaTime * 3f);
                yield return null;
            }
            AsyncOperation op = SceneManager.LoadSceneAsync(gameScene);
            while (op != null && !op.isDone) yield return null;
        }

        private void DrawOverlay(float w, float h)
        {
            if (_message != null && Time.unscaledTime < _messageUntil)
            {
                var r = new Rect(w * 0.5f - 420f, h - 130f, 840f, 44f);
                LobbyTheme.Fill(r, new Color(0.25f, 0.06f, 0.05f, 0.95f));
                LobbyTheme.Frame(r, LobbyTheme.Bad);
                LobbyTheme.Text_(r, _message, LobbyTheme.Center, LobbyTheme.Text);
            }
            if (_fade > 0.001f)
            {
                LobbyTheme.Fill(new Rect(0, 0, w, h), new Color(0f, 0f, 0f, _fade));
                if (_loading) LobbyTheme.Text_(new Rect(0, h * 0.5f - 20f, w, 40f), "CARGANDO…", LobbyTheme.Center, new Color(LobbyTheme.Text.r, LobbyTheme.Text.g, LobbyTheme.Text.b, _fade));
            }
        }
    }
}
