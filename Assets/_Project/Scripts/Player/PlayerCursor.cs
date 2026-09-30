using Polykov.Input;
using UnityEngine;

namespace Polykov.Player
{
    /// <summary>
    /// Owns the paused/playing state of the local player: locks the cursor while playing, releases it and
    /// blocks gameplay input while paused. Pause toggles; clicking the game view resumes unless a menu is open.
    /// </summary>
    public sealed class PlayerCursor : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader input;

        /// <summary>When false, a click does not resume (a menu handles its own resume button).</summary>
        public bool ClickToResume { get; set; } = true;

        public bool Paused { get; private set; }

        /// <summary>
        /// A non-pausing overlay (raid inventory) owns the cursor: it is freed and gameplay input blocked, but the
        /// game is not "paused" and Esc/click-to-resume are ignored here (the overlay handles them).
        /// </summary>
        public bool OverlayOpen
        {
            get => _overlay;
            set
            {
                if (_overlay == value) return;
                _overlay = value;
                if (!value) _overlayClosedFrame = Time.frameCount;
                Apply(_overlay || Paused);
            }
        }

        private bool _overlay;
        private int _overlayClosedFrame = -1;

        public event System.Action<bool> PausedChanged;

        private void OnEnable() => SetPaused(false);
        private void OnDisable() => Apply(false);

        private void Update()
        {
            if (_overlay) return;
            if (input.PausePressedThisFrame)
            {
                // The Esc that just closed an overlay must not also pause.
                if (_overlayClosedFrame != Time.frameCount) SetPaused(!Paused);
            }
            else if (Paused && ClickToResume && UnityEngine.InputSystem.Mouse.current != null
                     && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
            {
                SetPaused(false);
            }
            else if (!Paused && Cursor.lockState != CursorLockMode.Locked && Application.isFocused)
            {
                // The editor can steal the lock (Escape in the Game view, alt-tab): treat it as a pause.
                SetPaused(true);
            }
        }

        public void SetPaused(bool paused)
        {
            bool changed = paused != Paused;
            Paused = paused;
            Apply(paused || _overlay);
            if (changed) PausedChanged?.Invoke(paused);
        }

        private void Apply(bool paused)
        {
            Cursor.lockState = paused ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = paused;
            if (input != null) input.Blocked = paused;
        }
    }
}
