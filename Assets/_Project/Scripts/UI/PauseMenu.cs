using Polykov.Core;
using Polykov.Movement;
using Polykov.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Polykov.UI
{
    /// <summary>
    /// Pause menu (Esc): player preferences and game-feel presets. IMGUI on purpose — it is a dev/test menu
    /// that needs no scene setup; the shipped menu will be rebuilt with UI Toolkit.
    /// F2 cycles movement presets without pausing, to compare feel back to back.
    /// </summary>
    public sealed class PauseMenu : MonoBehaviour
    {
        [SerializeField] private PlayerCursor cursor;
        [SerializeField] private PlayerMotor motor;

        private const float Width = 440f;

        // -1 = MovementSettings asset, otherwise index into MovementPresets.
        private int _preset = -1;
        private float _toastUntil;
        private string _toast;
        private GUIStyle _title;
        private GUIStyle _label;
        private GUIStyle _small;
        private float _scale = 1f;

        private void OnEnable() => cursor.ClickToResume = false;
        private void OnDisable() => cursor.ClickToResume = true;

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f2Key.wasPressedThisFrame)
            {
                ApplyPreset(_preset + 1 >= MovementPresets.Count ? -1 : _preset + 1);
                ShowToast("Movimiento: " + motor.TuningLabel + "  (F2)");
            }
        }

        private void ApplyPreset(int preset)
        {
            _preset = preset;
            if (preset < 0) motor.ClearTuningOverride();
            else motor.SetTuningOverride(MovementPresets.Get(preset), MovementPresets.Names[preset]);
        }

        private void ShowToast(string text)
        {
            _toast = text;
            _toastUntil = Time.unscaledTime + 2f;
        }

        private void OnGUI()
        {
            EnsureStyles();
            _scale = Mathf.Max(1f, Screen.height / 1080f);
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(_scale, _scale, 1f));
            float screenW = Screen.width / _scale;
            float screenH = Screen.height / _scale;

            if (_toast != null && Time.unscaledTime < _toastUntil)
                GUI.Label(new Rect(screenW * 0.5f - 200f, screenH * 0.12f, 400f, 30f), _toast, _title);

            if (cursor.Paused) DrawMenu(screenW, screenH);
            GUI.matrix = previous;
        }

        private void DrawMenu(float screenW, float screenH)
        {
            float height = 600f;
            var area = new Rect((screenW - Width) * 0.5f, (screenH - height) * 0.5f, Width, height);
            GUI.Box(area, GUIContent.none);
            GUI.Box(area, GUIContent.none);
            GUILayout.BeginArea(new Rect(area.x + 20f, area.y + 14f, area.width - 40f, area.height - 28f));

            GUILayout.Label("PAUSA", _title);
            GUILayout.Space(8f);

            UserSettings.MouseSensitivity = Slider("Sensibilidad ratón", UserSettings.MouseSensitivity, 0.01f, 0.3f, "0.000");
            UserSettings.Fov = Slider("Campo de visión (FOV)", UserSettings.Fov, 60f, 110f, "0");
            UserSettings.HeadBob = Slider("Balanceo de cámara", UserSettings.HeadBob, 0f, 1f, "0%");
            UserSettings.CameraShake = Slider("Sacudidas de cámara", UserSettings.CameraShake, 0f, 1f, "0%");
            UserSettings.MasterVolume = Slider("Volumen", UserSettings.MasterVolume, 0f, 1f, "0%");
            UserSettings.InvertY = GUILayout.Toggle(UserSettings.InvertY, " Invertir eje Y");
            UserSettings.ToggleCrouch = GUILayout.Toggle(UserSettings.ToggleCrouch, " Agacharse alterna (en vez de mantener)");

            GUILayout.Space(12f);
            GUILayout.Label("Perfil de movimiento (F2 para alternar jugando)", _label);
            GUILayout.BeginHorizontal();
            if (PresetButton("Asset", -1)) ApplyPreset(-1);
            for (int i = 0; i < MovementPresets.Count; i++)
                if (PresetButton(MovementPresets.Names[i], i)) ApplyPreset(i);
            GUILayout.EndHorizontal();
            GUILayout.Label("\"Asset\" usa MovementSettings.asset (editable en Play Mode).", _small);

            GUILayout.Space(12f);
            GUILayout.Label("WASD mover · Shift esprintar · Alt andar · Espacio saltar · C agacharse · Q/E inclinarse\n" +
                            "Clic izq. disparar · Clic der. apuntar · R recargar · F1 debug · Esc pausa", _small);

            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Restablecer ajustes", GUILayout.Height(30f))) UserSettings.ResetToDefaults();
            if (GUILayout.Button("Reanudar", GUILayout.Height(30f)))
            {
                UserSettings.Flush();
                cursor.SetPaused(false);
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private bool PresetButton(string text, int index)
        {
            Color old = GUI.backgroundColor;
            if (_preset == index) GUI.backgroundColor = new Color(0.55f, 0.85f, 0.45f);
            bool clicked = GUILayout.Button(text, GUILayout.Height(28f));
            GUI.backgroundColor = old;
            return clicked;
        }

        private float Slider(string label, float value, float min, float max, string format)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, _label, GUILayout.Width(190f));
            float result = GUILayout.HorizontalSlider(value, min, max, GUILayout.ExpandWidth(true));
            GUILayout.Label(format == "0%" ? (result * 100f).ToString("0") + "%" : result.ToString(format), _label,
                GUILayout.Width(52f));
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);
            return result;
        }

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _title.normal.textColor = new Color(0.85f, 0.95f, 0.8f);
            _label = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
            _small.normal.textColor = new Color(0.75f, 0.78f, 0.72f);
        }
    }
}
