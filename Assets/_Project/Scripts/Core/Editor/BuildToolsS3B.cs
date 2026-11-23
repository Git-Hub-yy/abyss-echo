using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S3B 构建截图包：场景中启用 ScreenshotTool，运行后自动截图并退出。
    /// 输出：Builds/v0.3B_Shots/
    /// </summary>
    public static class BuildToolsS3B
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity";

        public static void BuildScreenshotPlayer()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var shot = UnityEngine.Object.FindFirstObjectByType<AbyssEcho.DebugTools.ScreenshotTool>();
            if (shot == null)
            {
                var playerGo = GameObject.Find("Player");
                if (playerGo != null) shot = playerGo.AddComponent<AbyssEcho.DebugTools.ScreenshotTool>();
            }
            if (shot == null)
            {
                Debug.LogError("[S3B] 找不到 ScreenshotTool");
                EditorApplication.Exit(1);
                return;
            }

            shot.runOnStart = true;
            // 截图期间关闭自动行走
            var walk = UnityEngine.Object.FindFirstObjectByType<AbyssEcho.DebugTools.ChapterWalkTest>();
            if (walk != null) walk.runOnStart = false;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            string projectRoot = Path.GetDirectoryName(Path.GetFullPath(Application.dataPath));
            string outDir = Path.GetFullPath(Path.Combine(projectRoot, "../Builds/v0.3B_Shots"));
            Directory.CreateDirectory(outDir);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = Path.Combine(outDir, "ABYSS_ECHO_Shots.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };

            Debug.Log("[S3B] 开始构建截图包");
            var report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            Debug.Log("[S3B] 构建结果：" + s.result + " | " + (s.totalSize / 1024 / 1024) + " MB | 错误 " + s.totalErrors);
            if (s.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}