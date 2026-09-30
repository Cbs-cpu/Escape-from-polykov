using Polykov.Player;
using UnityEngine;

namespace Polykov.Weapons
{
    /// <summary>
    /// Turns each shot's recoil (from the deterministic simulation) into view rotation: a fast kick, then a
    /// partial return to the point of aim. Runs right after PlayerLook so the camera sees it the same frame.
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public sealed class CameraRecoil : MonoBehaviour
    {
        [SerializeField] private PlayerWeapon weapon;
        [SerializeField] private PlayerLook look;

        private Vector2 _pending;
        private Vector2 _toReturn;

        private void OnEnable() => weapon.Fired += OnFired;
        private void OnDisable() => weapon.Fired -= OnFired;

        private void OnFired(WeaponState state)
        {
            _pending += state.RecoilKick;
            _toReturn += state.RecoilKick * weapon.Definition.CameraRecoilReturn;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            WeaponDefinition def = weapon.Definition;

            if (_pending.sqrMagnitude > 1e-8f)
            {
                Vector2 step = _pending * (1f - Mathf.Exp(-dt * 3f / def.CameraKickTime));
                _pending -= step;
                look.AddRotation(step.x, -step.y);
            }

            // Recover only once the kick is mostly applied, like a shooter bringing the sights back down.
            if (_pending.sqrMagnitude < 0.01f && _toReturn.sqrMagnitude > 1e-8f)
            {
                Vector2 back = _toReturn * (1f - Mathf.Exp(-dt * def.CameraReturnSpeed));
                _toReturn -= back;
                look.AddRotation(-back.x, back.y);
            }
        }
    }
}
