using UnityEditor;
using UnityEditor.SceneManagement;

namespace Polykov.EditorTools
{
    /// <summary>
    /// Pressing Play always starts in the Lobby scene, whatever scene is open (on by default, like the built game).
    /// Menu toggle to turn it off and play the open scene directly.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayFromLobby
    {
        private const string MenuPath = "Polykov/Play from Lobby";
        private const string PrefKey = "polykov.playFromLobby";
        private const string LobbyPath = "Assets/_Project/Scenes/Lobby.unity";

        static PlayFromLobby()
        {
            EditorApplication.delayCall += Apply;
            // Re-applied right before entering Play in case the scene asset was (re)imported after the domain reload.
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode) Apply();
            };
        }

        private static bool Enabled
        {
            get => EditorPrefs.GetBool(PrefKey, true);
            set => EditorPrefs.SetBool(PrefKey, value);
        }

        [MenuItem(MenuPath)]
        private static void Toggle()
        {
            Enabled = !Enabled;
            Apply();
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }

        private static void Apply()
        {
            EditorSceneManager.playModeStartScene = Enabled ? AssetDatabase.LoadAssetAtPath<SceneAsset>(LobbyPath) : null;
        }
    }
}
