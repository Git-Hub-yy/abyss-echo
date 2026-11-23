using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// Batch entry for S4: interaction system setup + interactable placement.
    ///
    /// NOTE: Must explicitly open the level scene first.
    ///   In -batchmode there is no active scene, so GetActiveScene() returns an
    ///   invalid scene and every SaveScene silently does nothing.
    ///   (This was the cause of "S4 objects missing after build".)
    /// </summary>
    public static class BatchEntryS4
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity";

        public static void RunAll()
        {
            Debug.Log("[ABYSS ECHO] S4 START");

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError("[S4] Cannot open scene: " + ScenePath);
                EditorApplication.Exit(1);
                return;
            }
            Debug.Log("[S4] Opened scene: " + scene.path);

            InteractionSetupBuilder.Setup();
            AssetDatabase.SaveAssets();

            InteractablePlacementBuilder.Build();
            AssetDatabase.SaveAssets();

            // Final save (scene is guaranteed valid here)
            scene = EditorSceneManager.GetActiveScene();
            if (scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[ABYSS ECHO] Final scene save: " + scene.path);
            }
            else
            {
                Debug.LogError("[S4] Active scene invalid after setup; nothing saved");
            }

            AssetDatabase.Refresh();
            Debug.Log("[ABYSS ECHO] S4 DONE");
        }

        /// <summary>Only place interactables (scene already open in editor).</summary>
        public static void PlaceOnly()
        {
            InteractablePlacementBuilder.Build();
        }
    }
}