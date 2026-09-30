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
        /// <summary>Orbit pivot and a comfortable viewing distance, measured on the factory build.</summary>
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

            // Measure the factory build once so the pivot does not jump around when parts are added.
            Mount(_definition.DefaultBuild);
            Bounds bounds = Measure();
            WeaponPivot = bounds.center;
            WeaponSize = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
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

        public void SetCharacterYaw(float degrees) => Character.rotation = Quaternion.Euler(0f, degrees, 0f);

        public void Destroy()
        {
            if (Character != null) Object.Destroy(Character.gameObject);
            if (_weaponRoot != null) Object.Destroy(_weaponRoot.gameObject);
        }
    }
}
