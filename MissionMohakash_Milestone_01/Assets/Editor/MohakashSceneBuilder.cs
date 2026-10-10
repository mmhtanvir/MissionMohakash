using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Mohakash.EditorTools
{
    /// <summary>Menu: Mission Mohakash > Create Milestone 1 Scene. Editor-only.</summary>
    public static class MohakashSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/M1_Sandbox.unity";

        [MenuItem("Mission Mohakash/Create Milestone 1 Scene")]
        public static void CreateMilestone1Scene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("MissionBootstrap");
            go.AddComponent<MissionBootstrap>();
            EditorSceneManager.SaveScene(scene, ScenePath);

            var list = EditorBuildSettings.scenes.ToList();
            if (!list.Any(s => s.path == ScenePath)) list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();

            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Mission Mohakash",
                "Created " + ScenePath + " and added it to Build Settings.\n\nPress Play, then click inside the Game view.", "OK");
        }
    }
}
