using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S1 构建验证：打包一个最小 Windows 可执行版本，用来证明"工程能出包"。
    /// 用法：
    ///   Unity.exe -batchmode -quit -projectPath &lt;工程&gt; -executeMethod AbyssEcho.Core.EditorTools.BatchEntry.BuildWindowsSmokeTest
    /// 输出：&lt;工程根&gt;/../Builds/v0.1_Stage1/ABYSS_ECHO.exe
    /// </summary>
    public static class BuildTools
    {
        private const string TestScenePath = "Assets/_Project/Scenes/TEST_Player.unity";

        public static void BuildWindowsSmokeTest()
        {
            string projectRoot = Path.GetDirectoryName(Path.GetFullPath(Application.dataPath));
            string outDir = Path.GetFullPath(Path.Combine(projectRoot, "../Builds/v0.1_Stage1"));
            Directory.CreateDirectory(outDir);

            var scenes = new[] { TestScenePath };

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(outDir, "ABYSS_ECHO.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            Debug.Log("[ABYSS ECHO] 开始构建：" + options.locationPathName);
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;

            Debug.Log("[ABYSS ECHO] 构建结果：" + summary.result +
                      " | 大小：" + (summary.totalSize / 1024 / 1024) + " MB" +
                      " | 耗时：" + summary.totalTime.TotalSeconds.ToString("F1") + " s" +
                      " | 错误数：" + summary.totalErrors +
                      " | 警告数：" + summary.totalWarnings);

            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError("[ABYSS ECHO] 构建失败，请查看上方日志与 log 文件。");
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }
    }
}