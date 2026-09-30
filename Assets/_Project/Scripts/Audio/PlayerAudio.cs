using Polykov.Core;
using Polykov.Movement;
using Polykov.Player;
using Polykov.Weapons;
using UnityEngine;

namespace Polykov.Audio
{
    /// <summary>
    /// Local player's sounds: weapon, reload, footsteps (from distance travelled, so cadence always matches the
    /// real speed), jump and landing. Placeholder synthesized clips until real audio assets exist.
    /// </summary>
    public sealed class PlayerAudio : MonoBehaviour
    {
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private PlayerWeapon weapon;

        [Header("Footsteps")]
        [Tooltip("Meters per step at walk speed and at sprint speed.")]
        [SerializeField] private Vector2 strideLength = new Vector2(0.62f, 1.05f);
        [SerializeField, Range(0f, 1f)] private float footstepVolume = 0.35f;
        [SerializeField, Range(0f, 1f)] private float crouchFootstepVolume = 0.12f;

        [Header("Weapon")]
        [SerializeField, Range(0f, 1f)] private float gunshotVolume = 0.9f;
        [SerializeField, Range(0f, 1f)] private float handlingVolume = 0.5f;

        private const int Voices = 6;
        private AudioSource[] _sources;
        private int _nextVoice;
        private float _stepDistance;
        private int _stepIndex;
        private Vector3 _lastPosition;

        private void Awake()
        {
            _sources = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++)
            {
                AudioSource source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f; // Local player: 2D. Remote players will use 3D sources.
                _sources[i] = source;
            }
            _lastPosition = transform.position;
        }

        private void OnEnable()
        {
            motor.Landed += OnLanded;
            motor.Jumped += OnJumped;
            if (weapon == null) return;
            weapon.Fired += OnFired;
            weapon.DryFired += OnDryFired;
            weapon.ReloadStarted += OnReloadStarted;
            weapon.MagazineInserted += OnMagazineInserted;
            weapon.ReloadFinished += OnReloadFinished;
        }

        private void OnDisable()
        {
            motor.Landed -= OnLanded;
            motor.Jumped -= OnJumped;
            if (weapon == null) return;
            weapon.Fired -= OnFired;
            weapon.DryFired -= OnDryFired;
            weapon.ReloadStarted -= OnReloadStarted;
            weapon.MagazineInserted -= OnMagazineInserted;
            weapon.ReloadFinished -= OnReloadFinished;
        }

        private void Update()
        {
            AudioListener.volume = UserSettings.MasterVolume;

            MovementState state = motor.State;
            Vector3 position = transform.position;
            Vector3 delta = position - _lastPosition;
            _lastPosition = position;
            if (!state.Grounded || state.PlanarSpeed < 0.3f)
            {
                // Next step lands halfway into a stride after starting to move.
                _stepDistance = Mathf.Min(_stepDistance, 0.5f);
                return;
            }

            MovementTuning tuning = motor.Tuning;
            float t = Mathf.InverseLerp(tuning.WalkSpeed, tuning.SprintSpeed, state.PlanarSpeed);
            float stride = Mathf.Lerp(strideLength.x, strideLength.y, t);
            delta.y = 0f;
            _stepDistance += delta.magnitude / stride;
            if (_stepDistance < 1f) return;
            _stepDistance -= 1f;

            float loudness = Mathf.Lerp(0.55f, 1f, t);
            float volume = Mathf.Lerp(footstepVolume, crouchFootstepVolume, state.Crouch) * loudness;
            Play(ProceduralSounds.Footstep(_stepIndex++ * 5 + Random.Range(0, 3)), volume, Random.Range(0.92f, 1.08f));
        }

        private void OnFired(WeaponState state)
            => Play(ProceduralSounds.Gunshot((int)state.ShotCount), gunshotVolume, Random.Range(0.96f, 1.04f));

        private void OnDryFired() => Play(ProceduralSounds.DryClick, handlingVolume, 1f);
        private void OnReloadStarted() => Play(ProceduralSounds.MagazineOut, handlingVolume, Random.Range(0.97f, 1.03f));
        private void OnMagazineInserted() => Play(ProceduralSounds.MagazineIn, handlingVolume, Random.Range(0.97f, 1.03f));

        private void OnReloadFinished(bool empty)
        {
            if (empty) Play(ProceduralSounds.SlideRelease, handlingVolume, 1f);
        }

        private void OnJumped() => Play(ProceduralSounds.Footstep(_stepIndex++), footstepVolume, 0.85f);

        private void OnLanded(float impact)
        {
            float volume = Mathf.Clamp01(impact / 8f);
            if (volume > 0.1f) Play(ProceduralSounds.Land, volume, Random.Range(0.95f, 1.05f));
            _stepDistance = 0f;
        }

        private void Play(AudioClip clip, float volume, float pitch)
        {
            AudioSource source = _sources[_nextVoice];
            _nextVoice = (_nextVoice + 1) % Voices;
            source.pitch = pitch;
            source.PlayOneShot(clip, volume);
        }
    }
}
