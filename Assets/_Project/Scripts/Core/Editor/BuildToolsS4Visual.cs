using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>Build the S4 visual check player.</summary>
    public static class BuildToolsS4Visual
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity";

        public static void Build()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            foreach (var t in UnityEngine.Object.FindObjectsByType<AbyssEcho.DebugTools.ScreenshotTool>(FindObjectsSortMode.None)) t.runOnStart = false;
            foreach (var t in UnityEngine.Object.FindObjectsByType<AbyssEcho.DebugTools.ChapterWalkTest>(FindObjectsSortMode.None)) t.runOnStart = false;
            foreach (var t in UnityEngine.Object.FindObjectsByType<AbyssEcho.DebugTools.LightingVerifier>(FindObjectsSortMode.None)) t.runOnStart = false;
            foreach (var t in UnityEngine.Object.FindObjectsByType<AbyssEcho.DebugTools.BlackScreenDiagnoser>(FindObjectsSortMode.None)) t.runOnStart = false;
            foreach (var t in UnityEngine.Object.FindObjectsByType<AbyssEcho.DebugTools.LevelScreenshotTool>(FindObjectsSortMode.None)) t.runOnStart = false;
            foreach (var t in UnityEngine.Object.FindObjectsByType<AbyssEcho.DebugTools.AutoTest_S4>(FindObjectsSortMode.None)) t.runOnStart = false;
            foreach (var t in UnityEngine.Object.FindObjectsByType<AbyssEcho.DebugTools.AutoTest_S4B>(FindObjectsSortMode.None)) t.runOnStart = false;

            var check = UnityEngine.Object.FindFirstObjectByType<AbyssEcho.DebugTools.S4VisualCheck>();
            if (check == null)
            {
                var playerGo = GameObject.Find("Player");
                if (playerGo != null) check = playerGo.AddComponent<AbyssEcho.DebugTools.S4VisualCheck>();
            }
            if (check == null) { Debug.LogError("[S4Visual] 找不到组件"); EditorApplication.Exit(1); return; }
            check.runOnStart = true;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            string root = Path.GetDirectoryName(Path.GetFullPath(Application.dataPath));
            string outDir = Path.GetFullPath(Path.Combine(root, "../Builds/v0.4_S4_Visual"));
            Directory.CreateDirectory(outDir);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = Path.Combine(outDir, "S4Visual.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };

            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log("[S4Visual] build: " + report.summary.result);
            if (report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}