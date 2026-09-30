using System.Text;
using Polykov.Movement;
using Polykov.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Polykov.DebugTools
{
    /// <summary>On-screen MOVEMENT DEBUG panel. Toggle with F1. Text refreshes at a fixed rate to keep GC low.</summary>
    public sealed class MovementDebugOverlay : MonoBehaviour
    {
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private PlayerLook look;
        [SerializeField] private bool visible = true;
        [SerializeField, Range(1f, 30f)] private float refreshRate = 10f;

        private readonly StringBuilder _builder = new StringBuilder(512);
        private string _text = string.Empty;
        private float _nextRefresh;
        private float _smoothedFrameTime;
        private float _peakSpeed;
        private GUIStyle _style;
        private GUIStyle _box;

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame) visible = !visible;

            _smoothedFrameTime = Mathf.Lerp(_smoothedFrameTime, Time.unscaledDeltaTime, 0.1f);
            MovementState state = motor.State;
            _peakSpeed = Mathf.Max(_peakSpeed * Mathf.Exp(-Time.deltaTime * 0.5f), state.PlanarSpeed);

            if (!visible || Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + 1f / refreshRate;

            GroundInfo ground = motor.Ground;
            Vector3 v = state.Velocity;
            _builder.Clear();
            _builder.Append("MOVEMENT DEBUG  (F1)\n");
            _builder.Append("FPS: ").Append((1f / Mathf.Max(_smoothedFrameTime, 1e-4f)).ToString("0")).Append("   Tick: ").Append(motor.Tick).Append('\n');
            _builder.Append("Speed: ").Append(state.PlanarSpeed.ToString("0.00")).Append(" m/s   Peak: ").Append(_peakSpeed.ToString("0.00")).Append('\n');
            _builder.Append("Velocity: (").Append(v.x.ToString("0.0")).Append(", ").Append(v.y.ToString("0.0")).Append(", ").Append(v.z.ToString("0.0")).Append(")\n");
            _builder.Append("State: ").Append(state.Locomotion.ToString()).Append("   Profile: ").Append(motor.TuningLabel).Append(" (F2)\n");
            _builder.Append("Grounded: ").Append(ground.Grounded ? "TRUE" : "FALSE").Append('\n');
            _builder.Append("Slope: ").Append(ground.SlopeAngle.ToString("0.0")).Append("°\n");
            _builder.Append("Crouch: ").Append(state.Crouch.ToString("0.00")).Append(motor.CeilingBlocked ? "  (ceiling)" : "").Append('\n');
            _builder.Append("Lean: ").Append(state.Lean.ToString("+0.00;-0.00;0.00"))
                .Append("   Last landing: ").Append(state.LandingImpact.ToString("0.0")).Append(" m/s\n");
            _builder.Append("Yaw: ").Append(look.Yaw.ToString("0.0")).Append("   Pitch: ").Append(look.Pitch.ToString("0.0"));
            _text = _builder.ToString();
        }

        private void OnGUI()
        {
            if (!visible) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = false };
                _style.normal.textColor = new Color(0.85f, 0.95f, 0.8f);
                _box = new GUIStyle(GUI.skin.box);
            }
            var rect = new Rect(12f, 12f, 360f, 210f);
            GUI.Box(rect, GUIContent.none, _box);
            GUI.Label(new Rect(rect.x + 10f, rect.y + 8f, rect.width - 20f, rect.height - 16f), _text, _style);
        }
    }
}
