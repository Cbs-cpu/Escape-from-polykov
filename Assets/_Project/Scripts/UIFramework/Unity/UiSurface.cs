using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Polykov.UI.Framework
{
    /// <summary>
    /// Runs an immediate-mode <see cref="Ui"/> every frame: reads the Input System in Update, calls <see cref="Build"/>
    /// and replays the recorded drawing in OnGUI. Layout is in 1080p reference pixels (height 1080, width by aspect).
    /// </summary>
    public sealed class UiSurface : MonoBehaviour
    {
        public const float ReferenceHeight = 1080f;
        private const string RegularFont = "Fonts/PolykovGrid-Regular";
        private const string BoldFont = "Fonts/PolykovGrid-Bold";

        /// <summary>Called once per frame with the UI context (draw and handle input here).</summary>
        public event Action<Ui> Build;
        /// <summary>Lower draws on top (IMGUI GUI.depth).</summary>
        public int Depth;

        private ImguiBackend _backend;
        private UiInput _input;
        private float _lastClickTime = -1f;
        private UiVec _lastClickPos;

        public Ui Ui { get; private set; }
        public float Scale { get; private set; } = 1f;

        private void Awake()
        {
            _backend = new ImguiBackend(Resources.Load<Font>(RegularFont), Resources.Load<Font>(BoldFont));
            _input = new UiInput();
            Ui = new Ui(_backend, _input);
        }

        private void Update()
        {
            Scale = Mathf.Max(0.1f, Screen.height / ReferenceHeight);
            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;
            Vector2 pos = mouse != null ? mouse.position.ReadValue() : Vector2.zero;
            var logical = new UiVec(pos.x / Scale, (Screen.height - pos.y) / Scale);
            _input.BeginFrame(logical);
            if (mouse != null)
            {
                _input.SetButton(0, mouse.leftButton.isPressed);
                _input.SetButton(1, mouse.rightButton.isPressed);
                _input.SetButton(2, mouse.middleButton.isPressed);
                float wheel = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 0.01f) _input.Scroll = -Mathf.Sign(wheel) * Mathf.Max(1f, Mathf.Abs(wheel) / 120f);
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    float now = Time.unscaledTime;
                    _input.DoubleClick = now - _lastClickTime < 0.3f && (logical - _lastClickPos).Length < 8f;
                    _lastClickTime = _input.DoubleClick ? -1f : now;
                    _lastClickPos = logical;
                }
            }
            if (keyboard != null)
            {
                _input.Ctrl = keyboard.ctrlKey.isPressed;
                _input.Shift = keyboard.shiftKey.isPressed;
                if (keyboard.escapeKey.wasPressedThisFrame) _input.PressKey(UiKey.Escape);
                if (keyboard.rKey.wasPressedThisFrame) _input.PressKey(UiKey.R);
                if (keyboard.deleteKey.wasPressedThisFrame) _input.PressKey(UiKey.Delete);
                if (keyboard.enterKey.wasPressedThisFrame) _input.PressKey(UiKey.Enter);
                if (keyboard.tabKey.wasPressedThisFrame) _input.PressKey(UiKey.Tab);
                if (keyboard.spaceKey.wasPressedThisFrame) _input.PressKey(UiKey.Space);
                if (keyboard.iKey.wasPressedThisFrame) _input.PressKey(UiKey.I);
                if (keyboard.eKey.wasPressedThisFrame) _input.PressKey(UiKey.E);
            }

            _backend.BeginFrame(Scale);
            Ui.BeginFrame(Screen.width / Scale, ReferenceHeight, Time.unscaledTime);
            Build?.Invoke(Ui);
            Ui.EndFrame();
        }

        private void OnGUI()
        {
            GUI.depth = Depth;
            _backend.Replay();
        }

        /// <summary>Reference-pixel point of a screen-space position (Camera.WorldToScreenPoint output).</summary>
        public UiVec FromScreen(Vector3 screen) => new UiVec(screen.x / Scale, (Screen.height - screen.y) / Scale);
    }
}
