using System;
using UnityEngine;

namespace Polykov.Weapons
{
    /// <summary>Another attachment that must be equipped for this one to work (e.g. suppressor requires the threaded barrel).</summary>
    [Serializable]
    public struct SlotRequirement
    {
        public AttachmentSlot Slot;
        public string AttachmentId;

        public SlotRequirement(AttachmentSlot slot, string attachmentId)
        {
            Slot = slot;
            AttachmentId = attachmentId;
        }
    }

    /// <summary>Pure data of one attachment: where it goes, what it needs and how it changes the weapon.</summary>
    [Serializable]
    public struct AttachmentRules
    {
        public string Id;
        public AttachmentSlot Slot;
        [Tooltip("Attachments (in other slots) that must be equipped. Equipping this one equips them automatically.")]
        public SlotRequirement[] Requires;

        [Header("Modifiers")]
        [Tooltip("Added to ergonomics.")]
        public float ErgonomicsAdd;
        [Tooltip("Multiplies recoil (1 = unchanged). A value <= 0 counts as 1.")]
        [Min(0f)] public float RecoilMultiplier;
        [Tooltip("Added to weight (kg).")]
        public float WeightAddKg;
        [Tooltip("Added to length (m).")]
        public float LengthAddM;
        [Tooltip("Multiplies gunshot loudness (1 = unchanged). A value <= 0 counts as 1.")]
        [Min(0f)] public float LoudnessMultiplier;
        [Tooltip("Multiplies muzzle flash size (1 = unchanged). A value <= 0 counts as 1.")]
        [Min(0f)] public float MuzzleFlashMultiplier;

        /// <summary>Attachment with no modifiers and no requirements.</summary>
        public static AttachmentRules Neutral(string id, AttachmentSlot slot) => new AttachmentRules
        {
            Id = id,
            Slot = slot,
            RecoilMultiplier = 1f,
            LoudnessMultiplier = 1f,
            MuzzleFlashMultiplier = 1f,
        };

        public float Recoil => RecoilMultiplier > 0f ? RecoilMultiplier : 1f;
        public float Loudness => LoudnessMultiplier > 0f ? LoudnessMultiplier : 1f;
        public float MuzzleFlash => MuzzleFlashMultiplier > 0f ? MuzzleFlashMultiplier : 1f;
    }
}
