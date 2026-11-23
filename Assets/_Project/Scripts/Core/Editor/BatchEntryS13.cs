using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>Batch entry for S13: chapter 2 horror systems.</summary>
    public static class BatchEntryS13
    {
        // ⚠️ S18 修复：这两个 BuildEntry 属于第二章内容，之前误指向第一章场景，
//    导致每次运行都往第一章里塞第二章的物件。
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter2_Graybox.unity";

        public static void RunAll()
        {
            Debug.Log("[ABYSS ECHO] S13 START");

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError("[S13] Cannot open scene");
                EditorApplication.Exit(1);
                return;
            }

            // ① 先补第二章灯光（复用 RoomLightBuilder，它已改为读 ChapterLayout）
            //    注意：RoomLightBuilder 使用当前章节，所以需要临时切到第二章
            var prevChapter = AbyssEcho.LevelGen.ChapterLayout.Current;
            AbyssEcho.LevelGen.ChapterLayout.Current = AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter2;
            Debug.Log("[S13] 已切换到第二章：" + AbyssEcho.LevelGen.ChapterLayout.Current);

            ChapterTwoHorrorBuilder.Build();
            AssetDatabase.SaveAssets();

            // 恢复章节设置
            AbyssEcho.LevelGen.ChapterLayout.Current = prevChapter;

            scene = EditorSceneManager.GetActiveScene();
            if (scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[ABYSS ECHO] Final scene save: " + scene.path);
            }

            AssetDatabase.Refresh();
            Debug.Log("[ABYSS ECHO] S13 DONE");
        }
    }
}