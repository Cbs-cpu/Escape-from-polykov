using UnityEngine;

namespace Polykov.Movement
{
    /// <summary>Static movement data asset. Edit in Play Mode to tune game feel live.</summary>
    [CreateAssetMenu(menuName = "Polykov/Movement Settings", fileName = "MovementSettings")]
    public sealed class MovementSettings : ScriptableObject
    {
        public MovementTuning Tuning = MovementTuning.Default;

        private void Reset() => Tuning = MovementTuning.Default;
    }
}
