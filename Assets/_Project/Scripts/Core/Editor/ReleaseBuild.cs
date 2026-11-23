using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// 正式发布构建（Windows 64-bit）。
    ///
    /// 输出：Builds/Release/ABYSS_ECHO_Final/ABYSS_ECHO_Final.exe
    ///
    /// 构建选项：
    ///   · 不使用 BuildOptions.Development —— 这是正式版
    ///   · 场景列表直接读 EditorBuildSettings（MainMenu + 五章）
    ///
    /// 构建后自动生成构建信息文件。
    /// </summary>
    public static class ReleaseBuild
    {
        [MenuItem("ABYSS ECHO/正式构建 Windows64")]
        public static void Build()
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════");
            sb.AppendLine("  ABYSS ECHO · 正式构建");
            sb.AppendLine("═══════════════════════════════════════");
            sb.AppendLine($"Unity 版本    : {Application.unityVersion}");
            sb.AppendLine($"产品名        : {Application.productName}");
            sb.AppendLine($"版本号        : {Application.version}");
            sb.AppendLine($"目标平台      : StandaloneWindows64");
            sb.AppendLine($"构建时间      : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"构建类型      : Release（非 Development）");
            sb.AppendLine();

            // ── 场景列表 ──
            var scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled && File.Exists(s.path))
                .Select(s => s.path)
                .ToArray();

            sb.AppendLine($"场景数        : {scenes.Length}");
            for (int i = 0; i < scenes.Length; i++)
                sb.AppendLine($"  [{i}] {scenes[i]}");
            sb.AppendLine();

            if (scenes.Length == 0)
            {
                Debug.LogError("[Release] 构建列表中没有启用的场景，中止构建");
                return;
            }

            // ── 输出路径 ──
            string repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), ".."));
            string outDir = Path.Combine(repoRoot, "Builds", "Release", "ABYSS_ECHO_Final");
            Directory.CreateDirectory(outDir);

            string exePath = Path.Combine(outDir, "ABYSS_ECHO_Final.exe");
            sb.AppendLine($"输出目录      : {outDir}");
            sb.AppendLine($"EXE 路径      : {exePath}");
            sb.AppendLine();

            // ── 执行构建 ──
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,     // ← Release 构建，不带 Development
            };

            Debug.Log("[Release] 开始构建…");
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;

            sb.AppendLine("═══════════════════════════════════════");
            sb.AppendLine($"构建结果      : {summary.result}");
            sb.AppendLine($"耗时          : {summary.totalTime}");
            sb.AppendLine($"总大小        : {FormatBytes((long)summary.totalSize)}");
            sb.AppendLine($"错误数        : {summary.totalErrors}");
            sb.AppendLine($"警告数        : {summary.totalWarnings}");
            sb.AppendLine($"输出路径      : {summary.outputPath}");
            sb.AppendLine("═══════════════════════════════════════");

            // ── 统计输出文件 ──
            if (Directory.Exists(outDir))
            {
                var files = Directory.GetFiles(outDir, "*", SearchOption.AllDirectories);
                long total = 0;
                foreach (var f in files) total += new FileInfo(f).Length;

                sb.AppendLine();
                sb.AppendLine($"输出文件数    : {files.Length}");
                sb.AppendLine($"输出总大小    : {FormatBytes(total)}");
                sb.AppendLine();
                sb.AppendLine("目录结构：");
                sb.AppendLine($"  {Path.GetFileName(outDir)}/");
                foreach (var d in Directory.GetDirectories(outDir))
                    sb.AppendLine($"    {Path.GetFileName(d)}/  ({Directory.GetFiles(d, "*", SearchOption.AllDirectories).Length} 个文件)");
                foreach (var f in Directory.GetFiles(outDir))
                    sb.AppendLine($"    {Path.GetFileName(f)}  ({FormatBytes(new FileInfo(f).Length)})");
            }

            string text = sb.ToString();
            Debug.Log(text);

            string infoPath = Path.Combine(outDir, "BUILD_INFO.txt");
            File.WriteAllText(infoPath, text, new UTF8Encoding(false));
            Debug.Log($"[Release] 构建信息已写入 {infoPath}");

            // 同时写一份到 Reports
            string reportDir = Path.Combine(repoRoot, "Reports", "Final_Test");
            Directory.CreateDirectory(reportDir);
            File.WriteAllText(Path.Combine(reportDir, "ReleaseBuild_Raw.txt"), text, new UTF8Encoding(false));

            if (summary.result != BuildResult.Succeeded)
                Debug.LogError($"[Release] 构建失败：{summary.result}");
            else
                Debug.Log($"[Release] 构建成功：{exePath}");
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("F1") + " KB";
            if (bytes < 1024L * 1024 * 1024) return (bytes / 1024.0 / 1024.0).ToString("F1") + " MB";
            return (bytes / 1024.0 / 1024.0 / 1024.0).ToString("F2") + " GB";
        }
    }
}