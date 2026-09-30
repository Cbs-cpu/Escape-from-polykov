using System.Collections.Generic;
using Polykov.Weapons;
using UnityEngine;

namespace Polykov.Combat
{
    /// <summary>
    /// Per-bone hit reactions (presentation, local): a damped spring impulse on the hit bone and its parents,
    /// added on top of the animated pose in LateUpdate (after the Animator), plus a brief stagger on big hits.
    /// No allocations per hit: springs live in fixed arrays.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public sealed class HitReactor : MonoBehaviour
    {
        private static readonly HumanBodyBones[] Tracked =
        {
            HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest,
            HumanBodyBones.Neck, HumanBodyBones.Head,
            HumanBodyBones.LeftShoulder, HumanBodyBones.RightShoulder,
            HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm,
            HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm,
            HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg,
            HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg,
        };

        private const float StaggerSlowSpeed = 0.35f;
        private const float StaggerRecovery = 0.4f;
        private const float StaggerSpeed = 2.4f;

        private Animator _animator;
        private Transform[] _bones;
        private HitSpring[] _springs;
        private readonly Dictionary<Transform, int> _index = new Dictionary<Transform, int>();
        private Transform _model;
        private Vector3 _modelRest;
        private Vector3 _stagger;
        private Vector3 _staggerVelocity;
        private float _slow;
        private bool _active;

        public bool Initialized => _bones != null;

        public void Init(Animator animator)
        {
            _animator = animator;
            _model = animator.transform;
            _modelRest = _model.localPosition;
            var bones = new List<Transform>(Tracked.Length);
            foreach (HumanBodyBones id in Tracked)
            {
                Transform bone = animator.GetBoneTransform(id);
                if (bone == null || _index.ContainsKey(bone)) continue;
                _index.Add(bone, bones.Count);
                bones.Add(bone);
            }
            _bones = bones.ToArray();
            _springs = new HitSpring[_bones.Length];
        }

        /// <summary>Kicks the hit bone (found from any child transform, e.g. a hitbox) and its parents.</summary>
        public void Hit(Transform hitTransform, in ShotHit hit, BodyPart part)
        {
            if (_bones == null || !enabled) return;
            float magnitude = HitReaction.Magnitude(hit.Damage, part);
            Vector3 dir = hit.Direction;
            Transform t = hitTransform;
            int depth = 0;
            // Climb to the first tracked bone; depth counts tracked bones only so limbs still reach the spine.
            while (t != null && !_index.ContainsKey(t)) t = t.parent;
            while (t != null && depth <= HitReaction.MaxChainDepth)
            {
                if (_index.TryGetValue(t, out int i))
                {
                    Vector3 axis = HitReaction.Axis(hit.Point - t.position, dir);
                    _springs[i].AddImpulse(axis * (magnitude * HitReaction.Attenuation(depth)));
                    depth++;
                }
                t = t.parent;
            }
            _active = true;

            if (HitReaction.Staggers(hit.Damage, part))
            {
                Vector3 flat = Vector3.ProjectOnPlane(dir, Vector3.up);
                if (flat.sqrMagnitude > 1e-4f) _staggerVelocity += _model.parent != null
                    ? _model.parent.InverseTransformDirection(flat.normalized) * StaggerSpeed
                    : flat.normalized * StaggerSpeed;
                _slow = 1f;
            }
        }

        /// <summary>Back to the animated pose immediately (death or respawn).</summary>
        public void ResetAll()
        {
            if (_springs == null) return;
            for (int i = 0; i < _springs.Length; i++) _springs[i].Reset();
            _stagger = Vector3.zero;
            _staggerVelocity = Vector3.zero;
            _slow = 0f;
            _active = false;
            if (_model != null) _model.localPosition = _modelRest;
            if (_animator != null) _animator.speed = 1f;
        }

        private void OnDisable() => ResetAll();

        private void LateUpdate()
        {
            if (!_active) return;
            float dt = Time.deltaTime;
            bool any = false;
            for (int i = 0; i < _bones.Length; i++)
            {
                ref HitSpring spring = ref _springs[i];
                if (spring.AtRest) continue;
                spring.Step(dt, HitReaction.Stiffness, HitReaction.Damping);
                float angle = spring.Angle.magnitude;
                if (angle > 1e-3f)
                {
                    _bones[i].rotation = Quaternion.AngleAxis(angle, spring.Angle / angle) * _bones[i].rotation;
                    any = true;
                }
            }

            // Stagger: spring the model root back a few centimetres and slow the animation for a moment.
            if (_slow > 0f || _stagger.sqrMagnitude > 1e-6f || _staggerVelocity.sqrMagnitude > 1e-4f)
            {
                _staggerVelocity += (-HitReaction.Stiffness * _stagger - HitReaction.Damping * _staggerVelocity) * dt;
                _stagger += _staggerVelocity * dt;
                _model.localPosition = _modelRest + _stagger;
                _slow = Mathf.Max(0f, _slow - dt / StaggerRecovery);
                _animator.speed = Mathf.Lerp(1f, StaggerSlowSpeed, _slow);
                any = true;
            }
            else if (_animator.speed != 1f)
            {
                _animator.speed = 1f;
                _model.localPosition = _modelRest;
            }
            if (!any) _active = false;
        }
    }
}
