using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// 复现包：直接用关卡场景启动（跳过主菜单），
    /// 并输出完整日志，用于定位"一移动就到大结局"的问题。
    /// </summary>
    public static class BuildToolsRepro
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity";

        public static void Build()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            DisableAll<AbyssEcho.DebugTools.ScreenshotTool>();
            DisableAll<AbyssEcho.DebugTools.ChapterWalkTest>();
            DisableAll<AbyssEcho.DebugTools.LightingVerifier>();
            DisableAll<AbyssEcho.DebugTools.BlackScreenDiagnoser>();
            DisableAll<AbyssEcho.DebugTools.LevelScreenshotTool>();
            DisableAll<AbyssEcho.DebugTools.S4VisualCheck>();
            DisableAll<AbyssEcho.DebugTools.S5VisualCheck>();
            DisableAll<AbyssEcho.DebugTools.MonsterVisualCheck>();
            DisableAll<AbyssEcho.DebugTools.PropVisualCheck>();
            DisableAll<AbyssEcho.DebugTools.S7VisualCheck>();
            DisableAll<AbyssEcho.DebugTools.S8VisualCheck>();
            DisableAll<AbyssEcho.DebugTools.AutoTest_S4>();
            DisableAll<AbyssEcho.DebugTools.AutoTest_S4B>();
            DisableAll<AbyssEcho.DebugTools.AutoTest_S5>();
            DisableAll<AbyssEcho.DebugTools.AutoTest_S5Monster>();
            DisableAll<AbyssEcho.DebugTools.AutoTest_Death>();
            DisableAll<AbyssEcho.DebugTools.AutoTest_S7>();
            DisableAll<AbyssEcho.DebugTools.AutoTest_S8>();
            DisableAll<AbyssEcho.DebugTools.EndingTriggerDiagnostic>();
            DisableAll<AbyssEcho.DebugTools.EndingReproTest>();
            DisableAll<AbyssEcho.DebugTools.EndingKeyRepro>();
            DisableAll<AbyssEcho.DebugTools.DeepEndingDiag>();

            // 挂载 UI 诊断
            var diag = UnityEngine.Object.FindFirstObjectByType<AbyssEcho.DebugTools.UIDiag>();
            if (diag == null)
            {
                var dgo = new GameObject("--- UIDIAG ---");
                diag = dgo.AddComponent<AbyssEcho.DebugTools.UIDiag>();
            }
            diag.runOnStart = true;

            var diag2 = UnityEngine.Object.FindFirstObjectByType<AbyssEcho.DebugTools.RealWalkTest>();
            if (diag2 == null)
            {
                var dgo2 = new GameObject("--- REALWALK ---");
                diag2 = dgo2.AddComponent<AbyssEcho.DebugTools.RealWalkTest>();
            }
            diag2.runOnStart = true;

            // 关闭标题卡（避免它冻结输入干扰）
            var card = UnityEngine.Object.FindFirstObjectByType<AbyssEcho.UI.ChapterTitleCard>();
            if (card != null) card.enabled = false;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            string root = Path.GetDirectoryName(Path.GetFullPath(Application.dataPath));
            string outDir = Path.GetFullPath(Path.Combine(root, "../Builds/Repro"));
            Directory.CreateDirectory(outDir);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = Path.Combine(outDir, "Repro.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };

            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log("[Repro] build: " + report.summary.result);
            if (report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }

        private static void DisableAll<T>() where T : Component
        {
            foreach (var t in UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None))
            {
                var field = t.GetType().GetField("runOnStart");
                if (field != null) field.SetValue(t, false);
            }
        }
    }
}