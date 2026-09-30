using System;
using Polykov.UI.Framework;

namespace Polykov.Lobby
{
    /// <summary>
    /// Where to put the level stage camera so the character (standing at the world origin, facing the camera) fills a
    /// given screen rect. Pure math shared by the Unity host and the offline preview (Blender) so both frame the same.
    /// World coordinates are Unity's (y up, camera looking along +z).
    /// </summary>
    public static class StageFraming
    {
        /// <summary>World height framed for the character (feet a bit below 0 to show the stand, a little headroom).</summary>
        public const float FrameBottom = -0.12f, FrameTop = 1.9f;

        public struct Shot
        {
            public float PosX, PosY, PosZ;
            public float LookX, LookY, LookZ;
            public float Fov;
        }

        public static Shot Character(UiRect area, float screenW, float screenH, float fovDeg)
        {
            float frameH = FrameTop - FrameBottom;
            float metersPerPx = frameH / Math.Max(1f, area.H);
            float visibleH = metersPerPx * screenH;
            float d = visibleH * 0.5f / (float)Math.Tan(fovDeg * 0.5f * Math.PI / 180.0);
            float centerY = (FrameTop + FrameBottom) * 0.5f;
            float dx = area.Center.X - screenW * 0.5f, dy = area.Center.Y - screenH * 0.5f;
            float camX = -dx * metersPerPx, camY = centerY + dy * metersPerPx;
            return new Shot { PosX = camX, PosY = camY, PosZ = -d, LookX = camX, LookY = camY, LookZ = 0f, Fov = fovDeg };
        }
    }
}
