using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S32：第三章的交互系统（玩家身上的检测器 + 玩家 UI）。
    ///
    /// 直接复用第一章/第二章验证过的 InteractionSetupBuilder，
    /// 只把场景换成第三章。
    /// </summary>
    public static class ChapterThreeInteractionSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter3_Graybox.unity";

        [MenuItem("ABYSS ECHO/S32 第三章交互系统")]
        public static void RunAll()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError("[Ch3Interact] 无法打开第三章场景");
                EditorApplication.Exit(1);
                return;
            }

            InteractionSetupBuilder.Setup();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            var det = Object.FindFirstObjectByType<AbyssEcho.Interaction.InteractionDetector>();
            Debug.Log($"<b>[Ch3Interact]</b> 第三章交互系统装配完成，检测器={(det != null ? "有" : "无")}");
        }
    }
}