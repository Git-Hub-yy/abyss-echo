using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>Batch entry for S5: horror system setup.</summary>
    public static class BatchEntryS5
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity";

        public static void RunAll()
        {
            Debug.Log("[ABYSS ECHO] S5 START");

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError("[S5] Cannot open scene");
                EditorApplication.Exit(1);
                return;
            }
            Debug.Log("[S5] Opened scene: " + scene.path);

            HorrorSetupBuilder.Setup();
            AssetDatabase.SaveAssets();

            scene = EditorSceneManager.GetActiveScene();
            if (scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[ABYSS ECHO] Final scene save: " + scene.path);
            }

            AssetDatabase.Refresh();
            Debug.Log("[ABYSS ECHO] S5 DONE");
        }
    }
}