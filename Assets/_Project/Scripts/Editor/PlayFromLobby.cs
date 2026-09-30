using UnityEditor;
using UnityEditor.SceneManagement;

namespace Polykov.EditorTools
{
    /// <summary>Menu toggle: pressing Play always starts in the Lobby scene, whatever scene is open.</summary>
    [InitializeOnLoad]
    public static class PlayFromLobby
    {
        private const string MenuPath = "Polykov/Play from Lobby";
        private const string PrefKey = "polykov.playFromLobby";
        private const string LobbyPath = "Assets/_Project/Scenes/Lobby.unity";

        static PlayFromLobby() => EditorApplication.delayCall += Apply;

        private static bool Enabled
        {
            get => EditorPrefs.GetBool(PrefKey, false);
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
