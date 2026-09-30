using UnityEngine;

namespace Polykov.Animation
{
    /// <summary>
    /// Puts both hands on a held object (weapon grip and support point) with analytic two-bone IK, then closes
    /// the fingers procedurally. Runs in LateUpdate after the Animator, body posing and camera, so it never fights
    /// the locomotion clips and works the same for any animation. Targets are driven by the weapon presenter.
    ///
    /// Target convention (see <see cref="HandFrame"/>): a target's position is the wrist; its forward is the
    /// direction from the wrist to the middle knuckle, its up is from the little-finger knuckle to the index one.
    /// </summary>
    [DefaultExecutionOrder(130)]
    public sealed class HandIK : MonoBehaviour
    {
        [SerializeField] private Animator animator;

        [Header("Elbow hints (character space, from the shoulder)")]
        [SerializeField] private Vector3 rightElbowHint = new Vector3(0.45f, -0.5f, -0.2f);
        [SerializeField] private Vector3 leftElbowHint = new Vector3(-0.45f, -0.5f, -0.2f);

        [Header("Fingers (degrees per phalanx)")]
        [SerializeField, Range(0f, 110f)] private float gripCurl = 72f;
        [SerializeField, Range(0f, 110f)] private float triggerFingerCurl = 28f;
        [SerializeField, Range(0f, 110f)] private float supportCurl = 62f;
        [SerializeField, Range(0f, 90f)] private float thumbCurl = 8f;

        private Arm _right;
        private Arm _left;

        /// <summary>Wrist target for the right (firing) hand.</summary>
        public Transform RightTarget { get; set; }
        /// <summary>Wrist target for the left (support) hand.</summary>
        public Transform LeftTarget { get; set; }
        /// <summary>Center of the held object; the palms curl toward it.</summary>
        public Transform GripCenter { get; set; }
        /// <summary>Global blend 0..1 (0 = pure animation).</summary>
        public float Weight { get; set; } = 1f;
        /// <summary>Per-hand blend for the support hand (e.g. released during a one-handed action).</summary>
        public float LeftWeight { get; set; } = 1f;
        /// <summary>Trigger finger curl override 0..1 (1 = pulling the trigger).</summary>
        public float TriggerPull { get; set; }
        /// <summary>World direction the right thumb points along (zero = leave the thumb to the curl).</summary>
        public Vector3 RightThumbDirection { get; set; }
        public Vector3 LeftThumbDirection { get; set; }

        private void Awake()
        {
            _right = new Arm(animator, true);
            _left = new Arm(animator, false);
        }

        private void LateUpdate()
        {
            if (Weight <= 0f) return;
            Transform body = animator.transform;
            if (RightTarget != null && _right.Valid)
            {
                _right.Solve(RightTarget, body.TransformDirection(rightElbowHint), Weight);
                _right.AimThumb(RightThumbDirection, Weight);
                _right.CurlFingers(GripCenter, Weight, gripCurl, Mathf.Lerp(triggerFingerCurl, triggerFingerCurl + 25f, TriggerPull), thumbCurl);
            }
            float left = Weight * LeftWeight;
            if (LeftTarget != null && _left.Valid && left > 0f)
            {
                _left.Solve(LeftTarget, body.TransformDirection(leftElbowHint), left);
                _left.AimThumb(LeftThumbDirection, left);
                _left.CurlFingers(GripCenter, left, supportCurl, supportCurl, thumbCurl);
            }
        }

        /// <summary>Hand frame helper: builds a target rotation from the convention above.</summary>
        public static Quaternion HandFrame(Vector3 wristToKnuckles, Vector3 littleToIndex)
            => Quaternion.LookRotation(wristToKnuckles, littleToIndex);

        private sealed class Arm
        {
            private readonly Transform _upper;
            private readonly Transform _lower;
            private readonly Transform _hand;
            // Rotation from the hand's own frame (wrist->knuckles, little->index) to its bone rotation.
            private readonly Quaternion _handFrameInverse;
            private readonly bool _hasFrame;
            private readonly Transform[][] _fingers; // index, middle, ring, little, thumb (proximal..distal)

            public bool Valid => _upper != null && _lower != null && _hand != null;

            public Arm(Animator animator, bool right)
            {
                _upper = animator.GetBoneTransform(right ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm);
                _lower = animator.GetBoneTransform(right ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm);
                _hand = animator.GetBoneTransform(right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
                _fingers = new[]
                {
                    Chain(animator, right ? HumanBodyBones.RightIndexProximal : HumanBodyBones.LeftIndexProximal),
                    Chain(animator, right ? HumanBodyBones.RightMiddleProximal : HumanBodyBones.LeftMiddleProximal),
                    Chain(animator, right ? HumanBodyBones.RightRingProximal : HumanBodyBones.LeftRingProximal),
                    Chain(animator, right ? HumanBodyBones.RightLittleProximal : HumanBodyBones.LeftLittleProximal),
                    Chain(animator, right ? HumanBodyBones.RightThumbProximal : HumanBodyBones.LeftThumbProximal),
                };
                if (!Valid) return;

                Transform index = _fingers[0][0];
                Transform middle = _fingers[1][0];
                Transform little = _fingers[3][0];
                if (index != null && middle != null && little != null)
                {
                    // Bone offsets are constant, so the hand-local frame can be measured once in any pose.
                    Quaternion inv = Quaternion.Inverse(_hand.rotation);
                    Vector3 forward = inv * (middle.position - _hand.position);
                    Vector3 side = inv * (index.position - little.position);
                    _handFrameInverse = Quaternion.Inverse(Quaternion.LookRotation(forward, side));
                    _hasFrame = true;
                }
            }

            private static Transform[] Chain(Animator animator, HumanBodyBones proximal)
                => new[]
                {
                    animator.GetBoneTransform(proximal),
                    animator.GetBoneTransform(proximal + 1),
                    animator.GetBoneTransform(proximal + 2),
                };

            public void Solve(Transform target, Vector3 hintOffset, float weight)
            {
                Vector3 a = _upper.position;
                TwoBoneIKSolver.Solve(a, _upper.rotation, _lower.position, _lower.rotation, _hand.position,
                    target.position, a + hintOffset, out Quaternion upper, out Quaternion lower);
                _upper.rotation = Quaternion.Slerp(_upper.rotation, upper, weight);
                _lower.rotation = Quaternion.Slerp(_lower.rotation, lower, weight);
                if (_hasFrame)
                    _hand.rotation = Quaternion.Slerp(_hand.rotation, target.rotation * _handFrameInverse, weight);
            }

            /// <summary>Points the thumb along a direction (thumbs-forward pistol grip).</summary>
            public void AimThumb(Vector3 direction, float weight)
            {
                Transform[] thumb = _fingers[4];
                if (direction.sqrMagnitude < 1e-6f || thumb[0] == null || thumb[1] == null) return;
                AimSegment(thumb[0], thumb[1], direction, weight);
                if (thumb[2] != null) AimSegment(thumb[1], thumb[2], direction, weight * 0.7f);
            }

            private static void AimSegment(Transform bone, Transform child, Vector3 direction, float weight)
            {
                Quaternion q = Quaternion.FromToRotation(child.position - bone.position, direction);
                bone.rotation = Quaternion.Slerp(Quaternion.identity, q, weight) * bone.rotation;
            }

            public void CurlFingers(Transform center, float weight, float curl, float indexCurl, float thumb)
            {
                if (!_hasFrame || _fingers[1][0] == null) return;
                // Palm normal: perpendicular to the knuckle line, pointing at the held object.
                Vector3 forward = _fingers[1][0].position - _hand.position;
                Vector3 side = _fingers[0][0].position - _fingers[3][0].position;
                Vector3 palm = Vector3.Cross(forward, side).normalized;
                Vector3 toCenter = (center != null ? center.position : _hand.position + forward) - _hand.position;
                if (Vector3.Dot(palm, toCenter) < 0f) palm = -palm;

                Curl(_fingers[0], palm, indexCurl * weight);
                Curl(_fingers[1], palm, curl * weight);
                Curl(_fingers[2], palm, curl * weight);
                Curl(_fingers[3], palm, curl * weight);
                Curl(_fingers[4], palm, thumb * weight);
            }

            private static void Curl(Transform[] chain, Vector3 palm, float degrees)
            {
                if (degrees <= 0f) return;
                for (int i = 0; i < chain.Length; i++)
                {
                    Transform bone = chain[i];
                    if (bone == null) return;
                    Transform next = i + 1 < chain.Length ? chain[i + 1] : null;
                    Vector3 dir = next != null ? next.position - bone.position
                        : bone.position - chain[Mathf.Max(0, i - 1)].position;
                    Vector3 axis = Vector3.Cross(dir, palm);
                    if (axis.sqrMagnitude < 1e-8f) continue;
                    float factor = i == 0 ? 1f : i == 1 ? 1.1f : 0.8f;
                    bone.rotation = Quaternion.AngleAxis(degrees * factor, axis.normalized) * bone.rotation;
                }
            }
        }
    }
}
