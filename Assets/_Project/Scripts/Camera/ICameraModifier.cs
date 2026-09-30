using UnityEngine;

namespace Polykov.CameraSystem
{
    /// <summary>Final camera values for this frame, before they are applied to the transform.</summary>
    public struct CameraFrame
    {
        public Vector3 Position;
        public float Pitch;
        public float Yaw;
        public float Roll;
        public float FieldOfView;
    }

    /// <summary>
    /// Lets other systems (weapon recoil, aim zoom, damage shake later) adjust the camera without the camera
    /// knowing about them. Register with <see cref="FirstPersonCameraRig.AddModifier"/>.
    /// </summary>
    public interface ICameraModifier
    {
        void ModifyCamera(ref CameraFrame frame, float deltaTime);
    }
}
