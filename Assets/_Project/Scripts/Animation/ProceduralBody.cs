using Polykov.Player;
using UnityEngine;

namespace Polykov.Animation
{
    /// <summary>
    /// Post-animation body posing: the spine follows the view pitch (so looking down shows your feet and
    /// looking up doesn't break the neck) and the torso rolls for lean/peek, limited by nearby walls.
    /// Runs after the Animator and before the camera reads the head.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public sealed class ProceduralBody : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private PlayerLook look;
        [Tooltip("Optional. When its controller has crouch clips, the procedural crouch bend is disabled.")]
        [SerializeField] private PlayerAnimator playerAnimator;

        [Header("Look")]
        [Tooltip("Fraction of a downward view pitch absorbed by the spine; the neck and head take the rest. " +
                 "Kept low so the head stays above the feet and you can see your legs.")]
        [SerializeField, Range(0f, 1f)] private float spinePitchShareDown = 0.2f;
        [Tooltip("Fraction of an upward view pitch absorbed by the spine.")]
        [SerializeField, Range(0f, 1f)] private float spinePitchShareUp = 0.45f;

        [Header("Crouch")]
        [Tooltip("Forward bend of the spine at full crouch (degrees); the neck compensates so the view is unchanged.")]
        [SerializeField, Range(0f, 40f)] private float crouchSpineBend = 16f;

        [Header("Lean")]
        [Tooltip("Total torso roll at full lean (degrees).")]
        [SerializeField, Range(0f, 40f)] private float leanAngle = 18f;
        [Tooltip("How far sideways the head travels at full lean; used for the wall check.")]
        [SerializeField, Range(0.1f, 0.6f)] private float leanReach = 0.34f;
        [SerializeField, Range(0.05f, 0.3f)] private float headRadius = 0.14f;
        [SerializeField] private LayerMask obstacleMask = ~0;
        [SerializeField] private float clearanceSharpness = 18f;

        private Transform _spine, _chest, _upperChest, _neck, _head;
        private float _leanLimit = 1f;

        /// <summary>Lean actually applied this frame after the wall check (-1..1).</summary>
        public float EffectiveLean { get; private set; }
        public Transform Head => _head;

        private void Awake()
        {
            _spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            _chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            _upperChest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
            _neck = animator.GetBoneTransform(HumanBodyBones.Neck);
            _head = animator.GetBoneTransform(HumanBodyBones.Head);
        }

        private void LateUpdate()
        {
            Quaternion yaw = Quaternion.Euler(0f, look.Yaw, 0f);
            Vector3 right = yaw * Vector3.right;
            Vector3 forward = yaw * Vector3.forward;

            // Pitch: bend the spine chain, then neck and head.
            bool clips = playerAnimator != null && playerAnimator.HasCrouchClips;
            float crouchBend = clips ? 0f : motor.State.Crouch * crouchSpineBend;
            float spinePitch = look.Pitch * (look.Pitch > 0f ? spinePitchShareDown : spinePitchShareUp) + crouchBend;
            float headPitch = look.Pitch - spinePitch;
            Rotate(_spine, right, spinePitch * 0.3f);
            Rotate(_chest, right, spinePitch * 0.35f);
            Rotate(_upperChest, right, spinePitch * 0.35f);
            Rotate(_neck, right, headPitch * 0.4f);
            Rotate(_head, right, headPitch * 0.6f);

            // Lean: reduce when a wall is in the way of the head.
            float lean = motor.State.Lean;
            float allowed = 1f;
            if (Mathf.Abs(lean) > 0.01f && _head != null)
            {
                Vector3 dir = right * Mathf.Sign(lean);
                if (Physics.SphereCast(_head.position, headRadius, dir, out RaycastHit hit, leanReach, obstacleMask,
                        QueryTriggerInteraction.Ignore))
                {
                    allowed = Mathf.Clamp01(hit.distance / leanReach);
                }
            }
            _leanLimit = Mathf.Lerp(_leanLimit, allowed, 1f - Mathf.Exp(-clearanceSharpness * Time.deltaTime));
            EffectiveLean = lean * _leanLimit;

            float roll = -EffectiveLean * leanAngle;
            Rotate(_spine, forward, roll * 0.3f);
            Rotate(_chest, forward, roll * 0.35f);
            Rotate(_upperChest, forward, roll * 0.35f);
        }

        private static void Rotate(Transform bone, Vector3 axis, float degrees)
        {
            if (bone != null) bone.rotation = Quaternion.AngleAxis(degrees, axis) * bone.rotation;
        }
    }
}
