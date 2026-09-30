using System.Collections.Generic;
using UnityEngine;

namespace Polykov.Weapons
{
    /// <summary>
    /// Puts the body's real hands on the weapon: analytic two-bone IK per arm, hand orientation from grip
    /// directions, and a finger grip pose. Runs in LateUpdate after the camera and weapon are placed, so the
    /// hands never lag the gun. Hand axes are derived from the skin bind pose (no hard-coded bone axes).
    /// </summary>
    [DefaultExecutionOrder(120)]
    public sealed class ArmIK : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private WeaponPose pose;
        [SerializeField] private PlayerWeaponController weapon;
        [SerializeField] private Transform cameraTransform;
        [Tooltip("Where the left hand grabs a fresh magazine during reloads (camera space).")]
        [SerializeField] private Vector3 pouchPosition = new Vector3(-0.16f, -0.50f, 0.05f);

        private static readonly string[] FingerNames = { "Thumb", "Index", "Middle", "Ring", "Little" };

        private sealed class Hand
        {
            public Transform Upper, Lower, Wrist;
            public Vector3 FingersLocal, PalmLocal;
            public Transform[][] Fingers = new Transform[5][];
            public Quaternion[][] BindLocal = new Quaternion[5][];
            public Vector3[][] CurlAxis = new Vector3[5][];
        }

        private Hand _right, _left;
        private bool _ready;

        private void Awake()
        {
            SkinnedMeshRenderer skin = FindBodySkin();
            if (skin == null)
            {
                Debug.LogError("[ArmIK] No SkinnedMeshRenderer found under the animator.", this);
                return;
            }
            var bind = new Dictionary<Transform, Matrix4x4>();
            Transform[] bones = skin.bones;
            Matrix4x4[] poses = skin.sharedMesh.bindposes;
            for (int i = 0; i < bones.Length; i++)
                if (bones[i] != null) bind[bones[i]] = poses[i].inverse; // bone -> mesh space at bind

            _right = BuildHand(bind, false);
            _left = BuildHand(bind, true);
            _ready = _right != null && _left != null;
        }

        private Hand BuildHand(Dictionary<Transform, Matrix4x4> bind, bool left)
        {
            HumanBodyBones upper = left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm;
            var h = new Hand
            {
                Upper = animator.GetBoneTransform(upper),
                Lower = animator.GetBoneTransform(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm),
                Wrist = animator.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand),
            };
            Transform index = animator.GetBoneTransform(left ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal);
            Transform middle = animator.GetBoneTransform(left ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal);
            Transform little = animator.GetBoneTransform(left ? HumanBodyBones.LeftLittleProximal : HumanBodyBones.RightLittleProximal);
            if (h.Upper == null || h.Lower == null || h.Wrist == null || index == null || middle == null || little == null) return null;
            if (!bind.ContainsKey(h.Wrist) || !bind.ContainsKey(middle)) return null;

            Matrix4x4 wristBind = bind[h.Wrist];
            Vector3 wristPos = wristBind.GetColumn(3);
            Vector3 fingers = ((Vector3)bind[middle].GetColumn(3) - wristPos).normalized;
            Vector3 across = ((Vector3)bind[index].GetColumn(3) - (Vector3)bind[little].GetColumn(3)).normalized;
            Vector3 palm = Vector3.Cross(fingers, across).normalized * (left ? -1f : 1f); // out of the palm
            Quaternion wristRot = wristBind.rotation;
            h.FingersLocal = Quaternion.Inverse(wristRot) * fingers;
            h.PalmLocal = Quaternion.Inverse(wristRot) * palm;

            // Finger chains: curl axis bends each finger toward the palm (sign verified numerically).
            string side = left ? "Left" : "Right";
            for (int f = 0; f < 5; f++)
            {
                h.Fingers[f] = new Transform[3];
                h.BindLocal[f] = new Quaternion[3];
                h.CurlAxis[f] = new Vector3[3];
                for (int s = 0; s < 3; s++)
                {
                    string seg = s == 0 ? "Proximal" : s == 1 ? "Intermediate" : "Distal";
                    var bone = (HumanBodyBones)System.Enum.Parse(typeof(HumanBodyBones), side + FingerNames[f] + seg);
                    Transform t = animator.GetBoneTransform(bone);
                    h.Fingers[f][s] = t;
                    if (t == null || !bind.ContainsKey(t) || !bind.ContainsKey(t.parent)) continue;
                    Quaternion rot = bind[t].rotation;
                    h.BindLocal[f][s] = Quaternion.Inverse(bind[t.parent].rotation) * rot;
                    Vector3 dir = f == 0 && t.childCount > 0 && bind.ContainsKey(t.GetChild(0))
                        ? ((Vector3)bind[t.GetChild(0)].GetColumn(3) - (Vector3)bind[t].GetColumn(3)).normalized
                        : fingers;
                    Vector3 axis = Vector3.Cross(dir, palm).normalized;
                    if (Vector3.Dot(Quaternion.AngleAxis(10f, axis) * dir, palm) < 0f) axis = -axis;
                    h.CurlAxis[f][s] = Quaternion.Inverse(rot) * axis;
                }
            }
            return h;
        }

        private void LateUpdate()
        {
            if (!_ready) return;
            float weight = pose.HandsWeight;
            if (weight <= 0.001f) return;

            WeaponViewData v = weapon.View;
            Transform gun = pose.transform;

            // Right hand: firing grip.
            Vector3 rightTarget = pose.RightHandSocket.position + gun.rotation * v.RightWristOffset;
            Quaternion rightRot = HandRotation(_right, gun.TransformDirection(v.RightFingersDirection),
                gun.TransformDirection(v.RightPalmNormal));
            Solve(_right, rightTarget, rightRot, cameraTransform.TransformPoint(v.RightElbowHint) - cameraTransform.position, weight);
            ApplyGrip(_right, v.RightGripCurl, v.RightIndexCurl, v.ThumbCurl, weight);

            // Left hand: support grip, leaving for a fresh magazine during reloads.
            Vector3 leftGrip = pose.RightHandSocket.position + gun.rotation * v.LeftWristOffset;
            Quaternion leftGripRot = HandRotation(_left, gun.TransformDirection(v.LeftFingersDirection),
                gun.TransformDirection(v.LeftPalmNormal));
            float away = ReloadAway(weapon.State, out float atWell);
            Vector3 pouch = cameraTransform.TransformPoint(pouchPosition);
            Vector3 well = pose.MagWellSocket.position + gun.rotation * new Vector3(-0.01f, -0.06f, -0.01f);
            Vector3 leftTarget = Vector3.Lerp(Vector3.Lerp(leftGrip, pouch, away), well, atWell);
            Quaternion leftRot = Quaternion.Slerp(leftGripRot,
                HandRotation(_left, gun.TransformDirection(new Vector3(0.2f, 0.9f, 0.3f)), gun.TransformDirection(Vector3.right)),
                Mathf.Max(away, atWell));
            Solve(_left, leftTarget, leftRot, cameraTransform.TransformPoint(v.LeftElbowHint) - cameraTransform.position, weight);
            ApplyGrip(_left, v.LeftGripCurl, v.LeftGripCurl, v.ThumbCurl, weight * (1f - 0.6f * Mathf.Max(away, atWell)));
        }

        /// <summary>
        /// Reload choreography for the left hand. Returns how far it is toward the pouch; atWell = how far it is
        /// toward the magazine well (bringing the new magazine).
        /// </summary>
        private static float ReloadAway(in WeaponState s, out float atWell)
        {
            atWell = 0f;
            if (s.Action != WeaponAction.Reloading) return 0f;
            bool empty = s.ReloadKind == ReloadKind.Empty;
            float t = s.ActionProgress;
            float magIn = empty ? WeaponSimulation.EmptyMagIn : WeaponSimulation.TacticalMagIn;
            float magOut = empty ? WeaponSimulation.EmptyMagOut : WeaponSimulation.TacticalMagOut;
            float toPouchStart = magOut - 0.08f, atPouch = magOut + 0.1f;
            float toWellEnd = magIn, backEnd = magIn + 0.2f;
            float away = Smooth(Mathf.InverseLerp(toPouchStart, atPouch, t));
            atWell = Smooth(Mathf.InverseLerp(atPouch + 0.04f, toWellEnd, t)) * (1f - Smooth(Mathf.InverseLerp(toWellEnd + 0.04f, backEnd, t)));
            float back = Smooth(Mathf.InverseLerp(toWellEnd + 0.04f, backEnd, t));
            return away * (1f - back);
        }

        private static Quaternion HandRotation(Hand h, Vector3 fingersWorld, Vector3 palmWorld)
        {
            Vector3 f = fingersWorld.normalized;
            Vector3 n = Vector3.ProjectOnPlane(palmWorld, f).normalized;
            return Quaternion.LookRotation(f, n) * Quaternion.Inverse(Quaternion.LookRotation(h.FingersLocal, h.PalmLocal));
        }

        private static void Solve(Hand h, Vector3 target, Quaternion wristRotation, Vector3 hintDirection, float weight)
        {
            Quaternion upper0 = h.Upper.rotation, lower0 = h.Lower.rotation, wrist0 = h.Wrist.rotation;

            Vector3 a = h.Upper.position, b = h.Lower.position, c = h.Wrist.position;
            float ab = (b - a).magnitude, bc = (c - b).magnitude;
            Vector3 at = target - a;
            float dist = Mathf.Clamp(at.magnitude, 0.01f, (ab + bc) * 0.9995f);
            Vector3 dir = at.normalized;
            float cosA = Mathf.Clamp((ab * ab + dist * dist - bc * bc) / (2f * ab * dist), -1f, 1f);
            float sinA = Mathf.Sqrt(1f - cosA * cosA);
            Vector3 bend = Vector3.ProjectOnPlane(hintDirection, dir);
            if (bend.sqrMagnitude < 1e-6f) bend = Vector3.ProjectOnPlane(Vector3.down, dir);
            bend.Normalize();
            Vector3 elbow = a + dir * (ab * cosA) + bend * (ab * sinA);

            h.Upper.rotation = Quaternion.FromToRotation(b - a, elbow - a) * h.Upper.rotation;
            Vector3 wristNow = h.Wrist.position;
            Vector3 elbowNow = h.Lower.position;
            h.Lower.rotation = Quaternion.FromToRotation(wristNow - elbowNow, (a + dir * dist) - elbowNow) * h.Lower.rotation;
            h.Wrist.rotation = wristRotation;

            if (weight < 0.999f)
            {
                h.Upper.rotation = Quaternion.Slerp(upper0, h.Upper.rotation, weight);
                h.Lower.rotation = Quaternion.Slerp(lower0, h.Lower.rotation, weight);
                h.Wrist.rotation = Quaternion.Slerp(wrist0, h.Wrist.rotation, weight);
            }
        }

        private static void ApplyGrip(Hand h, Vector3 fingerCurl, Vector3 indexCurl, Vector3 thumbCurl, float weight)
        {
            for (int f = 0; f < 5; f++)
            {
                Vector3 curl = f == 0 ? thumbCurl : f == 1 ? indexCurl : fingerCurl;
                for (int s = 0; s < 3; s++)
                {
                    Transform t = h.Fingers[f][s];
                    if (t == null) continue;
                    Quaternion target = h.BindLocal[f][s] * Quaternion.AngleAxis(curl[s], h.CurlAxis[f][s]);
                    t.localRotation = Quaternion.Slerp(t.localRotation, target, weight);
                }
            }
        }

        private SkinnedMeshRenderer FindBodySkin()
        {
            SkinnedMeshRenderer best = null;
            foreach (SkinnedMeshRenderer smr in animator.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (best == null || smr.bones.Length > best.bones.Length) best = smr;
            return best;
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);
    }
}
