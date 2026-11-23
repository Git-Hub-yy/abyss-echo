using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>Build a diagnostic player for the ending trigger bug.</summary>
    public static class BuildToolsEndingDiag
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

            var diag = UnityEngine.Object.FindFirstObjectByType<AbyssEcho.DebugTools.EndingTriggerDiagnostic>();
            if (diag == null)
            {
                var go = new GameObject("--- DIAG ---");
                diag = go.AddComponent<AbyssEcho.DebugTools.EndingTriggerDiagnostic>();
            }
            diag.runOnStart = true;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            string root = Path.GetDirectoryName(Path.GetFullPath(Application.dataPath));
            string outDir = Path.GetFullPath(Path.Combine(root, "../Builds/Diag_Ending"));
            Directory.CreateDirectory(outDir);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = Path.Combine(outDir, "EndingDiag.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };

            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log("[EndingDiag] build: " + report.summary.result);
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