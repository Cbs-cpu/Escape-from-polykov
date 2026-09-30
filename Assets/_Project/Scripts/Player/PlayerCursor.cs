using Polykov.Input;
using UnityEngine;

namespace Polykov.Player
{
    /// <summary>Locks the cursor while playing; Pause releases it and blocks gameplay input. Click to recapture.</summary>
    public sealed class PlayerCursor : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader input;

        private void OnEnable() => SetLocked(true);
        private void OnDisable() => SetLocked(false);

        private void Update()
        {
            if (input.PausePressedThisFrame)
            {
                SetLocked(false);
            }
            else if (!IsLocked && UnityEngine.InputSystem.Mouse.current != null
                     && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
            {
                SetLocked(true);
            }
        }

        private bool IsLocked => Cursor.lockState == CursorLockMode.Locked;

        private void SetLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
            if (input != null) input.Blocked = !locked;
        }
    }
}
