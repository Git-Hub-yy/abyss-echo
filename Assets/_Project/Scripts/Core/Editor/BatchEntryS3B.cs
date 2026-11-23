using UnityEditor;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// Batch entry: S3B material library -> scene rebuild -> lighting -> room lights.
    /// Usage: -executeMethod AbyssEcho.Core.EditorTools.BatchEntryS3B.RunAll
    ///
    /// NOTE: We deliberately use ASCII-only comments here.
    ///   Previously, Chinese comments combined with lost line endings caused
    ///   code to be swallowed into comment lines (LightingBuilder.Build() never ran).
    /// </summary>
    public static class BatchEntryS3B
    {
        public static void RunAll()
        {
            Debug.Log("[ABYSS ECHO] S3B START");

            // Step 1: material library (texture import settings + URP materials)
            MaterialLibraryBuilder.Build();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Step 2: rebuild scene with new materials
            ChapterOneSceneBuilder.Build();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Step 3: volume, ambient, URP pipeline fixes, depth fog
            LightingBuilder.Build();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Step 4: room emergency lights
            RoomLightBuilder.Build();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Step 5: final scene save (ensures all components are persisted)
            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (scene.IsValid() && scene.isLoaded)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
                Debug.Log("[ABYSS ECHO] Final scene save: " + scene.path);
            }

            Debug.Log("[ABYSS ECHO] S3B DONE");
        }

        /// <summary>Rebuild scene only (materials already ready).</summary>
        public static void RebuildSceneOnly()
        {
            ChapterOneSceneBuilder.Build();
            LightingBuilder.Build();
            RoomLightBuilder.Build();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ABYSS ECHO] S3B SCENE REBUILT");
        }
    }
}