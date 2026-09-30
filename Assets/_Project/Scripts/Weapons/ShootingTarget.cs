using UnityEngine;

namespace Polykov.Weapons
{
    /// <summary>
    /// Steel popper for the test range: falls back when hit and stands up again after a delay. Counts hits.
    /// The plate pivots around its base (this transform).
    /// </summary>
    public sealed class ShootingTarget : MonoBehaviour, IShotReceiver
    {
        [SerializeField] private float fallAngle = 80f;
        [SerializeField] private float resetDelay = 1.5f;
        [SerializeField] private float fallSpeed = 900f;
        [SerializeField] private float riseSpeed = 180f;

        private float _angle;
        private float _downTimer;
        private Quaternion _rest;

        public int Hits { get; private set; }
        public static int TotalHits { get; private set; }

        private void Awake() => _rest = transform.localRotation;

        public void OnShot(in ShotHit hit)
        {
            Hits++;
            TotalHits++;
            _downTimer = resetDelay;
        }

        private void Update()
        {
            bool down = _downTimer > 0f;
            _downTimer -= Time.deltaTime;
            _angle = Mathf.MoveTowards(_angle, down ? fallAngle : 0f, (down ? fallSpeed : riseSpeed) * Time.deltaTime);
            transform.localRotation = _rest * Quaternion.Euler(-_angle, 0f, 0f);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => TotalHits = 0;
    }
}
