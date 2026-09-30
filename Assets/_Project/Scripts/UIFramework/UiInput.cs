using System.Collections.Generic;

namespace Polykov.UI.Framework
{
    /// <summary>Input snapshot for one UI frame, filled by the host (Unity Input System, or a scripted preview).</summary>
    public sealed class UiInput
    {
        public UiVec Mouse;
        public UiVec MouseDelta;
        /// <summary>Wheel steps this frame; positive = scroll down / towards the user.</summary>
        public float Scroll;
        public bool Shift, Ctrl;
        public bool DoubleClick;

        private readonly bool[] _down = new bool[3];
        private readonly bool[] _pressed = new bool[3];
        private readonly bool[] _released = new bool[3];
        private readonly bool[] _consumed = new bool[3];
        private readonly HashSet<UiKey> _keys = new HashSet<UiKey>();
        private bool _scrollConsumed;

        public bool Down(int button) => _down[button];
        /// <summary>Button went down this frame and no widget took it yet.</summary>
        public bool Pressed(int button) => _pressed[button] && !_consumed[button];
        public bool Released(int button) => _released[button];
        public void Consume(int button) => _consumed[button] = true;
        public bool KeyPressed(UiKey key) => _keys.Contains(key);
        public void ConsumeKey(UiKey key) => _keys.Remove(key);
        public float TakeScroll()
        {
            if (_scrollConsumed) return 0f;
            _scrollConsumed = true;
            return Scroll;
        }

        /// <summary>Starts a new frame (the host then sets the fields and calls the setters below).</summary>
        public void BeginFrame(UiVec mouse)
        {
            MouseDelta = mouse - Mouse;
            Mouse = mouse;
            for (int i = 0; i < 3; i++) { _pressed[i] = _released[i] = _consumed[i] = false; }
            _keys.Clear();
            Scroll = 0f;
            _scrollConsumed = false;
            DoubleClick = false;
        }

        public void SetButton(int button, bool down)
        {
            if (down && !_down[button]) _pressed[button] = true;
            if (!down && _down[button]) _released[button] = true;
            _down[button] = down;
        }

        public void PressKey(UiKey key) => _keys.Add(key);
    }
}
