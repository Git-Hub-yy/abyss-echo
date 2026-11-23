using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>Build the S5 auto-test player.</summary>
    public static class BuildToolsS5Test
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity";

        public static void Build()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // Disable every other auto-runner
            foreach (var t in UnityEngine.Object.FindObjectsByType<AbyssEcho.DebugTools.ScreenshotTool>(FindObjectsSortMode.None)) t.runOnStart = false;
            foreach (var t in UnityEngine.Object.FindObjectsByType<AbyssEcho.DebugTools.ChapterWalkTest>(FindObjectsSortMode.None)) t.runOnStart = false;
            foreach (var t in UnityEngine.Object.FindObjectsByType<AbyssEcho.DebugTools.LightingVerifier>(FindObjectsSortMode.None)) t.runOnStart = false;
            foreach (var t in UnityEngine.Object.FindObjectsByType<AbyssEcho.DebugTools.BlackScreenDiagnoser>(FindObjectsSortMode.None)) t.runOnStart = false;
            foreach (var t in UnityEngine.Object.FindObjectsByType<AbyssEcho.DebugTools.LevelScreenshotTool>(FindObjectsSortMode.None)) t.runOnStart = false;
            foreach (var t in UnityEngine.Object.FindObjectsByType<AbyssEcho.DebugTools.S4VisualCheck>(FindObjectsSortMode.None)) t.runOnStart = false;
            foreach (var t in UnityEngine.Object.FindObjectsByType<AbyssEcho.DebugTools.AutoTest_S4>(FindObjectsSortMode.None)) t.runOnStart = false;
            foreach (var t in UnityEngine.Object.FindObjectsByType<AbyssEcho.DebugTools.AutoTest_S4B>(FindObjectsSortMode.None)) t.runOnStart = false;

            var test = UnityEngine.Object.FindFirstObjectByType<AbyssEcho.DebugTools.AutoTest_S5>();
            if (test == null)
            {
                var playerGo = GameObject.Find("Player");
                if (playerGo != null) test = playerGo.AddComponent<AbyssEcho.DebugTools.AutoTest_S5>();
            }
            if (test == null) { Debug.LogError("[S5Test] 找不到测试组件"); EditorApplication.Exit(1); return; }
            test.runOnStart = true;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            string root = Path.GetDirectoryName(Path.GetFullPath(Application.dataPath));
            string outDir = Path.GetFullPath(Path.Combine(root, "../Builds/v0.5_S5_Test"));
            Directory.CreateDirectory(outDir);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = Path.Combine(outDir, "S5Test.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };

            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log("[S5Test] build: " + report.summary.result);
            if (report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}