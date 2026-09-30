using Polykov.Movement;
using Polykov.Player;
using UnityEngine;

namespace Polykov.Animation
{
    /// <summary>
    /// Presentation only: maps the replicated movement state to Animator parameters.
    /// Uses local velocity so the same code drives remote players from replicated data in Phase 2.
    /// </summary>
    public sealed class PlayerAnimator : MonoBehaviour
    {
        private static readonly int VelX = Animator.StringToHash("VelX");
        private static readonly int VelZ = Animator.StringToHash("VelZ");
        private static readonly int Grounded = Animator.StringToHash("Grounded");
        private static readonly int VerticalSpeed = Animator.StringToHash("VerticalSpeed");

        [SerializeField] private PlayerMotor motor;
        [SerializeField] private PlayerLook look;
        [SerializeField] private Animator animator;
        [Tooltip("Seconds to smooth velocity parameters (hides the 60 Hz tick steps).")]
        [SerializeField, Range(0f, 0.3f)] private float velocityDamp = 0.08f;

        private void Update()
        {
            MovementState state = motor.State;
            Vector3 local = Quaternion.Inverse(Quaternion.Euler(0f, look.Yaw, 0f)) * state.PlanarVelocity;
            float dt = Time.deltaTime;
            animator.SetFloat(VelX, local.x, velocityDamp, dt);
            animator.SetFloat(VelZ, local.z, velocityDamp, dt);
            animator.SetFloat(VerticalSpeed, state.VerticalSpeed);
            animator.SetBool(Grounded, state.Grounded);
        }
    }
}
