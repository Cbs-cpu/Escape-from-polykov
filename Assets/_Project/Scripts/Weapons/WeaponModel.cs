using UnityEngine;

namespace Polykov.Weapons
{
    /// <summary>
    /// Named attachment points and moving parts of a weapon model. Works with the procedural placeholder
    /// (<see cref="M1911Builder"/>) or an imported model (ArtSource/Tools/build_m1911.py) with the same names.
    /// Weapon space (this transform): +Z muzzle, +Y up, origin on the frame top above the trigger.
    /// Part motions are computed in weapon space and converted to each part's parent, so they work whatever
    /// axis conversion the FBX importer applied to the intermediate nodes.
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
        public const string SafetyName = "Safety";

        /// <summary>Grip rake of the M1911 frame (degrees): the magazine slides along this axis.</summary>
        public const float GripRake = 18f;

        // Reference points of the M1911 in weapon space (see build_m1911.py), used to orient imported models.
        private static readonly Vector3 SightPoint = new Vector3(0f, 0.0395f, -0.0525f);
        private static readonly Vector3 MuzzlePoint = new Vector3(0f, 0.012f, 0.156f);
        private static readonly Vector3 GripCenterPoint = new Vector3(0f, -0.0628f, -0.0579f);

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
        /// <summary>Thumb safety lever (moves up when engaged).</summary>
        public Transform Safety;

        /// <summary>IK targets, created at runtime and posed from the weapon definition.</summary>
        public Transform RightHand { get; private set; }
        public Transform LeftHand { get; private set; }

        private Vector3 _slideRest;
        private Vector3 _slideBack;
        private Vector3 _barrelRest;
        private Vector3 _barrelBack;
        private Vector3 _magazineRest;
        private Vector3 _magazineDown;
        private Quaternion _hammerRest;
        private Vector3 _hammerAxis;
        private Quaternion _triggerRest;
        private Vector3 _triggerAxis;
        private Vector3 _safetyRest;
        private Vector3 _safetyUp;

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
            Safety = Safety != null ? Safety : Find(SafetyName);

            Barrel = FindIn(transform, "M1911_Barrel");
            EjectionPort = FindIn(transform, "Socket_EjectionPort");
            if (MagazineGrab == null && Magazine != null)
            {
                // Models without the point: the support hand takes the magazine base, next to the mag well.
                Transform well = FindIn(transform, "Socket_MagWell");
                Vector3 basePoint = well != null ? well.position : Magazine.position;
                MagazineGrab = new GameObject(MagazineGrabName).transform;
                MagazineGrab.position = basePoint + transform.TransformVector(new Vector3(-0.03f, -0.035f, 0f));
                MagazineGrab.SetParent(Magazine, true);
            }

            RightHand = new GameObject("RightHandIK").transform;
            RightHand.SetParent(transform, false);
            LeftHand = new GameObject("LeftHandIK").transform;
            LeftHand.SetParent(transform, false);

            if (Slide != null)
            {
                _slideRest = Slide.localPosition;
                _slideBack = ToParentSpace(Slide, Vector3.back);
            }
            if (Barrel != null)
            {
                _barrelRest = Barrel.localPosition;
                _barrelBack = ToParentSpace(Barrel, Vector3.back);
            }
            if (Magazine != null)
            {
                _magazineRest = Magazine.localPosition;
                _magazineDown = ToParentSpace(Magazine, Quaternion.Euler(GripRake, 0f, 0f) * Vector3.down);
            }
            if (Hammer != null)
            {
                _hammerRest = Hammer.localRotation;
                _hammerAxis = Quaternion.Inverse(Hammer.rotation) * transform.right;
            }
            if (Trigger != null)
            {
                _triggerRest = Trigger.localRotation;
                _triggerAxis = Quaternion.Inverse(Trigger.rotation) * transform.right;
            }
            if (Safety != null)
            {
                _safetyRest = Safety.localPosition;
                _safetyUp = ToParentSpace(Safety, Vector3.up);
            }
        }

        /// <summary>Thumb safety: 0 = off (fire), 1 = on (safe, lever up).</summary>
        public void PoseSafety(float engaged)
        {
            if (Safety != null) Safety.localPosition = _safetyRest + _safetyUp * (0.0035f * engaged);
        }

        /// <param name="slideBack">0 = in battery, 1 = fully back (locked or cycling).</param>
        /// <param name="magazineOut">0 = seated, 1 = fully out along the grip.</param>
        /// <param name="magazineVisible">Hide the magazine while it is "in the pouch".</param>
        /// <param name="hammerCocked">0 = down, 1 = cocked.</param>
        /// <param name="triggerPull">0..1.</param>
        public void Pose(float slideBack, float magazineOut, bool magazineVisible, float hammerCocked, float triggerPull)
        {
            if (Slide != null) Slide.localPosition = _slideRest + _slideBack * (0.03f * slideBack);
            if (Barrel != null) Barrel.localPosition = _barrelRest + _barrelBack * (0.03f * 0.3f * slideBack);
            if (Magazine != null)
            {
                Magazine.localPosition = _magazineRest + _magazineDown * (0.16f * magazineOut);
                if (Magazine.gameObject.activeSelf != magazineVisible) Magazine.gameObject.SetActive(magazineVisible);
            }
            // Positive rotation about weapon +X tips a part's top toward the muzzle.
            if (Hammer != null) Hammer.localRotation = _hammerRest * Quaternion.AngleAxis(-60f * hammerCocked, _hammerAxis);
            if (Trigger != null) Trigger.localRotation = _triggerRest * Quaternion.AngleAxis(12f * triggerPull, _triggerAxis);
        }

        /// <summary>A weapon-space offset (meters) expressed in the part's parent space.</summary>
        private Vector3 ToParentSpace(Transform part, Vector3 weaponSpaceOffset)
        {
            Vector3 world = transform.TransformVector(weaponSpaceOffset);
            return part.parent != null ? part.parent.InverseTransformVector(world) : world;
        }

        // Names used by the textured model (ArtSource/Tools/build_m1911.py): parts "M1911_*", points "Socket_*".
        private static readonly (string canonical, string alias)[] Aliases =
        {
            (SlideName, "M1911_Slide"), (MagazineName, "M1911_Magazine"), (HammerName, "M1911_Hammer"),
            (TriggerName, "M1911_Trigger"), (SafetyName, "M1911_Safety"), (MuzzleName, "Socket_Muzzle"),
            (SightName, "Socket_RearSight"), (GripCenterName, "Socket_RightHand"),
        };

        private Transform Find(string childName) => FindIn(transform, childName);

        /// <summary>Barrel (textured model): travels back with the slide for part of its stroke.</summary>
        public Transform Barrel { get; private set; }
        /// <summary>Where spent casings leave the gun (null on models without the socket).</summary>
        public Transform EjectionPort { get; private set; }

        /// <summary>
        /// Instantiates an imported weapon model under a clean weapon-space root, measuring its named points to
        /// undo any importer axis conversion or unit scale. Returns null if the model lacks the reference points.
        /// </summary>
        public static WeaponModel CreateFromModel(GameObject prefab, Transform parent, int layer, WeaponMaterials materials,
            Material overrideMaterial = null)
        {
            var root = new GameObject(prefab.name) { layer = layer };
            root.transform.SetParent(parent, false);
            GameObject instance = Instantiate(prefab, root.transform, false);
            foreach (Transform t in instance.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;

            Transform sight = FindIn(instance.transform, SightName);
            Transform muzzle = FindIn(instance.transform, MuzzleName);
            Transform grip = FindIn(instance.transform, GripCenterName);
            Transform frontSight = FindIn(instance.transform, "Socket_FrontSight");
            if (sight == null || muzzle == null || grip == null)
            {
                Destroy(root);
                return null;
            }

            // Measure the model: line of sight (rear -> front sight, or rear sight -> muzzle) becomes +Z,
            // "up" is from the grip to the rear sight, and the rear sight lands on this project's sight point.
            Transform r = root.transform;
            Vector3 rear = r.InverseTransformPoint(sight.position);
            Vector3 ahead = r.InverseTransformPoint(frontSight != null ? frontSight.position : muzzle.position);
            Vector3 gripPoint = r.InverseTransformPoint(grip.position);
            Vector3 forward = ahead - rear;
            Vector3 up = rear - gripPoint;
            Vector3 expectedForward = frontSight != null ? Vector3.forward : MuzzlePoint - SightPoint;
            Vector3 expectedUp = SightPoint - GripCenterPoint;

            // Only fix gross unit mismatches (cm vs m); real models differ from our reference by a few percent.
            float ratio = (MuzzlePoint - SightPoint).magnitude
                          / Mathf.Max((r.InverseTransformPoint(muzzle.position) - rear).magnitude, 1e-6f);
            float scale = ratio > 2f || ratio < 0.5f ? ratio : 1f;
            Quaternion correction = Quaternion.LookRotation(expectedForward, expectedUp)
                                    * Quaternion.Inverse(Quaternion.LookRotation(forward, up));
            Transform t0 = instance.transform;
            t0.localScale *= scale;
            t0.localRotation = correction * t0.localRotation;
            t0.localPosition = correction * t0.localPosition * scale;
            t0.localPosition += SightPoint - r.InverseTransformPoint(sight.position);

            if (overrideMaterial != null)
            {
                foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] slots = renderer.sharedMaterials;
                    for (int i = 0; i < slots.Length; i++) slots[i] = overrideMaterial;
                    renderer.sharedMaterials = slots;
                }
            }
            else
            {
                materials.ApplyTo(instance);
            }
            var model = root.AddComponent<WeaponModel>();
            model.Initialize();
            return model;
        }

        private static Transform FindIn(Transform root, string childName)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == childName) return t;
            foreach ((string canonical, string alias) in Aliases)
            {
                if (canonical != childName) continue;
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == alias) return t;
            }
            return null;
        }
    }

    /// <summary>Project materials for weapon models, matched by the source material names (M_Steel, M_Wood...).</summary>
    [System.Serializable]
    public struct WeaponMaterials
    {
        public Material Steel;
        public Material SteelDark;
        public Material Grip;
        public Material GripDark;
        public Material Brass;

        public Material For(string sourceName)
        {
            string n = sourceName ?? string.Empty;
            Material m = null;
            if (n.Contains("Brass")) m = Brass;
            else if (n.Contains("WoodDark")) m = GripDark;
            else if (n.Contains("Wood") || n.Contains("Grip")) m = Grip;
            else if (n.Contains("SteelDark")) m = SteelDark;
            return m != null ? m : Steel;
        }

        public void ApplyTo(GameObject model)
        {
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                Material[] slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++) slots[i] = For(slots[i] != null ? slots[i].name : null);
                renderer.sharedMaterials = slots;
            }
        }
    }
}
