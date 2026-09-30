using Polykov.Player;
using UnityEngine;

namespace Polykov.Animation
{
    /// <summary>
    /// Humanoid foot placement: keeps the animated foot lift relative to the real ground under each foot,
    /// aligns feet to slopes and lowers the pelvis for the lower foot. Local presentation only.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public sealed class FootIK : MonoBehaviour
    {
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField, Range(0.1f, 1f)] private float rayAbove = 0.5f;
        [SerializeField, Range(0.1f, 1f)] private float rayBelow = 0.55f;
        [SerializeField, Range(0f, 0.6f)] private float maxPelvisDrop = 0.4f;
        [SerializeField, Range(0f, 1f)] private float rotationWeight = 0.8f;
        [SerializeField] private float pelvisSharpness = 14f;
        [SerializeField] private float weightSharpness = 10f;
        [Tooltip("Pelvis drop at full crouch (m). Feet stay planted by IK, so the knees bend.")]
        [SerializeField, Range(0f, 0.7f)] private float crouchPelvisDrop = 0.42f;

        [Tooltip("Optional. When its controller has crouch clips, the procedural crouch drop is disabled.")]
        [SerializeField] private PlayerAnimator playerAnimator;

        private Animator _animator;
        private float _leftWeight;
        private float _rightWeight;
        private float _pelvisOffset;

        private void Awake() => _animator = GetComponent<Animator>();

        private void OnAnimatorIK(int layerIndex)
        {
            float dt = Time.deltaTime;
            bool grounded = motor.State.Grounded;
            float rootY = motor.InterpolatedPosition.y;

            Probe(AvatarIKGoal.LeftFoot, rootY, out float leftDelta, out Vector3 leftPos, out Quaternion leftRot, out bool leftHit);
            Probe(AvatarIKGoal.RightFoot, rootY, out float rightDelta, out Vector3 rightPos, out Quaternion rightRot, out bool rightHit);

            float target = grounded ? 1f : 0f;
            _leftWeight = Damp(_leftWeight, leftHit ? target : 0f, weightSharpness, dt);
            _rightWeight = Damp(_rightWeight, rightHit ? target : 0f, weightSharpness, dt);

            float pelvisTarget = grounded ? Mathf.Clamp(Mathf.Min(leftDelta, rightDelta, 0f), -maxPelvisDrop, 0f) : 0f;
            // Crouch pose without dedicated clips: lower the hips and let the foot IK bend the legs.
            // Only while grounded, otherwise the feet would poke out of the (shrunk) capsule.
            bool clips = playerAnimator != null && playerAnimator.HasCrouchClips;
            if (grounded && !clips) pelvisTarget -= motor.State.Crouch * crouchPelvisDrop;
            _pelvisOffset = Damp(_pelvisOffset, pelvisTarget, pelvisSharpness, dt);
            _animator.bodyPosition += Vector3.up * _pelvisOffset;

            Apply(AvatarIKGoal.LeftFoot, _leftWeight, leftPos, leftRot);
            Apply(AvatarIKGoal.RightFoot, _rightWeight, rightPos, rightRot);
        }

        private void Probe(AvatarIKGoal goal, float rootY, out float delta, out Vector3 position, out Quaternion rotation, out bool hit)
        {
            Vector3 animated = _animator.GetIKPosition(goal);
            rotation = _animator.GetIKRotation(goal);
            position = animated;
            delta = 0f;
            hit = Physics.Raycast(animated + Vector3.up * rayAbove, Vector3.down, out RaycastHit info,
                rayAbove + rayBelow, groundMask, QueryTriggerInteraction.Ignore);
            if (!hit) return;

            // Preserve the animation's lift above the character root, but measured from the real ground here.
            float lift = animated.y - rootY;
            float targetY = info.point.y + lift;
            delta = targetY - animated.y;
            position = new Vector3(animated.x, targetY, animated.z);
            rotation = Quaternion.FromToRotation(Vector3.up, info.normal) * rotation;
        }

        private void Apply(AvatarIKGoal goal, float weight, Vector3 position, Quaternion rotation)
        {
            _animator.SetIKPositionWeight(goal, weight);
            _animator.SetIKRotationWeight(goal, weight * rotationWeight);
            _animator.SetIKPosition(goal, position);
            _animator.SetIKRotation(goal, rotation);
        }

        private static float Damp(float current, float target, float sharpness, float dt)
            => Mathf.Lerp(current, target, 1f - Mathf.Exp(-sharpness * dt));
    }
}
