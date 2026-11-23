using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S2 正式可玩包构建（不含自动化测试，供人工试玩验收）。
    /// 输出：Builds/v0.2_Stage2/ABYSS_ECHO.exe
    ///
    /// 注意：构建前会把场景中的 AutoTest_Stage2.runOnStart 置为 false，
    /// 保证人工试玩时不会自动跑测试并退出。
    /// </summary>
    public static class BuildToolsS2Release
    {
        private const string TestScenePath = "Assets/_Project/Scenes/TEST_Player.unity";

        public static void BuildRelease()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                TestScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);

            var test = UnityEngine.Object.FindFirstObjectByType<AbyssEcho.DebugTools.AutoTest_Stage2>();
            if (test != null)
            {
                test.runOnStart = false;   // 人工试玩不自动跑测试
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, TestScenePath);
                AssetDatabase.SaveAssets();
                Debug.Log("[ABYSS ECHO] 已关闭自动测试（runOnStart=false）并保存场景");
            }

            string projectRoot = Path.GetDirectoryName(Path.GetFullPath(Application.dataPath));
            string outDir = Path.GetFullPath(Path.Combine(projectRoot, "../Builds/v0.2_Stage2"));
            Directory.CreateDirectory(outDir);
            string exePath = Path.Combine(outDir, "ABYSS_ECHO.exe");

            var options = new BuildPlayerOptions
            {
                scenes = new[] { TestScenePath },
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            Debug.Log("[ABYSS ECHO] 开始构建 S2 正式包：" + exePath);
            var report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            Debug.Log("[ABYSS ECHO] 构建结果：" + s.result +
                      " | 大小 " + (s.totalSize / 1024 / 1024) + " MB" +
                      " | 耗时 " + s.totalTime.TotalSeconds.ToString("F1") + " s" +
                      " | 错误数 " + s.totalErrors);

            if (s.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}