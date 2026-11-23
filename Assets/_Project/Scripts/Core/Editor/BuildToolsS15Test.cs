using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>Build the S15 auto-test player.</summary>
    public static class BuildToolsS15Test
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
            DisableAll<AbyssEcho.DebugTools.S7VisualCheck>();
            DisableAll<AbyssEcho.DebugTools.S8VisualCheck>();
            DisableAll<AbyssEcho.DebugTools.MonsterVisualCheck>();
            DisableAll<AbyssEcho.DebugTools.PropVisualCheck>();
            DisableAll<AbyssEcho.DebugTools.AutoTest_S4>();
            DisableAll<AbyssEcho.DebugTools.AutoTest_S4B>();
            DisableAll<AbyssEcho.DebugTools.AutoTest_S5>();
            DisableAll<AbyssEcho.DebugTools.AutoTest_S5Monster>();
            DisableAll<AbyssEcho.DebugTools.AutoTest_S7>();
            DisableAll<AbyssEcho.DebugTools.AutoTest_S8>();
            DisableAll<AbyssEcho.DebugTools.AutoTest_S10>();
            DisableAll<AbyssEcho.DebugTools.AutoTest_S12>();
            DisableAll<AbyssEcho.DebugTools.AutoTest_S13>();
            DisableAll<AbyssEcho.DebugTools.AutoTest_S14>();
            DisableAll<AbyssEcho.DebugTools.AutoTest_Death>();
            DisableAll<AbyssEcho.DebugTools.PlayerAudit>();
            DisableAll<AbyssEcho.DebugTools.RuntimeTracer>();

            var card = UnityEngine.Object.FindFirstObjectByType<AbyssEcho.UI.ChapterTitleCard>();
            if (card != null) card.enabled = false;

            // 关闭操作说明面板的自动弹出，避免干扰测试
            var help = UnityEngine.Object.FindFirstObjectByType<AbyssEcho.UI.ControlsHelpUI>();
            if (help != null) help.showOnFirstEnter = false;

            var test = UnityEngine.Object.FindFirstObjectByType<AbyssEcho.DebugTools.AutoTest_S15>();
            if (test == null)
            {
                var playerGo = GameObject.Find("Player");
                if (playerGo != null) test = playerGo.AddComponent<AbyssEcho.DebugTools.AutoTest_S15>();
            }
            if (test == null) { Debug.LogError("[S15Test] 找不到测试组件"); EditorApplication.Exit(1); return; }
            test.runOnStart = true;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            string root = Path.GetDirectoryName(Path.GetFullPath(Application.dataPath));
            string outDir = Path.GetFullPath(Path.Combine(root, "../Builds/v0.15_S15_Test"));
            Directory.CreateDirectory(outDir);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = Path.Combine(outDir, "S15Test.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };

            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log("[S15Test] build: " + report.summary.result);
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