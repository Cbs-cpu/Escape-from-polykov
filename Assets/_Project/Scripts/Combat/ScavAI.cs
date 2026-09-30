using Polykov.Weapons;
using UnityEngine;
using UnityEngine.AI;

namespace Polykov.Combat
{
    /// <summary>
    /// Drives a scav with the pure <see cref="ScavBrain"/>: gathers senses (sight raycast, gunshots, damage, lost limbs),
    /// steps the state machine at 10 Hz and executes the goal with a NavMeshAgent. Without a baked NavMesh it falls back to
    /// straight-line steering glued to the ground, so the arena works before baking.
    /// </summary>
    [RequireComponent(typeof(HealthComponent))]
    public sealed class ScavAI : MonoBehaviour
    {
        [SerializeField] private BrainTuning tuning = BrainTuning.Default;
        [SerializeField] private float eyeHeight = 1.6f;
        [SerializeField] private float wanderRadius = 12f;
        [SerializeField] private LayerMask sightMask = ~0;
        [SerializeField] private float turnSpeed = 240f;

        private const float ThinkInterval = 0.1f;

        private HealthComponent _health;
        private NavMeshAgent _agent;
        private Animator _animator;
        private Transform _target;
        private PlayerWeapon _weapon;
        private BrainState _state = BrainState.Initial;
        private float _thinkTimer;
        private Vector3 _home;
        private Vector3 _lastKnown;
        private Vector3 _destination;
        private bool _hasDestination;
        private bool _wasHit;
        private float _heardLoudness;
        private bool _heardShot;
        private int _velXHash = Animator.StringToHash("VelX"), _velZHash = Animator.StringToHash("VelZ");
        private bool _hasVelParams;

        public BrainMode Mode => _state.Mode;
        /// <summary>Raised when the scav strikes; the target should take damage (player health lands with the multiplayer server).</summary>
        public event System.Action<Transform> Attacked;

        private void Awake()
        {
            _health = GetComponent<HealthComponent>();
            _animator = GetComponentInChildren<Animator>();
            _agent = GetComponent<NavMeshAgent>();
            _home = transform.position;
            // Other scavs and corpses do not block sight (and neither do our own hitboxes).
            sightMask &= ~((1 << CombatLayers.Enemy) | (1 << CombatLayers.Ragdoll));
            if (_animator != null)
                foreach (AnimatorControllerParameter p in _animator.parameters)
                    if (p.nameHash == _velXHash) _hasVelParams = true;
        }

        private void OnEnable()
        {
            _health.Damaged += OnDamaged;
            _health.Died += OnDied;
            _home = transform.position;
        }

        private void OnDisable()
        {
            _health.Damaged -= OnDamaged;
            _health.Died -= OnDied;
            if (_weapon != null) _weapon.ShotHeard -= OnShotHeard;
        }

        private void OnDamaged(BodyPart part, DamageResult result, ShotHit hit) => _wasHit = true;

        private void OnDied(BodyPart part)
        {
            if (_agent != null) _agent.enabled = false;
            enabled = false;
        }

        private void OnShotHeard(float loudness)
        {
            _heardShot = true;
            _heardLoudness = loudness;
        }

        private void FindTarget()
        {
            _weapon = FindFirstObjectByType<PlayerWeapon>();
            if (_weapon == null) return;
            _target = _weapon.transform.root;
            _weapon.ShotHeard += OnShotHeard;
        }

        private void Update()
        {
            if (_target == null)
            {
                FindTarget();
                if (_target == null) return;
            }
            _thinkTimer -= Time.deltaTime;
            if (_thinkTimer <= 0f)
            {
                Think(ThinkInterval);
                _thinkTimer = ThinkInterval;
            }
            Locomote(Time.deltaTime);
        }

        private void Think(float dt)
        {
            Vector3 eye = transform.position + Vector3.up * eyeHeight;
            Vector3 targetPoint = _target.position + Vector3.up * 1.4f;
            Vector3 to = targetPoint - eye;
            float distance = to.magnitude;
            Vector3 flat = Vector3.ProjectOnPlane(to, Vector3.up);

            var senses = new BrainSenses
            {
                TargetAlive = true,
                TargetDistance = distance,
                TargetAngle = flat.sqrMagnitude > 1e-4f ? Vector3.Angle(transform.forward, flat) : 0f,
                LineOfSight = distance <= tuning.SightRange && HasLineOfSight(eye, to, distance),
                HeardShot = _heardShot,
                // A quieter shot (suppressor) is heard as if it were farther away.
                HeardDistance = distance / Mathf.Clamp(_heardLoudness, 0.1f, 2f),
                WasHit = _wasHit,
                LeftLegSevered = _health.Wounds.LeftLegSevered,
                RightLegSevered = _health.Wounds.RightLegSevered,
                LeftArmSevered = _health.Wounds.LeftArmSevered,
                RightArmSevered = _health.Wounds.RightArmSevered,
            };
            _heardShot = false;
            _wasHit = false;

            BrainState next = ScavBrain.Step(_state, senses, tuning, dt);
            if (next.TargetKnown && (senses.LineOfSight || senses.HeardShot || senses.WasHit)) _lastKnown = _target.position;
            if (next.Mode != _state.Mode) _hasDestination = false;
            _state = next;
            if (_state.Attack) Attacked?.Invoke(_target);
        }

        private bool HasLineOfSight(Vector3 eye, Vector3 to, float distance)
        {
            if (!Physics.Raycast(eye, to / Mathf.Max(distance, 1e-4f), out RaycastHit hit, distance, sightMask, QueryTriggerInteraction.Ignore))
                return true;
            return hit.collider.transform.IsChildOf(_target);
        }

        private void Locomote(float dt)
        {
            Vector3 face = Vector3.zero;
            switch (_state.Goal)
            {
                case BrainGoal.Wander:
                    if (!_hasDestination || Reached()) PickWanderPoint();
                    break;
                case BrainGoal.Investigate:
                    SetDestination(_lastKnown);
                    break;
                case BrainGoal.Chase:
                    SetDestination(_target.position);
                    break;
                case BrainGoal.FaceTarget:
                    face = _target.position - transform.position;
                    break;
            }

            float speed = _state.Goal == BrainGoal.FaceTarget || _state.Goal == BrainGoal.Stay ? 0f : _state.Speed;
            Vector3 velocity = Vector3.zero;
            if (speed > 0.01f && _hasDestination) velocity = Steer(speed, dt, out face);
            else if (_agent != null && _agent.enabled && _agent.isOnNavMesh) _agent.isStopped = true;

            face.y = 0f;
            if (face.sqrMagnitude > 1e-4f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(face), turnSpeed * dt);

            if (_hasVelParams)
            {
                Vector3 local = transform.InverseTransformDirection(velocity);
                _animator.SetFloat(_velXHash, local.x, 0.1f, dt);
                _animator.SetFloat(_velZHash, local.z, 0.1f, dt);
            }
        }

        private Vector3 Steer(float speed, float dt, out Vector3 face)
        {
            face = Vector3.zero;
            if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
            {
                _agent.isStopped = false;
                _agent.speed = speed;
                _agent.SetDestination(_destination);
                face = _agent.desiredVelocity;
                return _agent.velocity;
            }

            // No NavMesh: walk straight at the destination and stay on the ground.
            Vector3 delta = _destination - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude < 0.25f) return Vector3.zero;
            Vector3 dir = delta.normalized;
            Vector3 next = transform.position + dir * (speed * dt);
            if (Physics.Raycast(next + Vector3.up * 1f, Vector3.down, out RaycastHit ground, 3f, sightMask, QueryTriggerInteraction.Ignore))
                next.y = ground.point.y;
            transform.position = next;
            face = dir;
            return dir * speed;
        }

        private void SetDestination(Vector3 point)
        {
            _destination = point;
            _hasDestination = true;
        }

        private bool Reached()
        {
            Vector3 d = _destination - transform.position;
            d.y = 0f;
            return d.sqrMagnitude < 1f;
        }

        private void PickWanderPoint()
        {
            Vector2 offset = Random.insideUnitCircle * wanderRadius;
            Vector3 point = _home + new Vector3(offset.x, 0f, offset.y);
            if (NavMesh.SamplePosition(point, out NavMeshHit hit, 4f, NavMesh.AllAreas)) point = hit.position;
            SetDestination(point);
        }
    }
}
