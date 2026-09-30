using Polykov.Core;
using Polykov.Movement;
using Polykov.Player;
using Polykov.UI.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Polykov.UI
{
    /// <summary>
    /// Pause menu (Esc) in the lobby's Tarkov style (<see cref="PauseScreen"/>, drawn by a <see cref="UiSurface"/>):
    /// player preferences, gore, game-feel presets and the controls. F2 cycles movement presets without pausing.
    /// </summary>
    public sealed class PauseMenu : MonoBehaviour
    {
        [SerializeField] private PlayerCursor cursor;
        [SerializeField] private PlayerMotor motor;

        private const string LobbySceneName = "Lobby";

        // -1 = MovementSettings asset, otherwise index into MovementPresets.
        private int _preset = -1;
        private float _toastUntil;
        private string _toast;
        private UiSurface _surface;
        private readonly PauseSettings _settings = new PauseSettings();

        private void Awake()
        {
            _surface = gameObject.AddComponent<UiSurface>();
            _surface.Depth = -10;
            _surface.Build += OnUi;
            _settings.Presets = MovementPresets.Names;
            _settings.CanReturnToLobby = Application.CanStreamedLevelBeLoaded(LobbySceneName);
        }

        private void OnDestroy()
        {
            if (_surface != null) _surface.Build -= OnUi;
        }

        private void OnEnable() => cursor.ClickToResume = false;
        private void OnDisable() => cursor.ClickToResume = true;

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f2Key.wasPressedThisFrame)
            {
                ApplyPreset(_preset + 1 >= MovementPresets.Count ? -1 : _preset + 1);
                ShowToast("MOVIMIENTO: " + motor.TuningLabel.ToUpperInvariant() + "  (F2)");
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

        private void OnUi(Ui ui)
        {
            if (_toast != null && Time.unscaledTime < _toastUntil)
            {
                float w = ui.Draw.TextWidth(_toast, UiFont.Bold, UiTheme.SizeLabel) + 40f;
                var r = new UiRect((ui.Width - w) * 0.5f, ui.Height * 0.12f, w, 38f);
                ui.Fill(r, UiTheme.Panel);
                ui.Frame(r, UiTheme.BorderLight);
                ui.Label(r, _toast, UiTheme.SizeLabel, UiFont.Bold, UiAlign.Center, UiTheme.TextBright);
            }
            if (!cursor.Paused) return;

            Read();
            PauseAction action = PauseScreen.Frame(ui, _settings);
            Write();
            if (_settings.Preset != _preset) ApplyPreset(_settings.Preset);
            switch (action)
            {
                case PauseAction.Resume:
                    UserSettings.Flush();
                    cursor.SetPaused(false);
                    break;
                case PauseAction.ReturnToLobby:
                    UserSettings.Flush();
                    UnityEngine.SceneManagement.SceneManager.LoadScene(LobbySceneName);
                    break;
                case PauseAction.ResetSettings:
                    UserSettings.ResetToDefaults();
                    break;
            }
        }

        private void Read()
        {
            _settings.Sensitivity = UserSettings.MouseSensitivity;
            _settings.Fov = UserSettings.Fov;
            _settings.HeadBob = UserSettings.HeadBob;
            _settings.Shake = UserSettings.CameraShake;
            _settings.Volume = UserSettings.MasterVolume;
            _settings.InvertY = UserSettings.InvertY;
            _settings.ToggleCrouch = UserSettings.ToggleCrouch;
            _settings.Dismemberment = UserSettings.Dismemberment;
            _settings.Gore = (int)UserSettings.Gore;
            _settings.Preset = _preset;
        }

        private void Write()
        {
            // Setters raise UserSettings.Changed: only write what the menu actually changed.
            if (_settings.Sensitivity != UserSettings.MouseSensitivity) UserSettings.MouseSensitivity = _settings.Sensitivity;
            if (_settings.Fov != UserSettings.Fov) UserSettings.Fov = _settings.Fov;
            if (_settings.HeadBob != UserSettings.HeadBob) UserSettings.HeadBob = _settings.HeadBob;
            if (_settings.Shake != UserSettings.CameraShake) UserSettings.CameraShake = _settings.Shake;
            if (_settings.Volume != UserSettings.MasterVolume) UserSettings.MasterVolume = _settings.Volume;
            if (_settings.InvertY != UserSettings.InvertY) UserSettings.InvertY = _settings.InvertY;
            if (_settings.ToggleCrouch != UserSettings.ToggleCrouch) UserSettings.ToggleCrouch = _settings.ToggleCrouch;
            if (_settings.Dismemberment != UserSettings.Dismemberment) UserSettings.Dismemberment = _settings.Dismemberment;
            if (_settings.Gore != (int)UserSettings.Gore) UserSettings.Gore = (GoreLevel)_settings.Gore;
        }
    }
}
