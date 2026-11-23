using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>Build the S7 UI visual check player.</summary>
    public static class BuildToolsS7Visual
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
            DisableAll<AbyssEcho.DebugTools.AutoTest_S4>();
            DisableAll<AbyssEcho.DebugTools.AutoTest_S4B>();
            DisableAll<AbyssEcho.DebugTools.AutoTest_S5>();
            DisableAll<AbyssEcho.DebugTools.AutoTest_S5Monster>();
            DisableAll<AbyssEcho.DebugTools.AutoTest_Death>();
            DisableAll<AbyssEcho.DebugTools.AutoTest_S7>();

            var check = UnityEngine.Object.FindFirstObjectByType<AbyssEcho.DebugTools.S7VisualCheck>();
            if (check == null)
            {
                var playerGo = GameObject.Find("Player");
                if (playerGo != null) check = playerGo.AddComponent<AbyssEcho.DebugTools.S7VisualCheck>();
            }
            if (check == null) { Debug.LogError("[S7Visual] 找不到组件"); EditorApplication.Exit(1); return; }
            check.runOnStart = true;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            string root = Path.GetDirectoryName(Path.GetFullPath(Application.dataPath));
            string outDir = Path.GetFullPath(Path.Combine(root, "../Builds/v0.7_S7_Visual"));
            Directory.CreateDirectory(outDir);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = Path.Combine(outDir, "S7Visual.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };

            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log("[S7Visual] build: " + report.summary.result);
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