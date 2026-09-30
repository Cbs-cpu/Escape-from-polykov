// Signature-only stand-ins for the subset of com.unity.inputsystem used by the project.
// Used by check.py to type-check scripts outside Unity. Never shipped; bodies are not meant to run.
// Signatures copied from com.unity.inputsystem 1.20.0 — add members here when new API is used.
using System;
using UnityEngine;

namespace UnityEngine.InputSystem.Controls
{
    public class ButtonControl : InputControl
    {
        public bool isPressed => false;
        public bool wasPressedThisFrame => false;
        public bool wasReleasedThisFrame => false;
        public float ReadValue() => 0f;
    }

    public class Vector2Control : InputControl
    {
        public Vector2 ReadValue() => default;
    }

    public class StickControl : Vector2Control
    {
    }

    public class KeyControl : ButtonControl
    {
    }
}

namespace UnityEngine.InputSystem
{
    using UnityEngine.InputSystem.Controls;

    public abstract class InputControl
    {
        public InputDevice device => null;
        public string name => null;
        public string path => null;
        public string displayName => null;
    }

    public class InputDevice : InputControl
    {
        public bool added => false;
        public bool enabled => false;
        public int deviceId => 0;
    }

    public class Pointer : InputDevice
    {
        public Vector2Control delta => null;
        public Vector2Control position => null;
        public ButtonControl press => null;
        public static Pointer current => null;
    }

    public class Mouse : Pointer
    {
        public ButtonControl leftButton => null;
        public ButtonControl rightButton => null;
        public ButtonControl middleButton => null;
        public Vector2Control scroll => null;
        public new static Mouse current => null;
    }

    public enum Key
    {
        None, Space, Enter, Tab, Backquote, Quote, Semicolon, Comma, Period, Slash, Backslash, LeftBracket, RightBracket,
        Minus, Equals, A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
        Digit1, Digit2, Digit3, Digit4, Digit5, Digit6, Digit7, Digit8, Digit9, Digit0,
        LeftShift, RightShift, LeftAlt, RightAlt, LeftCtrl, RightCtrl, Escape, LeftArrow, RightArrow, UpArrow, DownArrow,
        Backspace, PageDown, PageUp, Home, End, Insert, Delete,
        F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,
    }

    public class Keyboard : InputDevice
    {
        public static Keyboard current => null;
        public KeyControl this[Key key] => null;
        public KeyControl anyKey => null;
        public KeyControl spaceKey => null;
        public KeyControl enterKey => null;
        public KeyControl tabKey => null;
        public KeyControl escapeKey => null;
        public KeyControl backquoteKey => null;
        public KeyControl leftShiftKey => null;
        public KeyControl leftAltKey => null;
        public KeyControl leftCtrlKey => null;
        public KeyControl f1Key => null;
        public KeyControl f2Key => null;
        public KeyControl f3Key => null;
        public KeyControl f4Key => null;
        public KeyControl f5Key => null;
        public KeyControl f6Key => null;
        public KeyControl f7Key => null;
        public KeyControl f8Key => null;
        public KeyControl f9Key => null;
        public KeyControl f10Key => null;
        public KeyControl f11Key => null;
        public KeyControl f12Key => null;
        public KeyControl digit1Key => null;
        public KeyControl digit2Key => null;
        public KeyControl digit3Key => null;
        public KeyControl digit4Key => null;
        public KeyControl digit5Key => null;
        public KeyControl upArrowKey => null;
        public KeyControl downArrowKey => null;
        public KeyControl leftArrowKey => null;
        public KeyControl rightArrowKey => null;
        public KeyControl pageUpKey => null;
        public KeyControl pageDownKey => null;
        public KeyControl minusKey => null;
        public KeyControl equalsKey => null;
        public KeyControl leftBracketKey => null;
        public KeyControl rightBracketKey => null;
        public KeyControl backspaceKey => null;
        public KeyControl deleteKey => null;
    }

    public class Gamepad : InputDevice
    {
        public static Gamepad current => null;
        public StickControl leftStick => null;
        public StickControl rightStick => null;
        public ButtonControl buttonSouth => null;
        public ButtonControl buttonNorth => null;
        public ButtonControl buttonEast => null;
        public ButtonControl buttonWest => null;
        public ButtonControl leftTrigger => null;
        public ButtonControl rightTrigger => null;
        public ButtonControl leftShoulder => null;
        public ButtonControl rightShoulder => null;
        public ButtonControl startButton => null;
        public ButtonControl selectButton => null;
        public ButtonControl leftStickButton => null;
        public ButtonControl rightStickButton => null;
        public void SetMotorSpeeds(float lowFrequency, float highFrequency) { }
        public void ResetHaptics() { }
    }

    public enum InputActionPhase { Disabled, Waiting, Started, Performed, Canceled }

    public sealed class InputAction
    {
        public struct CallbackContext
        {
            public InputAction action => null;
            public InputControl control => null;
            public InputActionPhase phase => default;
            public bool started => false;
            public bool performed => false;
            public bool canceled => false;
            public TValue ReadValue<TValue>() where TValue : struct => default;
            public bool ReadValueAsButton() => false;
        }

        public string name => null;
        public bool enabled => false;
        public bool triggered => false;
        public InputActionPhase phase => default;
        public InputControl activeControl => null;
        public InputActionMap actionMap => null;
        public event Action<CallbackContext> started { add { } remove { } }
        public event Action<CallbackContext> performed { add { } remove { } }
        public event Action<CallbackContext> canceled { add { } remove { } }
        public TValue ReadValue<TValue>() where TValue : struct => default;
        public object ReadValueAsObject() => null;
        public bool IsPressed() => false;
        public bool IsInProgress() => false;
        public bool WasPressedThisFrame() => false;
        public bool WasReleasedThisFrame() => false;
        public bool WasPerformedThisFrame() => false;
        public bool WasCompletedThisFrame() => false;
        public float GetTimeoutCompletionPercentage() => 0f;
        public void Enable() { }
        public void Disable() { }
    }

    public sealed class InputActionMap
    {
        public string name => null;
        public bool enabled => false;
        public InputActionAsset asset => null;
        public InputAction FindAction(string actionNameOrId, bool throwIfNotFound = false) => null;
        public InputAction this[string actionNameOrId] => null;
        public void Enable() { }
        public void Disable() { }
    }

    public class InputActionAsset : ScriptableObject
    {
        public InputActionMap FindActionMap(string nameOrId, bool throwIfNotFound = false) => null;
        public InputAction FindAction(string actionNameOrId, bool throwIfNotFound = false) => null;
        public InputAction this[string actionNameOrId] => null;
        public void Enable() { }
        public void Disable() { }
    }

    public static class InputSystem
    {
        public static event Action<InputDevice, InputDeviceChange> onDeviceChange { add { } remove { } }
    }

    public enum InputDeviceChange { Added, Removed, Disconnected, Reconnected, Enabled, Disabled, UsageChanged, ConfigurationChanged, SoftReset, HardReset }
}
