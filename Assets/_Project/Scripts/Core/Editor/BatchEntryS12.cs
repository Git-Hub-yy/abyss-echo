using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>Batch entry for S12: chapter 2 content.</summary>
    public static class BatchEntryS12
    {
        // ⚠️ S18 修复：这两个 BuildEntry 属于第二章内容，之前误指向第一章场景，
//    导致每次运行都往第一章里塞第二章的物件。
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter2_Graybox.unity";

        public static void RunAll()
        {
            Debug.Log("[ABYSS ECHO] S12 START");

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError("[S12] Cannot open scene");
                EditorApplication.Exit(1);
                return;
            }

            ChapterTwoContentBuilder.Build();
            AssetDatabase.SaveAssets();

            scene = EditorSceneManager.GetActiveScene();
            if (scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[ABYSS ECHO] Final scene save: " + scene.path);
            }

            AssetDatabase.Refresh();
            Debug.Log("[ABYSS ECHO] S12 DONE");
        }

        /// <summary>只校验第二章交互物配置，不写入场景（供测试用）。</summary>
        public static void ValidateOnly()
        {
            Debug.Log("[ABYSS ECHO] S12 校验模式");
            ChapterTwoContentBuilder.Build();
        }
    }
}