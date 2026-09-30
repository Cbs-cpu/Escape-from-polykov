using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Polykov.EditorTools
{
    /// <summary>
    /// After a pull that adds clips to Operator.fbx (e.g. the crouch set), rebuilds AC_Operator automatically once,
    /// so nobody has to remember the menu item. Only runs when the controller is missing the parameter the new
    /// clips need; the rebuild keeps the controller GUID.
    /// </summary>
    [InitializeOnLoad]
    internal static class OperatorAnimatorAutoUpgrade
    {
        static OperatorAnimatorAutoUpgrade()
        {
            EditorApplication.delayCall += Check;
        }

        private static void Check()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(OperatorAnimatorBuilder.ControllerPath);
            if (controller == null) return;
            bool hasCrouch = controller.parameters.Any(p => p.name == "Crouch");
            if (hasCrouch || !OperatorAnimatorBuilder.ModelHasCrouchClips()) return;
            Debug.Log("[Polykov] Operator.fbx has new clips (crouch): rebuilding AC_Operator...");
            OperatorAnimatorBuilder.Build();
        }
    }
}
