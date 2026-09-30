using System;
using UnityEngine;

namespace Polykov.Core
{
    /// <summary>How much blood and wounds are shown (presentation only).</summary>
    public enum GoreLevel : byte
    {
        Off = 0,
        Reduced = 1,
        Full = 2,
    }

    /// <summary>
    /// Player preferences (sensitivity, FOV, comfort). Local only, never replicated, persisted in PlayerPrefs.
    /// Presentation code reads these every frame; the menu writes them.
    /// </summary>
    public static class UserSettings
    {
        public const float DefaultMouseSensitivity = 0.08f;
        public const float DefaultFov = 62f;

        private const string Prefix = "polykov.settings.";

        private static bool _loaded;
        private static float _mouseSensitivity;
        private static float _fov;
        private static float _headBob;
        private static float _cameraShake;
        private static bool _invertY;
        private static bool _toggleCrouch;
        private static float _masterVolume;
        private static GoreLevel _gore = GoreLevel.Full;
        private static bool _dismemberment = true;

        /// <summary>Raised after any value changes.</summary>
        public static event Action Changed;

        /// <summary>Degrees of rotation per mouse pixel.</summary>
        public static float MouseSensitivity
        {
            get { EnsureLoaded(); return _mouseSensitivity; }
            set => Set(ref _mouseSensitivity, Mathf.Clamp(value, 0.005f, 0.5f), "mouseSensitivity");
        }

        /// <summary>Base vertical field of view in degrees.</summary>
        public static float Fov
        {
            get { EnsureLoaded(); return _fov; }
            set => Set(ref _fov, Mathf.Clamp(value, 50f, 90f), "fovTarkov");
        }

        /// <summary>Head bob and strafe tilt multiplier (accessibility). 0 = off.</summary>
        public static float HeadBob
        {
            get { EnsureLoaded(); return _headBob; }
            set => Set(ref _headBob, Mathf.Clamp01(value), "headBob");
        }

        /// <summary>Landing dip, recoil camera kick and other camera impulses. 0 = off.</summary>
        public static float CameraShake
        {
            get { EnsureLoaded(); return _cameraShake; }
            set => Set(ref _cameraShake, Mathf.Clamp01(value), "cameraShake");
        }

        public static bool InvertY
        {
            get { EnsureLoaded(); return _invertY; }
            set => Set(ref _invertY, value, "invertY");
        }

        /// <summary>Crouch key toggles instead of hold.</summary>
        public static bool ToggleCrouch
        {
            get { EnsureLoaded(); return _toggleCrouch; }
            set => Set(ref _toggleCrouch, value, "toggleCrouch");
        }

        /// <summary>Master volume 0..1.</summary>
        public static float MasterVolume
        {
            get { EnsureLoaded(); return _masterVolume; }
            set => Set(ref _masterVolume, Mathf.Clamp01(value), "masterVolume");
        }

        /// <summary>Blood, wound decals and splats: Full, Reduced (fewer, smaller) or Off.</summary>
        public static GoreLevel Gore
        {
            get { EnsureLoaded(); return _gore; }
            set
            {
                EnsureLoaded();
                if (_gore == value) return;
                _gore = value;
                PlayerPrefs.SetInt(Prefix + "gore", (int)value);
                Changed?.Invoke();
            }
        }

        /// <summary>Limbs can be severed (and the head, very rarely).</summary>
        public static bool Dismemberment
        {
            get { EnsureLoaded(); return _dismemberment; }
            set => Set(ref _dismemberment, value, "dismemberment");
        }

        public static void ResetToDefaults()
        {
            _mouseSensitivity = DefaultMouseSensitivity;
            _fov = DefaultFov;
            _headBob = 1f;
            _cameraShake = 1f;
            _invertY = false;
            _toggleCrouch = false;
            _masterVolume = 0.8f;
            _gore = GoreLevel.Full;
            _dismemberment = true;
            _loaded = true;
            Save();
            Changed?.Invoke();
        }

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            _mouseSensitivity = PlayerPrefs.GetFloat(Prefix + "mouseSensitivity", DefaultMouseSensitivity);
            _fov = Mathf.Clamp(PlayerPrefs.GetFloat(Prefix + "fovTarkov", DefaultFov), 50f, 90f);
            _headBob = PlayerPrefs.GetFloat(Prefix + "headBob", 1f);
            _cameraShake = PlayerPrefs.GetFloat(Prefix + "cameraShake", 1f);
            _invertY = PlayerPrefs.GetInt(Prefix + "invertY", 0) != 0;
            _toggleCrouch = PlayerPrefs.GetInt(Prefix + "toggleCrouch", 0) != 0;
            _masterVolume = PlayerPrefs.GetFloat(Prefix + "masterVolume", 0.8f);
            _gore = (GoreLevel)Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "gore", (int)GoreLevel.Full), 0, 2);
            _dismemberment = PlayerPrefs.GetInt(Prefix + "dismemberment", 1) != 0;
        }

        private static void Set(ref float field, float value, string key)
        {
            EnsureLoaded();
            if (Mathf.Approximately(field, value)) return;
            field = value;
            PlayerPrefs.SetFloat(Prefix + key, value);
            Changed?.Invoke();
        }

        private static void Set(ref bool field, bool value, string key)
        {
            EnsureLoaded();
            if (field == value) return;
            field = value;
            PlayerPrefs.SetInt(Prefix + key, value ? 1 : 0);
            Changed?.Invoke();
        }

        private static void Save()
        {
            PlayerPrefs.SetFloat(Prefix + "mouseSensitivity", _mouseSensitivity);
            PlayerPrefs.SetFloat(Prefix + "fov", _fov);
            PlayerPrefs.SetFloat(Prefix + "headBob", _headBob);
            PlayerPrefs.SetFloat(Prefix + "cameraShake", _cameraShake);
            PlayerPrefs.SetInt(Prefix + "invertY", _invertY ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "toggleCrouch", _toggleCrouch ? 1 : 0);
            PlayerPrefs.SetFloat(Prefix + "masterVolume", _masterVolume);
            PlayerPrefs.SetInt(Prefix + "gore", (int)_gore);
            PlayerPrefs.SetInt(Prefix + "dismemberment", _dismemberment ? 1 : 0);
        }

        /// <summary>Writes pending values to disk (call when closing the menu).</summary>
        public static void Flush() => PlayerPrefs.Save();

        // Domain reload may be disabled in the editor: reset statics on play.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _loaded = false;
            Changed = null;
        }
    }
}
