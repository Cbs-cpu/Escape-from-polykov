using UnityEngine;

namespace Polykov.Weapons
{
    /// <summary>
    /// Named attachment points and moving parts of a weapon model. Works with the procedural placeholder
    /// (<see cref="M1911Builder"/>) or an imported model whose children use the same names.
    /// Weapon space: +Z muzzle, +Y up, origin on the frame top above the trigger.
    /// </summary>
    public sealed class WeaponModel : MonoBehaviour
    {
        public const string SlideName = "Slide";
        public const string MagazineName = "Magazine";
        public const string HammerName = "Hammer";
        public const string TriggerName = "Trigger";
        public const string MuzzleName = "Muzzle";
        public const string SightName = "SightLine";
        public const string GripCenterName = "GripCenter";
        public const string MagazineGrabName = "MagazineGrab";

        public Transform Slide;
        public Transform Magazine;
        public Transform Hammer;
        public Transform Trigger;
        public Transform Muzzle;
        /// <summary>Rear sight notch; its forward is the line of sight.</summary>
        public Transform Sight;
        public Transform GripCenter;
        /// <summary>Where the support hand grabs the magazine during a reload (wrist, hand frame).</summary>
        public Transform MagazineGrab;

        /// <summary>IK targets, created at runtime and posed from the weapon definition.</summary>
        public Transform RightHand { get; private set; }
        public Transform LeftHand { get; private set; }

        private Vector3 _slideRest;
        private Vector3 _magazineRest;
        private Quaternion _hammerRest;
        private Quaternion _triggerRest;

        public void Initialize()
        {
            Slide = Slide != null ? Slide : Find(SlideName);
            Magazine = Magazine != null ? Magazine : Find(MagazineName);
            Hammer = Hammer != null ? Hammer : Find(HammerName);
            Trigger = Trigger != null ? Trigger : Find(TriggerName);
            Muzzle = Muzzle != null ? Muzzle : Find(MuzzleName);
            Sight = Sight != null ? Sight : Find(SightName);
            GripCenter = GripCenter != null ? GripCenter : Find(GripCenterName);
            MagazineGrab = MagazineGrab != null ? MagazineGrab : Find(MagazineGrabName);

            RightHand = new GameObject("RightHandIK").transform;
            RightHand.SetParent(transform, false);
            LeftHand = new GameObject("LeftHandIK").transform;
            LeftHand.SetParent(transform, false);

            if (Slide != null) _slideRest = Slide.localPosition;
            if (Magazine != null) _magazineRest = Magazine.localPosition;
            if (Hammer != null) _hammerRest = Hammer.localRotation;
            if (Trigger != null) _triggerRest = Trigger.localRotation;
        }

        /// <param name="slideBack">0 = in battery, 1 = fully back (locked or cycling).</param>
        /// <param name="magazineOut">0 = seated, 1 = fully out along the grip.</param>
        /// <param name="magazineVisible">Hide the magazine while it is "in the pouch".</param>
        /// <param name="hammerCocked">0 = down, 1 = cocked.</param>
        /// <param name="triggerPull">0..1.</param>
        public void Pose(float slideBack, float magazineOut, bool magazineVisible, float hammerCocked, float triggerPull)
        {
            if (Slide != null) Slide.localPosition = _slideRest + Vector3.back * (0.03f * slideBack);
            if (Magazine != null)
            {
                Magazine.localPosition = _magazineRest + Vector3.down * (0.16f * magazineOut);
                if (Magazine.gameObject.activeSelf != magazineVisible) Magazine.gameObject.SetActive(magazineVisible);
            }
            if (Hammer != null) Hammer.localRotation = _hammerRest * Quaternion.Euler(-60f * hammerCocked, 0f, 0f);
            if (Trigger != null) Trigger.localRotation = _triggerRest * Quaternion.Euler(-12f * triggerPull, 0f, 0f);
        }

        private Transform Find(string childName)
        {
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
                if (t.name == childName) return t;
            return null;
        }
    }
}
