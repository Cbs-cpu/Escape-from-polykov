using UnityEngine;

namespace Polykov.Weapons
{
    /// <summary>Asset for one weapon attachment: pure rules plus the visual prefab.</summary>
    [CreateAssetMenu(menuName = "Polykov/Attachment Definition", fileName = "AttachmentDefinition")]
    public sealed class AttachmentDefinition : ScriptableObject
    {
        public AttachmentRules Rules = AttachmentRules.Neutral("attachment", AttachmentSlot.Muzzle);
        public GameObject Prefab;
        [Tooltip("Optional child of the prefab whose position is the new muzzle (flash / shot origin). Empty = none.")]
        public string MuzzleSocketName;
        [Tooltip("Material for the prefab's body slots (outline slots keep the weapon outline material). Empty = keep the model's own.")]
        public Material BodyMaterial;

        public string Id => Rules.Id;
        public AttachmentSlot Slot => Rules.Slot;
    }
}
