using Polykov.Weapons;
using UnityEngine;

namespace Polykov.Combat
{
    /// <summary>
    /// Owns a character's <see cref="HealthState"/> and applies hits from its <see cref="Hitbox"/>es through the pure
    /// <see cref="HealthModel"/>. In multiplayer this runs on the server only; clients get the replicated state.
    /// </summary>
    public sealed class HealthComponent : MonoBehaviour
    {
        [SerializeField] private HealthTuning tuning = HealthTuning.Default;
        [SerializeField] private WoundTuning woundTuning = WoundTuning.Default;
        [Tooltip("Seeds the dismemberment dice: same seed and hits = same outcome (server and clients agree).")]
        [SerializeField] private uint seed = 1;

        private HealthState _state;
        private WoundState _wounds;
        private uint _hitCounter;

        public HealthState State => _state;
        public WoundState Wounds => _wounds;
        public HealthTuning Tuning => tuning;
        public uint Seed { get => seed; set => seed = value; }

        /// <summary>The bullet that landed last (for ragdoll impulse and effects).</summary>
        public ShotHit LastHit { get; private set; }
        public BodyPart LastHitPart { get; private set; }

        /// <summary>Every hit on a living body, after damage was applied.</summary>
        public event System.Action<BodyPart, DamageResult, ShotHit> Damaged;
        public event System.Action<BodyPart> Died;
        /// <summary>A limb (or the head) came off. Dismemberment can be switched off in the settings.</summary>
        public event System.Action<BodyPart, ShotHit> Severed;

        /// <summary>Raised for every bullet that reaches a hitbox, alive or dead (after damage/death).</summary>
        public event System.Action<Hitbox, ShotHit> ShotReceived;

        /// <summary>The hitbox of the last bullet received (valid inside <see cref="Damaged"/>/<see cref="Died"/>).</summary>
        public Hitbox LastHitbox { get; private set; }
        /// <summary>Bone of the last hitbox that was hit (null for hits without one).</summary>
        public Transform LastHitBone => LastHitbox != null ? LastHitbox.Bone : null;

        private void Awake() => ResetHealth();

        public void ResetHealth()
        {
            _state = HealthState.Full(tuning);
            _wounds = default;
            _hitCounter = 0;
        }

        /// <summary>Entry point for hitboxes: applies damage and then notifies presentation listeners.</summary>
        public void Receive(Hitbox hitbox, in ShotHit hit)
        {
            LastHitbox = hitbox;
            TakeHit(hitbox.Part, hit);
            ShotReceived?.Invoke(hitbox, hit);
        }

        public void TakeHit(BodyPart part, in ShotHit hit)
        {
            if (!_state.Alive) return;
            LastHit = hit;
            LastHitPart = part;
            _state = HealthModel.ApplyDamage(_state, part, hit.Damage, tuning, out DamageResult result);

            WoundEvent wound = default;
            if (Polykov.Core.UserSettings.Dismemberment && Polykov.Core.UserSettings.Gore != Polykov.Core.GoreLevel.Off)
            {
                _wounds = WoundModel.ApplyHit(_wounds, _state, part, hit.Damage, result.Overflow, hit.CalibreMultiplier,
                    seed + ++_hitCounter * 0x9E3779B9u, woundTuning, out wound);
            }

            Damaged?.Invoke(part, result, hit);
            if (wound.Severed) Severed?.Invoke(wound.Part, hit);
            bool dead = result.Killed;
            if (wound.Kills && _state.Alive)
            {
                // A severed head kills instantly.
                _state.Alive = false;
                _state.KillingPart = BodyPart.Head;
                dead = true;
            }
            if (dead) Died?.Invoke(_state.KillingPart);
        }
    }
}
