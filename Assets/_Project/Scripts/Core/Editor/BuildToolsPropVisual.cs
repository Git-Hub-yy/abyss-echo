using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>Build the prop visual check player.</summary>
    public static class BuildToolsPropVisual
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity";

        public static void Build()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // 统一关闭所有自动运行器
            DisableAll<AbyssEcho.DebugTools.ScreenshotTool>(scene);
            DisableAll<AbyssEcho.DebugTools.ChapterWalkTest>(scene);
            DisableAll<AbyssEcho.DebugTools.LightingVerifier>(scene);
            DisableAll<AbyssEcho.DebugTools.BlackScreenDiagnoser>(scene);
            DisableAll<AbyssEcho.DebugTools.LevelScreenshotTool>(scene);
            DisableAll<AbyssEcho.DebugTools.S4VisualCheck>(scene);
            DisableAll<AbyssEcho.DebugTools.S5VisualCheck>(scene);
            DisableAll<AbyssEcho.DebugTools.MonsterVisualCheck>(scene);
            DisableAll<AbyssEcho.DebugTools.AutoTest_S4>(scene);
            DisableAll<AbyssEcho.DebugTools.AutoTest_S4B>(scene);
            DisableAll<AbyssEcho.DebugTools.AutoTest_S5>(scene);
            DisableAll<AbyssEcho.DebugTools.AutoTest_S5Monster>(scene);
            DisableAll<AbyssEcho.DebugTools.AutoTest_Death>(scene);

            var check = UnityEngine.Object.FindFirstObjectByType<AbyssEcho.DebugTools.PropVisualCheck>();
            if (check == null)
            {
                var playerGo = GameObject.Find("Player");
                if (playerGo != null) check = playerGo.AddComponent<AbyssEcho.DebugTools.PropVisualCheck>();
            }
            if (check == null) { Debug.LogError("[PropVisual] 找不到组件"); EditorApplication.Exit(1); return; }
            check.runOnStart = true;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            string root = Path.GetDirectoryName(Path.GetFullPath(Application.dataPath));
            string outDir = Path.GetFullPath(Path.Combine(root, "../Builds/v0.6_Prop_Visual"));
            Directory.CreateDirectory(outDir);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = Path.Combine(outDir, "PropVisual.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };

            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log("[PropVisual] build: " + report.summary.result);
            if (report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }

        private static void DisableAll<T>(UnityEngine.SceneManagement.Scene scene) where T : Component
        {
            foreach (var t in UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None))
            {
                var field = t.GetType().GetField("runOnStart");
                if (field != null) field.SetValue(t, false);
            }
        }
    }
}