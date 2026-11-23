using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S18B 给第二章补 UI。
    ///
    /// 问题：第二章场景生成后，只跑了 ChapterTwoContentBuilder / ChapterTwoHorrorBuilder，
    /// 但**从未跑 UISetupBuilder**——
    /// 所以第二章没有物品栏（Tab）、日志（J）、暂停菜单（Esc）、操作说明（H）。
    ///
    /// 玩家在第二章按 Tab 没反应，会以为是 bug 或看到残留在屏幕上的旧面板。
    /// </summary>
    public static class ChapterTwoUISetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter2_Graybox.unity";

        [MenuItem("ABYSS ECHO/S18B 给第二章装配 UI")]
        public static void RunAll()
        {
            var prevChapter = AbyssEcho.LevelGen.ChapterLayout.Current;

            AbyssEcho.LevelGen.ChapterLayout.Current = AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter2;

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError("[Ch2UI] 无法打开第二章场景");
                AbyssEcho.LevelGen.ChapterLayout.Current = prevChapter;
                EditorApplication.Exit(1);
                return;
            }

            // 复用第一章的 UI 装配器（它会给 --- DEBUG --- 挂上全部 UI）
            UISetupBuilder.Setup();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            AbyssEcho.LevelGen.ChapterLayout.Current = prevChapter;

            Debug.Log("<b>[Ch2UI]</b> 第二章 UI 装配完成");
            AssetDatabase.Refresh();
        }
    }
}