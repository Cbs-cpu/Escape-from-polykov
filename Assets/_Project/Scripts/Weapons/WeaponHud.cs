using UnityEngine;

namespace Polykov.Weapons
{
    /// <summary>Dev HUD (bottom right): ammo, state and range hits. Not the final UI (Tarkov-style has none).</summary>
    public sealed class WeaponHud : MonoBehaviour
    {
        [SerializeField] private PlayerWeapon weapon;
        [SerializeField] private bool visible = true;

        private GUIStyle _style;
        private string _text = string.Empty;
        private int _magazine = -1;
        private bool _chambered;
        private int _reserve = -1;
        private int _hits = -1;
        private int _mode = -1;
        private string _message;
        private float _messageUntil;
        private GUIStyle _messageStyle;

        private void OnEnable() => weapon.ActionCompleted += OnActionCompleted;
        private void OnDisable() => weapon.ActionCompleted -= OnActionCompleted;

        private void OnActionCompleted(WeaponAction action, WeaponState state)
        {
            if (action != WeaponAction.ChamberCheck) return;
            _message = state.LastChamberCheckLoaded ? "Recámara: con bala" : "Recámara: VACÍA";
            _messageUntil = Time.unscaledTime + 2.5f;
        }

        private void OnGUI()
        {
            if (!visible) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.LowerRight };
                _style.normal.textColor = new Color(0.85f, 0.95f, 0.8f);
            }

            WeaponState s = weapon.State;
            int mode = s.IsReloading ? 1 : s.RoundsLoaded == 0 ? 2 : weapon.Obstructed ? 3 : s.SafetyOn ? 4 : 0;
            // Rebuild the string only when something changes (no per-frame garbage).
            if (s.Magazine != _magazine || s.Chambered != _chambered || s.Reserve != _reserve
                || ShootingTarget.TotalHits != _hits || mode != _mode)
            {
                _magazine = s.Magazine;
                _chambered = s.Chambered;
                _reserve = s.Reserve;
                _hits = ShootingTarget.TotalHits;
                _mode = mode;
                string status = mode == 1 ? "  RECARGANDO" : mode == 2 ? "  VACÍA (R)" : mode == 3 ? "  BLOQUEADA"
                    : mode == 4 ? "  SEGURO (B)" : "";
                _text = weapon.Definition.DisplayName + "   " + s.Magazine + (s.Chambered ? "+1" : "") + " / " + s.Reserve
                        + status + "\nImpactos: " + _hits + "   L inspeccionar · T recámara · B seguro";
            }

            float scale = Mathf.Max(1f, Screen.height / 1080f);
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float w = Screen.width / scale;
            float h = Screen.height / scale;
            GUI.Label(new Rect(w - 420f, h - 70f, 400f, 56f), _text, _style);
            if (_message != null && Time.unscaledTime < _messageUntil)
            {
                if (_messageStyle == null)
                {
                    _messageStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
                    _messageStyle.normal.textColor = new Color(0.95f, 0.9f, 0.7f);
                }
                GUI.Label(new Rect(w * 0.5f - 200f, h * 0.62f, 400f, 30f), _message, _messageStyle);
            }
            GUI.matrix = previous;
        }
    }
}
