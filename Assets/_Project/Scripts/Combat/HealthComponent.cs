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

        private void Awake() => ResetHealth();

        public void ResetHealth() => _state = HealthState.Full(tuning);

        public void TakeHit(BodyPart part, in ShotHit hit)
        {
            if (!_state.Alive) return;
            _state = HealthModel.ApplyDamage(_state, part, hit.Damage, tuning, out DamageResult result);
            Damaged?.Invoke(part, result, hit);
            if (result.Killed) Died?.Invoke(_state.KillingPart);
        }
    }
}
