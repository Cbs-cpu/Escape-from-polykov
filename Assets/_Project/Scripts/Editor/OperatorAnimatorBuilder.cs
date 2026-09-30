using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Polykov.EditorTools
{
    /// <summary>Builds the Operator animator controller from the imported clips. Menu: Polykov/Build Operator Animator.</summary>
    public static class OperatorAnimatorBuilder
    {
        public const string ControllerPath = "Assets/_Project/Animations/Player/AC_Operator.controller";

        // Blend positions are local velocities (m/s) that match MovementTuning defaults.
        private static readonly (string clip, Vector2 velocity)[] Locomotion =
        {
            ("Idle", new Vector2(0f, 0f)),
            ("Walk_F", new Vector2(0f, 1.8f)),
            ("Run_F", new Vector2(0f, 3.6f)),
            ("Sprint_F", new Vector2(0f, 5.8f)),
            ("Walk_B", new Vector2(0f, -1.26f)),
            ("Run_B", new Vector2(0f, -2.52f)),
            ("Walk_L", new Vector2(-1.53f, 0f)),
            ("Walk_R", new Vector2(1.53f, 0f)),
            ("Run_L", new Vector2(-3.06f, 0f)),
            ("Run_R", new Vector2(3.06f, 0f)),
        };

        [MenuItem("Polykov/Build Operator Animator")]
        public static void Build()
        {
            Dictionary<string, AnimationClip> clips = AssetDatabase.LoadAllAssetsAtPath(OperatorModelPostprocessor.ModelPath)
                .OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__"))
                .ToDictionary(c => c.name);

            AssetDatabase.DeleteAsset(ControllerPath);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("VelX", AnimatorControllerParameterType.Float);
            controller.AddParameter("VelZ", AnimatorControllerParameterType.Float);
            controller.AddParameter("VerticalSpeed", AnimatorControllerParameterType.Float);
            controller.AddParameter(new AnimatorControllerParameter
                { name = "Grounded", type = AnimatorControllerParameterType.Bool, defaultBool = true });

            AnimatorControllerLayer[] layers = controller.layers;
            layers[0].iKPass = true;
            controller.layers = layers;
            AnimatorStateMachine sm = controller.layers[0].stateMachine;

            AnimatorState locomotion = controller.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
            tree.blendType = BlendTreeType.FreeformDirectional2D;
            tree.blendParameter = "VelX";
            tree.blendParameterY = "VelZ";
            foreach ((string clip, Vector2 velocity) in Locomotion)
                tree.AddChild(Require(clips, clip), velocity);
            sm.defaultState = locomotion;

            AnimatorState air = sm.AddState("Air", new Vector3(300f, 120f));
            air.motion = Require(clips, "Jump_Air");
            AnimatorState land = sm.AddState("Land", new Vector3(300f, 220f));
            land.motion = Require(clips, "Jump_Land");
            land.speed = 1.3f;

            AnimatorStateTransition toAir = locomotion.AddTransition(air);
            toAir.hasExitTime = false;
            toAir.duration = 0.12f;
            toAir.AddCondition(AnimatorConditionMode.IfNot, 0f, "Grounded");

            AnimatorStateTransition toLand = air.AddTransition(land);
            toLand.hasExitTime = false;
            toLand.duration = 0.06f;
            toLand.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");

            AnimatorStateTransition landToAir = land.AddTransition(air);
            landToAir.hasExitTime = false;
            landToAir.duration = 0.1f;
            landToAir.AddCondition(AnimatorConditionMode.IfNot, 0f, "Grounded");

            AnimatorStateTransition landToLocomotion = land.AddTransition(locomotion);
            landToLocomotion.hasExitTime = true;
            landToLocomotion.exitTime = 0.45f;
            landToLocomotion.duration = 0.2f;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Polykov] Built {ControllerPath} with {clips.Count} clips.");
        }

        private static AnimationClip Require(Dictionary<string, AnimationClip> clips, string name)
        {
            if (!clips.TryGetValue(name, out AnimationClip clip))
                throw new System.InvalidOperationException($"Clip '{name}' not found in {OperatorModelPostprocessor.ModelPath}");
            return clip;
        }
    }
}
