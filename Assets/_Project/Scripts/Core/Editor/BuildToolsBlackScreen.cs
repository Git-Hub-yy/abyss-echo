using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>Build the black-screen diagnosis player.</summary>
    public static class BuildToolsBlackScreen
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity";

        public static void Build()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            foreach (var s in UnityEngine.Object.FindObjectsByType<AbyssEcho.DebugTools.ScreenshotTool>(FindObjectsSortMode.None))
                s.runOnStart = false;
            foreach (var w in UnityEngine.Object.FindObjectsByType<AbyssEcho.DebugTools.ChapterWalkTest>(FindObjectsSortMode.None))
                w.runOnStart = false;
            foreach (var v in UnityEngine.Object.FindObjectsByType<AbyssEcho.DebugTools.LightingVerifier>(FindObjectsSortMode.None))
                v.runOnStart = false;

            var d = UnityEngine.Object.FindFirstObjectByType<AbyssEcho.DebugTools.BlackScreenDiagnoser>();
            if (d == null)
            {
                var playerGo = GameObject.Find("Player");
                if (playerGo != null) d = playerGo.AddComponent<AbyssEcho.DebugTools.BlackScreenDiagnoser>();
            }
            if (d == null) { Debug.LogError("[BlackScreen] 找不到诊断组件"); EditorApplication.Exit(1); return; }
            d.runOnStart = true;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            string root = Path.GetDirectoryName(Path.GetFullPath(Application.dataPath));
            string outDir = Path.GetFullPath(Path.Combine(root, "../Builds/v0.3B_BlackScreen"));
            Directory.CreateDirectory(outDir);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = Path.Combine(outDir, "BlackScreen.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };

            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log("[BlackScreen] build: " + report.summary.result);
            if (report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}