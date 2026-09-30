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

        private void OnGUI()
        {
            if (!visible) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.LowerRight };
                _style.normal.textColor = new Color(0.85f, 0.95f, 0.8f);
            }

            WeaponState s = weapon.State;
            int mode = s.IsReloading ? 1 : s.RoundsLoaded == 0 ? 2 : weapon.Obstructed ? 3 : 0;
            // Rebuild the string only when something changes (no per-frame garbage).
            if (s.Magazine != _magazine || s.Chambered != _chambered || s.Reserve != _reserve
                || ShootingTarget.TotalHits != _hits || mode != _mode)
            {
                _magazine = s.Magazine;
                _chambered = s.Chambered;
                _reserve = s.Reserve;
                _hits = ShootingTarget.TotalHits;
                _mode = mode;
                string status = mode == 1 ? "  RECARGANDO" : mode == 2 ? "  VACÍA (R)" : mode == 3 ? "  BLOQUEADA" : "";
                _text = weapon.Definition.DisplayName + "   " + s.Magazine + (s.Chambered ? "+1" : "") + " / " + s.Reserve
                        + status + "\nImpactos: " + _hits + "   F3 dianas · F4 munición";
            }

            float scale = Mathf.Max(1f, Screen.height / 1080f);
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float w = Screen.width / scale;
            float h = Screen.height / scale;
            GUI.Label(new Rect(w - 420f, h - 70f, 400f, 56f), _text, _style);
            GUI.matrix = previous;
        }
    }
}
