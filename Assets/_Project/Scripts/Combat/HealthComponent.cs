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

        private HealthState _state;

        public HealthState State => _state;
        public HealthTuning Tuning => tuning;

        public event System.Action<BodyPart, DamageResult, ShotHit> Damaged;
        public event System.Action<BodyPart> Died;

        /// <summary>Raised for every bullet that reaches a hitbox, alive or dead (after damage/death).</summary>
        public event System.Action<Hitbox, ShotHit> ShotReceived;

        /// <summary>The hitbox of the last bullet received (valid inside <see cref="Damaged"/>/<see cref="Died"/>).</summary>
        public Hitbox LastHitbox { get; private set; }

        private void Awake() => ResetHealth();

        public void ResetHealth() => _state = HealthState.Full(tuning);

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
            _state = HealthModel.ApplyDamage(_state, part, hit.Damage, tuning, out DamageResult result);
            Damaged?.Invoke(part, result, hit);
            if (result.Killed) Died?.Invoke(_state.KillingPart);
        }
    }
}
