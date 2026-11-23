using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>Batch entry for S6: prop placement.</summary>
    public static class BatchEntryS6
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity";

        public static void RunAll()
        {
            Debug.Log("[ABYSS ECHO] S6 START");

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError("[S6] Cannot open scene");
                EditorApplication.Exit(1);
                return;
            }

            PropPlacementBuilder.Build();
            AssetDatabase.SaveAssets();

            scene = EditorSceneManager.GetActiveScene();
            if (scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[ABYSS ECHO] Final scene save: " + scene.path);
            }

            AssetDatabase.Refresh();
            Debug.Log("[ABYSS ECHO] S6 DONE");
        }
    }
}