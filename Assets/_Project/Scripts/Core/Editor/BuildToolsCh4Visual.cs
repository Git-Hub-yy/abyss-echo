using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S56：构建第四章视觉审计包。
    ///
    /// ══════════════════ 关键约束 ══════════════════
    ///
    ///   本任务是"只读调查"，正式场景文件**必须保持字节级不变**。
    ///
    ///   方案：不在场景里加任何对象，
    ///        而是把场景**原样**加入构建，
    ///        再通过一个"启动器脚本"在运行时用 [RuntimeInitializeOnLoadMethod] 挂上审计组件。
    ///
    ///   这样正式场景从头到尾不被打开编辑。
    /// </summary>
    public static class BuildToolsCh4Visual
    {
        private const string SrcScene = "Assets/_Project/Scenes/Level_Chapter4_Graybox.unity";

        [MenuItem("ABYSS ECHO/调查/构建第四章视觉审计")]
        public static void BuildCh4() => Build(4);

        public static void Build(int chapterNum)
        {
            string repoRoot = Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(Application.dataPath), "../.."));
            string outDir = Path.Combine(repoRoot, "Builds", $"Ch4Visual_Ch{chapterNum}");
            Directory.CreateDirectory(outDir);

            var opts = new BuildPlayerOptions
            {
                scenes = new[] { SrcScene },     // 直接用正式场景，不复制、不修改
                locationPathName = Path.Combine(outDir, $"Ch4VisualCh{chapterNum}.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development,
            };

            Debug.Log($"[Ch4Visual] 输出路径 = {opts.locationPathName}");
            Debug.Log($"[Ch4Visual] 输出目录存在 = {Directory.Exists(outDir)}");

            var report = BuildPipeline.BuildPlayer(opts);
            Debug.Log($"[Ch4Visual] 构建结果 = {report.summary.result}  " +
                      $"错误数 = {report.summary.totalErrors}  " +
                      $"输出 = {report.summary.outputPath}");
            Debug.Log($"[Ch4Visual] exe 是否存在 = {File.Exists(opts.locationPathName)}");

            if (report.summary.result != BuildResult.Succeeded)
            {
                foreach (var step in report.steps)
                    foreach (var msg in step.messages)
                        if (msg.type == LogType.Error || msg.type == LogType.Exception)
                            Debug.LogError($"[Ch4Visual] 构建错误: {msg.content}");
            }
        }
    }
}