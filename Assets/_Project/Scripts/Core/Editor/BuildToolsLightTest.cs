using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// 构建"灯光验证包"：运行后自动做像素对比，输出可判定的结论。
    /// </summary>
    public static class BuildToolsLightTest
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity";

        public static void Build()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // 关闭其他自动行为
            foreach (var s in UnityEngine.Object.FindObjectsByType<AbyssEcho.DebugTools.ScreenshotTool>(FindObjectsSortMode.None))
                s.runOnStart = false;
            foreach (var w in UnityEngine.Object.FindObjectsByType<AbyssEcho.DebugTools.ChapterWalkTest>(FindObjectsSortMode.None))
                w.runOnStart = false;

            var v = UnityEngine.Object.FindFirstObjectByType<AbyssEcho.DebugTools.LightingVerifier>();
            if (v == null)
            {
                var playerGo = GameObject.Find("Player");
                if (playerGo != null) v = playerGo.AddComponent<AbyssEcho.DebugTools.LightingVerifier>();
            }
            if (v == null) { Debug.LogError("[LightTest] 找不到 LightingVerifier"); EditorApplication.Exit(1); return; }
            v.runOnStart = true;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            string root = Path.GetDirectoryName(Path.GetFullPath(Application.dataPath));
            string outDir = Path.GetFullPath(Path.Combine(root, "../Builds/v0.3B_LightTest"));
            Directory.CreateDirectory(outDir);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = Path.Combine(outDir, "LightTest.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };

            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log("[LightTest] 构建结果：" + report.summary.result + " | 错误 " + report.summary.totalErrors);
            if (report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}