using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>Pressing Play always starts the game from the Intro scene, whatever scene is open in the editor.</summary>
[InitializeOnLoad]
static class PlayFromIntro
{
    static PlayFromIntro()
    {
        EditorApplication.delayCall += () =>
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Intro.unity");
    }
}
