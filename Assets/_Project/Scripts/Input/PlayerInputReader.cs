using Polykov.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Polykov.Input
{
    /// <summary>
    /// Adapter between Unity Input System and gameplay. Exposes plain values only,
    /// so gameplay code never depends on devices or bindings.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class PlayerInputReader : MonoBehaviour
    {
        [SerializeField] private InputActionAsset actions;
        [SerializeField] private string mapName = "Player";

        private InputActionMap _map;
        private InputAction _move;
        private InputAction _look;
        private InputAction _sprint;
        private InputAction _walk;
        private InputAction _pause;
        private InputAction _jump;
        private InputAction _lean;
        private InputAction _crouch;
        private InputAction _fire;
        private InputAction _aim;
        private InputAction _reload;
        private InputAction _safety;
        private InputAction _inspect;
        private InputAction _chamberCheck;
        private bool _jumpLatched;
        private bool _safetyLatched;
        private bool _inspectLatched;
        private bool _chamberCheckLatched;
        private bool _fireLatched;
        private bool _reloadLatched;
        private bool _crouchToggled;

        /// <summary>When true, gameplay input reads as neutral (e.g. cursor unlocked, menus open).</summary>
        public bool Blocked { get; set; }

        public Vector2 Move => Blocked ? Vector2.zero : _move.ReadValue<Vector2>();
        public bool SprintHeld => !Blocked && _sprint.IsPressed();
        public bool WalkHeld => !Blocked && _walk.IsPressed();
        /// <summary>-1 = lean left (Q), +1 = lean right (E).</summary>
        public float Lean => Blocked ? 0f : Mathf.Clamp(_lean.ReadValue<float>(), -1f, 1f);
        public bool PausePressedThisFrame => _pause.WasPressedThisFrame();

        /// <summary>
        /// Crouch intent. Hold mode: key held. Toggle mode (UserSettings.ToggleCrouch): press toggles,
        /// sprinting or jumping stands you up.
        /// </summary>
        public bool FireHeld => !Blocked && _fire.IsPressed();
        public bool AimHeld => !Blocked && _aim.IsPressed();

        /// <summary>True once per trigger press, latched between simulation ticks.</summary>
        public bool ConsumeFirePressed()
        {
            bool pressed = _fireLatched;
            _fireLatched = false;
            return pressed;
        }

        /// <summary>True once per reload press, latched between simulation ticks.</summary>
        public bool ConsumeReload()
        {
            bool pressed = _reloadLatched;
            _reloadLatched = false;
            return pressed;
        }

        /// <summary>Weapon manipulation presses since the last tick (each true once).</summary>
        public void ConsumeWeaponActions(out bool safety, out bool inspect, out bool chamberCheck)
        {
            safety = _safetyLatched;
            inspect = _inspectLatched;
            chamberCheck = _chamberCheckLatched;
            _safetyLatched = _inspectLatched = _chamberCheckLatched = false;
        }

        public bool Crouch => !Blocked && (UserSettings.ToggleCrouch ? _crouchToggled : _crouch.IsPressed());

        /// <summary>
        /// Returns true once per jump press. Presses are latched between simulation ticks so none are lost
        /// when the frame rate is higher than the tick rate.
        /// </summary>
        public bool ConsumeJump()
        {
            bool pressed = _jumpLatched;
            _jumpLatched = false;
            return pressed;
        }

        private void Awake()
        {
            _map = actions.FindActionMap(mapName, throwIfNotFound: true);
            _move = _map.FindAction("Move", true);
            _look = _map.FindAction("Look", true);
            _sprint = _map.FindAction("Sprint", true);
            _walk = _map.FindAction("Walk", true);
            _pause = _map.FindAction("Pause", true);
            _jump = _map.FindAction("Jump", true);
            _lean = _map.FindAction("Lean", true);
            _crouch = _map.FindAction("Crouch", true);
            _fire = _map.FindAction("Fire", true);
            _aim = _map.FindAction("Aim", true);
            _reload = _map.FindAction("Reload", true);
            // Optional actions: older copies of the input asset may not have them yet.
            _safety = _map.FindAction("Safety");
            _inspect = _map.FindAction("Inspect");
            _chamberCheck = _map.FindAction("ChamberCheck");
        }

        private void Update()
        {
            if (Blocked) return;
            if (_jump.WasPressedThisFrame()) _jumpLatched = true;
            if (_fire.WasPressedThisFrame()) _fireLatched = true;
            if (_reload.WasPressedThisFrame()) _reloadLatched = true;
            if (_safety != null && _safety.WasPressedThisFrame()) _safetyLatched = true;
            if (_inspect != null && _inspect.WasPressedThisFrame()) _inspectLatched = true;
            if (_chamberCheck != null && _chamberCheck.WasPressedThisFrame()) _chamberCheckLatched = true;
            if (_crouch.WasPressedThisFrame()) _crouchToggled = !_crouchToggled;
            if (_sprint.WasPressedThisFrame() || _jump.WasPressedThisFrame()) _crouchToggled = false;
        }

        private void OnEnable() => _map?.Enable();
        private void OnDisable() => _map?.Disable();

        /// <summary>
        /// Look rotation for this frame in degrees (x = yaw, y = pitch up).
        /// Mouse is frame-rate independent by nature (pixels); sticks are rates and get scaled by deltaTime.
        /// </summary>
        public Vector2 ReadLookDelta(float mouseDegreesPerPixel, Vector2 stickDegreesPerSecond, float deltaTime)
        {
            if (Blocked) return Vector2.zero;
            Vector2 raw = _look.ReadValue<Vector2>();
            InputDevice device = _look.activeControl?.device;
            if (device is Pointer) return raw * mouseDegreesPerPixel;

            // Quadratic response curve gives fine aim near the center of the stick.
            Vector2 curved = raw * raw.magnitude;
            return Vector2.Scale(curved, stickDegreesPerSecond) * deltaTime;
        }
    }
}
