using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>S57：构建第四章终点门通关验证包（用运行时启动器，不改场景）。</summary>
    public static class BuildToolsCh4ExitFlow
    {
        [MenuItem("ABYSS ECHO/调查/构建第四章终点门验证")]
        public static void BuildCh4() => Build(4);

        public static void Build(int chapterNum)
        {
            string repoRoot = Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(Application.dataPath), ".."));
            string outDir = Path.Combine(repoRoot, "Builds", $"Ch4Exit_Ch{chapterNum}");
            Directory.CreateDirectory(outDir);

            var opts = new BuildPlayerOptions
            {
                scenes = new[] { $"Assets/_Project/Scenes/Level_Chapter{chapterNum}_Graybox.unity" },
                locationPathName = Path.Combine(outDir, $"Ch4ExitCh{chapterNum}.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development,
            };

            var report = BuildPipeline.BuildPlayer(opts);
            Debug.Log($"[Ch4Exit] 构建 = {report.summary.result}  输出 = {opts.locationPathName}  " +
                      $"存在 = {File.Exists(opts.locationPathName)}");
        }
    }
}