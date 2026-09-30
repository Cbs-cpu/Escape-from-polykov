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
        public const string ScavModelPath = "Assets/_Project/Art/Characters/Scav/Scav.fbx";
        private const string ScavAlbedoPath = "Assets/_Project/Art/Characters/Scav/T_Scav_Albedo.png";
        private const string ScavNormalPath = "Assets/_Project/Art/Characters/Scav/T_Scav_Normal.png";
        private const string PalettePath = "Assets/_Project/Art/Characters/Operator/T_Operator_Palette.png";

        private static readonly HashSet<string> Looping = new HashSet<string>
        {
            "Idle", "Walk_F", "Run_F", "Sprint_F", "Walk_B", "Run_B",
            "Walk_L", "Walk_R", "Run_L", "Run_R", "Jump_Air",
            "Crouch_Idle", "Crouch_F", "Crouch_B", "Crouch_L", "Crouch_R",
        };

        // Bump when import settings change so Unity reimports the character models (v2: readable Scav mesh).
        public override uint GetVersion() => 2;

        private void OnPreprocessModel()
        {
            if (!IsCharacterModel(assetPath)) return;
            var importer = (ModelImporter)assetImporter;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            // The Scav can lose limbs: severing rebuilds its meshes at runtime, which needs CPU access.
            importer.isReadable = assetPath == ScavModelPath;
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
            if (!IsCharacterModel(assetPath)) return;
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

        private static bool IsCharacterModel(string path) => path == ModelPath || path == ScavModelPath;

        private void OnPreprocessTexture()
        {
            if (assetPath == ScavAlbedoPath || assetPath == ScavNormalPath)
            {
                var scav = (TextureImporter)assetImporter;
                bool normal = assetPath == ScavNormalPath;
                scav.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                scav.filterMode = UnityEngine.FilterMode.Point;
                scav.mipmapEnabled = false;
                scav.maxTextureSize = 512;
                scav.wrapMode = UnityEngine.TextureWrapMode.Clamp;
                scav.sRGBTexture = !normal;
                return;
            }
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
