using UnityEngine;

namespace Polykov.Combat
{
    /// <summary>
    /// Per-bone flinch: a bullet kicks an angular spring in the bone it hit and, attenuated, in its parents; the
    /// springs pull the pose back to the animation. Runs after the Animator, so it layers on top of any clip.
    /// Cheap (a handful of springs, only while active); the full ragdoll only exists once the character is dead.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public sealed class HitReaction : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private HealthComponent health;
        [SerializeField] private HitReactionTuning tuning = HitReactionTuning.Default;

        private Transform[] _bones;
        private AngularSpring[] _springs;
        private Transform _hips;
        private bool _active;

        public HitReactionTuning Tuning { get => tuning; set => tuning = value; }

        public void Initialize(Animator target, HealthComponent owner)
        {
            animator = target;
            health = owner;
            Build();
        }

        private void Awake()
        {
            if (animator != null && health != null) Build();
        }

        private void Build()
        {
            _hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            var list = new System.Collections.Generic.List<Transform>(24);
            // Hips..RightHand covers legs, spine, neck, head, shoulders and arms; UpperChest sits later in the enum.
            for (HumanBodyBones b = HumanBodyBones.Hips; b <= HumanBodyBones.RightHand; b++)
            {
                Transform t = animator.GetBoneTransform(b);
                if (t != null && !list.Contains(t)) list.Add(t);
            }
            Transform upperChest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
            if (upperChest != null && !list.Contains(upperChest)) list.Add(upperChest);
            _bones = list.ToArray();
            _springs = new AngularSpring[_bones.Length];
        }

        private void OnEnable()
        {
            if (health == null) return;
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }

        private void OnDisable()
        {
            if (health == null) return;
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
        }

        private void OnDied(BodyPart part)
        {
            // The ragdoll takes over: stop layering springs on a pose that is about to be physics-driven.
            if (_springs != null) System.Array.Clear(_springs, 0, _springs.Length);
            _active = false;
            enabled = false;
        }

        private void OnDamaged(BodyPart part, DamageResult result, Weapons.ShotHit hit)
        {
            if (_bones == null || !health.State.Alive) return;
            Transform bone = health.LastHitBone != null ? health.LastHitBone : BoneFor(part);
            if (bone == null) return;

            float peak = HitReactionModel.Degrees(part, hit.Impulse, hit.Damage, tuning);
            if (peak <= 0f) return;

            Transform current = bone;
            for (int level = 0; level < tuning.ChainLength && current != null; level++)
            {
                int index = System.Array.IndexOf(_bones, current);
                if (index >= 0)
                {
                    Vector3 boneAxis = BoneAxis(current);
                    Vector3 axis = HitReactionModel.Axis(boneAxis, hit.Direction);
                    if (axis != Vector3.zero)
                        _springs[index].Kick(axis, peak * HitReactionModel.Attenuation(level, tuning), tuning.Stiffness, tuning.Damping);
                }
                if (current == _hips) break;
                current = current.parent;
            }
            _active = true;
        }

        private void LateUpdate()
        {
            if (!_active) return;
            float dt = Time.deltaTime;
            bool any = false;
            for (int i = 0; i < _bones.Length; i++)
            {
                if (_springs[i].Idle) continue;
                _springs[i].Step(tuning.Stiffness, tuning.Damping, dt);
                Vector3 v = _springs[i].Value;
                float degrees = v.magnitude;
                if (degrees > 1e-4f) _bones[i].rotation = Quaternion.AngleAxis(degrees, v / degrees) * _bones[i].rotation;
                any |= !_springs[i].Idle;
            }
            _active = any;
        }

        /// <summary>Direction the bone points along (toward its first child); up for the spine and head.</summary>
        private Vector3 BoneAxis(Transform bone)
        {
            if (bone.childCount > 0)
            {
                Vector3 toChild = bone.GetChild(0).position - bone.position;
                if (toChild.sqrMagnitude > 1e-6f) return toChild.normalized;
            }
            return Vector3.up;
        }

        private Transform BoneFor(BodyPart part) => part switch
        {
            BodyPart.Head => animator.GetBoneTransform(HumanBodyBones.Head),
            BodyPart.Thorax => animator.GetBoneTransform(HumanBodyBones.UpperChest) ?? animator.GetBoneTransform(HumanBodyBones.Chest),
            BodyPart.Stomach => animator.GetBoneTransform(HumanBodyBones.Spine),
            BodyPart.LeftArm => animator.GetBoneTransform(HumanBodyBones.LeftUpperArm),
            BodyPart.RightArm => animator.GetBoneTransform(HumanBodyBones.RightUpperArm),
            BodyPart.LeftLeg => animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg),
            _ => animator.GetBoneTransform(HumanBodyBones.RightUpperLeg),
        };
    }
}
