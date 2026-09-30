using Polykov.CameraSystem;
using Polykov.Player;
using UnityEngine;

namespace Polykov.Weapons
{
    /// <summary>
    /// Camera side of shooting: recoil kick (part permanent, part spring that returns), roll and FOV punch,
    /// plus the aim-down-sights zoom. Local-only presentation; the permanent part changes the real aim.
    /// </summary>
    public sealed class WeaponCameraEffects : MonoBehaviour, ICameraModifier
    {
        [SerializeField] private FirstPersonCameraRig rig;
        [SerializeField] private PlayerWeaponController weapon;
        [SerializeField] private PlayerLook look;
        [SerializeField] private float aimFovSharpness = 14f;

        // x = yaw (deg, right +), y = pitch (deg, up +), z = roll (deg)
        private SpringVector3 _kick;
        private SpringVector3 _fovPunch;
        private float _aimBlend;

        /// <summary>Current recoil offset of the view (x = yaw, y = pitch up). Shots include it.</summary>
        public Vector2 AimOffset => new Vector2(_kick.Value.x, _kick.Value.y);

        private void OnEnable()
        {
            rig.AddModifier(this);
            weapon.Fired += OnFired;
        }

        private void OnDisable()
        {
            rig.RemoveModifier(this);
            weapon.Fired -= OnFired;
        }

        private void OnFired(ShotInfo shot)
        {
            RecoilTuning r = weapon.Data.Recoil;
            float mult = Mathf.Lerp(1f, r.AimMultiplier, weapon.State.Aim);
            float pitch = (r.CameraPitch + Random.Range(-r.CameraPitchVariance, r.CameraPitchVariance)) * mult;
            float yaw = Random.Range(-r.CameraYaw, r.CameraYaw) * mult;
            float roll = Random.Range(-r.CameraRoll, r.CameraRoll) * mult;

            look.AddRecoil(pitch * r.PermanentFraction, yaw * r.PermanentFraction);
            Vector3 elastic = new Vector3(yaw * (1f - r.PermanentFraction), pitch * (1f - r.PermanentFraction), roll);
            _kick.AddImpulse(SpringVector3.ImpulseFor(elastic, r.CameraStiffness, r.CameraDamping));
            _fovPunch.AddImpulse(SpringVector3.ImpulseFor(new Vector3(r.FovPunch * mult, 0f, 0f), r.CameraStiffness * 1.5f, 0.6f));
        }

        public void ModifyCamera(ref CameraFrame frame, float dt)
        {
            RecoilTuning r = weapon.Data.Recoil;
            _kick.Step(r.CameraStiffness, r.CameraDamping, dt);
            _fovPunch.Step(r.CameraStiffness * 1.5f, 0.6f, dt);

            frame.Pitch -= _kick.Value.y;
            frame.Yaw += _kick.Value.x;
            frame.Roll += _kick.Value.z;

            float aimTarget = Smooth(weapon.State.Aim);
            _aimBlend = Mathf.Lerp(_aimBlend, aimTarget, 1f - Mathf.Exp(-aimFovSharpness * dt));
            frame.FieldOfView = Mathf.Lerp(frame.FieldOfView, weapon.View.AimFov, _aimBlend) + _fovPunch.Value.x;
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);
    }
}
