using Polykov.Movement;
using Polykov.Player;
using UnityEngine;

namespace Polykov.UI
{
    /// <summary>Thin stamina bar at the bottom center; only visible while not full. Dev placeholder UI.</summary>
    public sealed class StaminaBar : MonoBehaviour
    {
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private Vector2 size = new Vector2(220f, 5f);

        private float _alpha;
        private Texture2D _pixel;

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            MovementState state = motor.State;
            float fraction = state.StaminaFraction(motor.Tuning);
            _alpha = Mathf.MoveTowards(_alpha, fraction < 0.999f ? 1f : 0f, Time.unscaledDeltaTime * 2f);
            if (_alpha <= 0f) return;
            if (_pixel == null)
            {
                _pixel = new Texture2D(1, 1);
                _pixel.SetPixel(0, 0, Color.white);
                _pixel.Apply();
            }

            float scale = Mathf.Max(1f, Screen.height / 1080f);
            var back = new Rect((Screen.width - size.x * scale) * 0.5f, Screen.height - 40f * scale, size.x * scale, size.y * scale);
            Color old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.45f * _alpha);
            GUI.DrawTexture(back, _pixel);
            GUI.color = state.Exhausted ? new Color(0.9f, 0.35f, 0.25f, _alpha) : new Color(0.85f, 0.95f, 0.8f, 0.85f * _alpha);
            GUI.DrawTexture(new Rect(back.x, back.y, back.width * fraction, back.height), _pixel);
            GUI.color = old;
        }
    }
}
