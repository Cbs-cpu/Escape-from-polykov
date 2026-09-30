using Polykov.Weapons;
using UnityEngine;

namespace Polykov.Combat
{
    /// <summary>
    /// Presentation of a shootable humanoid: hitboxes, per-bone hit reactions, wound decals and the ragdoll on death.
    /// Sits next to a <see cref="HealthComponent"/> on the character root; the Animator is found in the children.
    /// </summary>
    [RequireComponent(typeof(HealthComponent))]
    public sealed class HumanoidCombatRig : MonoBehaviour
    {
        [SerializeField] private Animator animator;

        private static readonly int GroundedHash = Animator.StringToHash("Grounded");

        private HealthComponent _health;
        private HitReactor _reactor;
        private Ragdoll _ragdoll;
        private Transform _hips;
        private Vector3 _lastHips;
        private Vector3 _velocity;
        private bool _justDied;
        private bool _initialized;

        public HealthComponent Health => _health;
        public Ragdoll Ragdoll => _ragdoll;
        public bool IsRagdoll => _ragdoll != null && _ragdoll.Active;

        private void Awake() => Init();

        private void Init()
        {
            if (_initialized) return;
            _initialized = true;
            _health = GetComponent<HealthComponent>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator == null || !animator.isHuman) return;

            if (GetComponentInChildren<Hitbox>(true) == null)
                HumanoidHitboxes.Build(animator, _health, CombatLayers.Enemy);

            animator.applyRootMotion = false;
            foreach (AnimatorControllerParameter p in animator.parameters)
                if (p.nameHash == GroundedHash && p.type == AnimatorControllerParameterType.Bool)
                    animator.SetBool(GroundedHash, true);

            _reactor = gameObject.AddComponent<HitReactor>();
            _reactor.Init(animator);
            _ragdoll = gameObject.AddComponent<Ragdoll>();
            _ragdoll.Init(animator);
            if (GetComponent<WoundDecals>() == null) gameObject.AddComponent<WoundDecals>();

            _hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            _lastHips = _hips.position;
            _health.ShotReceived += OnShotReceived;
            _health.Died += OnDied;
        }

        private void OnDestroy()
        {
            if (_health == null) return;
            _health.ShotReceived -= OnShotReceived;
            _health.Died -= OnDied;
        }

        private void LateUpdate()
        {
            if (_hips == null || _ragdoll.Active) return;
            float dt = Time.deltaTime;
            if (dt > 1e-5f) _velocity = Vector3.Lerp(_velocity, (_hips.position - _lastHips) / dt, 0.5f);
            _lastHips = _hips.position;
        }

        private void OnShotReceived(Hitbox hitbox, ShotHit hit)
        {
            if (_reactor == null) return;
            if (_ragdoll.Active)
            {
                float impulse = _justDied
                    ? RagdollProfile.LethalImpulse(hit.Damage, hitbox.Part)
                    : RagdollProfile.CorpseImpulse(hit.Damage);
                _justDied = false;
                _ragdoll.AddImpulse(hitbox.transform, hit.Point, hit.Direction, impulse);
            }
            else
            {
                _reactor.Hit(hitbox.transform, hit, hitbox.Part);
            }
        }

        private void OnDied(BodyPart part)
        {
            if (_ragdoll == null) return;
            _reactor.ResetAll();
            _reactor.enabled = false;
            _justDied = true;
            // Velocity below ~0.1 m/s is animation jitter, not motion.
            Vector3 inherited = _velocity.sqrMagnitude < 0.01f ? Vector3.zero : _velocity;
            _ragdoll.Activate(inherited);
        }

        /// <summary>Stands the character up again with full health (dummy respawn).</summary>
        public void Revive()
        {
            if (_ragdoll == null) return;
            _ragdoll.Deactivate();
            _reactor.enabled = true;
            _reactor.ResetAll();
            _justDied = false;
            _velocity = Vector3.zero;
            _lastHips = _hips.position;
            var decals = GetComponent<WoundDecals>();
            if (decals != null) decals.Clear();
        }
    }
}
