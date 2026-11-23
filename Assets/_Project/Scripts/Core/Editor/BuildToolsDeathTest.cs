using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>Build the death-flow auto-test player.</summary>
    public static class BuildToolsDeathTest
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity";

        public static void Build()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // 关闭所有其它自动运行器
            foreach (var t in UnityEngine.Object.FindObjectsByType<AbyssEcho.DebugTools.ScreenshotTool>(FindObjectsSortMode.None)) t.runOnStart = false;
            foreach (var t in UnityEngine.Object.FindObjectsByType<AbyssEcho.DebugTools.ChapterWalkTest>(FindObjectsSortMode.None)) t.runOnStart = false;
            foreach (var t in UnityEngine.Object.FindObjectsByType<AbyssEcho.DebugTools.LightingVerifier>(FindObjectsSortMode.None)) t.runOnStart = false;
            foreach (var t in UnityEngine.Object.FindObjectsByType<AbyssEcho.DebugTools.BlackScreenDiagnoser>(FindObjectsSortMode.None)) t.runOnStart = false;
            foreach (var t in UnityEngine.Object.FindObjectsByType<AbyssEcho.DebugTools.LevelScreenshotTool>(FindObjectsSortMode.None)) t.runOnStart = false;
            foreach (var t in UnityEngine.Object.FindObjectsByType<AbyssEcho.DebugTools.S4VisualCheck>(FindObjectsSortMode.None)) t.runOnStart = false;
            foreach (var t in UnityEngine.Object.FindObjectsByType<AbyssEcho.DebugTools.S5VisualCheck>(FindObjectsSortMode.None)) t.runOnStart = false;
            foreach (var t in UnityEngine.Object.FindObjectsByType<AbyssEcho.DebugTools.MonsterVisualCheck>(FindObjectsSortMode.None)) t.runOnStart = false;
            foreach (var t in UnityEngine.Object.FindObjectsByType<AbyssEcho.DebugTools.AutoTest_S4>(FindObjectsSortMode.None)) t.runOnStart = false;
            foreach (var t in UnityEngine.Object.FindObjectsByType<AbyssEcho.DebugTools.AutoTest_S4B>(FindObjectsSortMode.None)) t.runOnStart = false;
            foreach (var t in UnityEngine.Object.FindObjectsByType<AbyssEcho.DebugTools.AutoTest_S5>(FindObjectsSortMode.None)) t.runOnStart = false;
            foreach (var t in UnityEngine.Object.FindObjectsByType<AbyssEcho.DebugTools.AutoTest_S5Monster>(FindObjectsSortMode.None)) t.runOnStart = false;

            var test = UnityEngine.Object.FindFirstObjectByType<AbyssEcho.DebugTools.AutoTest_Death>();
            if (test == null)
            {
                var playerGo = GameObject.Find("Player");
                if (playerGo != null) test = playerGo.AddComponent<AbyssEcho.DebugTools.AutoTest_Death>();
            }
            if (test == null) { Debug.LogError("[DeathTest] 找不到测试组件"); EditorApplication.Exit(1); return; }
            test.runOnStart = true;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            string root = Path.GetDirectoryName(Path.GetFullPath(Application.dataPath));
            string outDir = Path.GetFullPath(Path.Combine(root, "../Builds/v0.5D_Death_Test"));
            Directory.CreateDirectory(outDir);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = Path.Combine(outDir, "DeathTest.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };

            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log("[DeathTest] build: " + report.summary.result);
            if (report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}