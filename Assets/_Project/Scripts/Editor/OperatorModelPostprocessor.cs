using System.Collections.Generic;
using UnityEditor;

namespace Polykov.EditorTools
{
    /// <summary>
    /// Enforces import settings for the Blender-exported Operator so re-exports never need manual setup:
    /// humanoid avatar, in-place looping clips, no embedded materials, pixel-exact palette texture.
    /// </summary>
    public sealed class OperatorModelPostprocessor : AssetPostprocessor
    {
        public const string ModelPath = "Assets/_Project/Art/Characters/Operator/Operator.fbx";
        private const string PalettePath = "Assets/_Project/Art/Characters/Operator/T_Operator_Palette.png";

        private static readonly HashSet<string> Looping = new HashSet<string>
        {
            "Idle", "Walk_F", "Run_F", "Sprint_F", "Walk_B", "Run_B",
            "Walk_L", "Walk_R", "Run_L", "Run_R", "Jump_Air",
        };

        private void OnPreprocessModel()
        {
            if (assetPath != ModelPath) return;
            var importer = (ModelImporter)assetImporter;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.isReadable = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            // Bones stay as transforms: the camera anchors to the head and the body is posed procedurally.
            importer.optimizeGameObjects = false;
            importer.importAnimation = true;
        }

        private void OnPreprocessAnimation()
        {
            if (assetPath != ModelPath) return;
            var importer = (ModelImporter)assetImporter;
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                int bar = clip.name.LastIndexOf('|');
                string name = bar >= 0 ? clip.name.Substring(bar + 1) : clip.name;
                clip.name = name;
                clip.loopTime = Looping.Contains(name);
                clip.loopPose = false;
                // In-place clips: the motor owns movement. Bake root motion into the pose.
                clip.lockRootRotation = true;
                clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = true;
                clip.keepOriginalOrientation = true;
                clip.keepOriginalPositionY = true;
                clip.keepOriginalPositionXZ = true;
            }
            importer.clipAnimations = clips;
        }

        private void OnPreprocessTexture()
        {
            if (assetPath != PalettePath) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = UnityEngine.FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.sRGBTexture = true;
        }
    }
}
