using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S32：第三章 UI 系统（准星、物品栏、暂停菜单、控制说明、章节标题卡）。
    ///
    /// 复用 UISetupBuilder —— 它在第二章已经验证过（修复了硬编码路径的问题）。
    /// </summary>
    public static class ChapterThreeUISetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter3_Graybox.unity";

        [MenuItem("ABYSS ECHO/S32 第三章 UI")]
        public static void RunAll()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError("[Ch3UI] 无法打开第三章场景");
                EditorApplication.Exit(1);
                return;
            }

            UISetupBuilder.Setup();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            var cross = Object.FindFirstObjectByType<AbyssEcho.UI.CrosshairUI>();
            var pause = Object.FindFirstObjectByType<AbyssEcho.UI.PauseMenuUI>();
            Debug.Log($"<b>[Ch3UI]</b> 第三章 UI 装配完成，准星={(cross != null ? "有" : "无")}，暂停菜单={(pause != null ? "有" : "无")}");
        }
    }
}