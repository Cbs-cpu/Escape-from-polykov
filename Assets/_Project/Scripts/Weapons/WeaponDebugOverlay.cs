using System.Text;
using UnityEngine;

namespace Polykov.Weapons
{
    /// <summary>WEAPON DEBUG panel (below the movement panel). Shares the F1 toggle through its own flag.</summary>
    public sealed class WeaponDebugOverlay : MonoBehaviour
    {
        [SerializeField] private PlayerWeaponController weapon;
        [SerializeField] private bool visible = true;

        private readonly StringBuilder _builder = new StringBuilder(256);
        private string _text = string.Empty;
        private float _next;
        private GUIStyle _style;

        private void Update()
        {
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.f1Key.wasPressedThisFrame)
                visible = !visible;
            if (!visible || Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.1f;
            WeaponState s = weapon.State;
            _builder.Clear();
            _builder.Append("WEAPON  ").Append(weapon.Data.DisplayName).Append('\n');
            _builder.Append("Ammo: ").Append(s.AmmoInMagazine).Append(s.Chambered ? "+1" : "+0")
                .Append(" / ").Append(s.Reserve).Append(s.SlideLocked ? "   SLIDE LOCKED" : string.Empty).Append('\n');
            _builder.Append("State: ").Append(s.Action.ToString());
            if (s.Action == WeaponAction.Reloading) _builder.Append(" (").Append(s.ReloadKind.ToString()).Append(')');
            _builder.Append("  ").Append((s.ActionProgress * 100f).ToString("0")).Append("%\n");
            _builder.Append("Aim: ").Append(s.Aim.ToString("0.00")).Append("   Spread: ")
                .Append(weapon.CurrentSpread.ToString("0.00")).Append("°");
            _text = _builder.ToString();
        }

        private void OnGUI()
        {
            if (!visible) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = 14 };
                _style.normal.textColor = new Color(0.95f, 0.85f, 0.6f);
            }
            var rect = new Rect(12f, 210f, 320f, 90f);
            GUI.Box(rect, GUIContent.none);
            GUI.Label(new Rect(rect.x + 10f, rect.y + 8f, rect.width - 20f, rect.height - 16f), _text, _style);
        }
    }
}
