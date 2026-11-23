using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// 给第二章补全交互相关 UI（准星、日志、密钥盘、日志系统）。
    ///
    /// 这些由 InteractionSetupBuilder 创建，之前只在第一章跑过。
    /// </summary>
    public static class ChapterTwoInteractionSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter2_Graybox.unity";

        [MenuItem("ABYSS ECHO/S18C 给第二章装配交互 UI")]
        public static void RunAll()
        {
            var prev = AbyssEcho.LevelGen.ChapterLayout.Current;
            AbyssEcho.LevelGen.ChapterLayout.Current = AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter2;

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError("[Ch2InteractionUI] 无法打开第二章场景");
                AbyssEcho.LevelGen.ChapterLayout.Current = prev;
                EditorApplication.Exit(1);
                return;
            }

            InteractionSetupBuilder.Setup();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            AbyssEcho.LevelGen.ChapterLayout.Current = prev;
            Debug.Log("<b>[Ch2InteractionUI]</b> 第二章交互 UI 装配完成");
        }
    }
}