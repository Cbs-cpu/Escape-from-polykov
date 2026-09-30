using System.Collections.Generic;
using Polykov.Weapons;
using UnityEngine;

namespace Polykov.Lobby
{
    /// <summary>
    /// The 3D side of the lobby: the Operator standing in idle, and the weapon on its own display far from the
    /// character (so the armorer can orbit it freely). Everything is built from the game's own assets at runtime.
    /// </summary>
    public sealed class LobbyStage
    {
        /// <summary>World position of the weapon display (far from the character).</summary>
        public static readonly Vector3 WeaponOrigin = new Vector3(30f, 1.2f, 0f);

        private readonly WeaponDefinition _definition;
        private readonly AttachmentCatalog _catalog;
        private readonly Material _outline;
        private WeaponModel _weapon;
        private Transform _weaponRoot;
        private Animator _animator;

        public Transform Character { get; private set; }
        public WeaponModel Weapon => _weapon;
        public AttachmentCatalog Catalog => _catalog;
        public WeaponDefinition Definition => _definition;
        /// <summary>Orbit pivot and longest side of the mounted build (re-measured on every <see cref="Mount"/>).</summary>
        public Vector3 WeaponPivot { get; private set; }
        public float WeaponSize { get; private set; } = 0.35f;

        public LobbyStage(WeaponDefinition definition, GameObject weaponPrefab, Material weaponMaterial, Material outline,
            GameObject characterModel, RuntimeAnimatorController characterController, Material characterMaterial)
        {
            _definition = definition;
            _catalog = definition.BuildCatalog();
            _outline = outline;
            BuildWeapon(weaponPrefab, weaponMaterial);
            BuildCharacter(characterModel, characterController, characterMaterial);
        }

        private void BuildWeapon(GameObject prefab, Material material)
        {
            _weaponRoot = new GameObject("WeaponDisplay").transform;
            _weaponRoot.position = WeaponOrigin;
            // Slightly raised muzzle, like a display stand.
            _weaponRoot.rotation = Quaternion.Euler(-4f, 0f, 0f);
            if (prefab != null) _weapon = WeaponModel.CreateFromModel(prefab, _weaponRoot, 0, default, material, _outline);
            if (_weapon == null) _weapon = M1911Builder.Build(_weaponRoot, material, material, 0);

            Mount(_definition.DefaultBuild);
        }

        private void BuildCharacter(GameObject model, RuntimeAnimatorController controller, Material material)
        {
            Character = new GameObject("LobbyCharacter").transform;
            Character.rotation = Quaternion.Euler(0f, 165f, 0f);
            if (model == null) return;
            GameObject body = Object.Instantiate(model, Character, false);
            _animator = body.GetComponentInChildren<Animator>();
            if (_animator != null)
            {
                _animator.runtimeAnimatorController = controller;
                _animator.applyRootMotion = false;
            }
            foreach (Renderer renderer in body.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is SkinnedMeshRenderer skinned) skinned.updateWhenOffscreen = true;
                Material[] slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    bool outlineSlot = i == 1 && slots.Length > 1;
                    slots[i] = outlineSlot && _outline != null ? _outline : material != null ? material : slots[i];
                }
                renderer.sharedMaterials = slots;
            }
        }

        /// <summary>Mounts a build on the display weapon (models of the parts follow the same rules as in the match).</summary>
        public void Mount(WeaponBuild build)
        {
            var ordered = new List<AttachmentDefinition>();
            foreach (AttachmentSlot slot in new[] { AttachmentSlot.Barrel, AttachmentSlot.Muzzle, AttachmentSlot.Grips, AttachmentSlot.Magazine })
            {
                string id = build.Get(slot);
                if (id == null || _definition.Attachments == null) continue;
                foreach (AttachmentDefinition a in _definition.Attachments)
                    if (a != null && a.Id == id) { ordered.Add(a); break; }
            }
            _weapon.MountAttachments(ordered, 0, _outline);
            // The armorer camera eases to the new pivot/size, so re-framing on each change is smooth.
            Bounds bounds = Measure();
            WeaponPivot = bounds.center;
            WeaponSize = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        }

        private Bounds Measure()
        {
            Bounds bounds = default;
            bool first = true;
            foreach (Renderer r in _weapon.GetComponentsInChildren<Renderer>())
            {
                if (first) { bounds = r.bounds; first = false; }
                else bounds.Encapsulate(r.bounds);
            }
            return first ? new Bounds(WeaponOrigin, Vector3.one * 0.3f) : bounds;
        }

        /// <summary>
        /// A round stand under the character and a large dark floor that catches the key light's pool, so the
        /// character does not float in the void. Materials are copies of <paramref name="template"/> (URP Lit) without maps.
        /// </summary>
        public void BuildFloor(Material template, Color background)
        {
            if (template == null) return;
            _floor = new GameObject("LobbyFloor").transform;
            Material floor = Flat(template, Color.Lerp(background, new Color(0.16f, 0.16f, 0.15f), 0.55f), 0.1f);
            Material stand = Flat(template, new Color(0.13f, 0.13f, 0.125f), 0.3f);
            Material rim = Flat(template, new Color(0.46f, 0.4f, 0.26f), 0.4f);
            // Unity cylinder: radius 0.5, height 2. Stand top sits at the feet (y = 0), gold rim just below its edge.
            Primitive(PrimitiveType.Plane, new Vector3(0f, -0.025f, 0f), new Vector3(2f, 1f, 2f), floor);
            Primitive(PrimitiveType.Cylinder, new Vector3(0f, -0.012f, 0f), new Vector3(1.47f, 0.012f, 1.47f), rim);
            Primitive(PrimitiveType.Cylinder, new Vector3(0f, -0.01f, 0f), new Vector3(1.44f, 0.012f, 1.44f), stand);
        }

        private Transform _floor;
        private Transform _backdrop;

        /// <summary>A dark workbench grid behind the weapon (kept facing the armorer camera by <see cref="PlaceBackdrop"/>).</summary>
        public void BuildBackdrop(Material template)
        {
            if (template == null) return;
            Material grid = Flat(template, new Color(0.34f, 0.35f, 0.36f), 0.05f);
            grid.name = "LobbyGrid";
            grid.SetTexture("_BaseMap", GridTexture());
            grid.SetTextureScale("_BaseMap", new Vector2(BackdropSize / 0.2f, BackdropSize / 0.2f));
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.Destroy(go.GetComponent<Collider>());
            go.name = "WeaponBackdrop";
            go.GetComponent<Renderer>().sharedMaterial = grid;
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _backdrop = go.transform;
            _backdrop.localScale = new Vector3(BackdropSize, BackdropSize, 1f);
            _backdrop.position = WeaponOrigin + Vector3.left * BackdropDistance;
        }

        private const float BackdropSize = 5f;
        private const float BackdropDistance = 1.3f;

        /// <summary>Keeps the backdrop behind the weapon as seen from <paramref name="cameraPosition"/>.</summary>
        public void PlaceBackdrop(Vector3 cameraPosition)
        {
            if (_backdrop == null) return;
            Vector3 away = WeaponPivot - cameraPosition;
            if (away.sqrMagnitude < 1e-6f) return;
            away.Normalize();
            _backdrop.SetPositionAndRotation(WeaponPivot + away * BackdropDistance, Quaternion.LookRotation(away, Vector3.up));
        }

        /// <summary>128 px tile: a bright major line on two edges and faint minor lines every 32 px (5 cm at 20 cm/tile).</summary>
        private static Texture2D GridTexture()
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "LobbyGrid", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8,
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    byte v = x < 2 || y < 2 ? (byte)150 : x % 32 == 0 || y % 32 == 0 ? (byte)92 : (byte)60;
                    pixels[y * size + x] = new Color32(v, v, v, 255);
                }
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
        }

        private void Primitive(PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(_floor, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static Material Flat(Material template, Color color, float smoothness)
        {
            var m = new Material(template) { name = "LobbyFloor" };
            m.SetTexture("_BaseMap", null);
            m.SetTexture("_BumpMap", null);
            m.SetTexture("_MetallicGlossMap", null);
            m.DisableKeyword("_NORMALMAP");
            m.DisableKeyword("_METALLICSPECGLOSSMAP");
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", 0f);
            return m;
        }

        public void SetCharacterYaw(float degrees) => Character.rotation = Quaternion.Euler(0f, degrees, 0f);

        public void Destroy()
        {
            if (Character != null) Object.Destroy(Character.gameObject);
            if (_weaponRoot != null) Object.Destroy(_weaponRoot.gameObject);
            if (_floor != null) Object.Destroy(_floor.gameObject);
            if (_backdrop != null) Object.Destroy(_backdrop.gameObject);
        }
    }
}
