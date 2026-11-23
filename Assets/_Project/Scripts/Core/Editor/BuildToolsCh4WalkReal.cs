using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
namespace AbyssEcho.Core.EditorTools {
    public static class BuildToolsCh4WalkReal {
        [MenuItem("ABYSS ECHO/调查/构建第四章真实行走验证")]
        public static void BuildCh4() {
            string repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), ".."));
            string outDir = Path.Combine(repoRoot, "Builds", "Ch4Walk_Ch4");
            Directory.CreateDirectory(outDir);
            var opts = new BuildPlayerOptions {
                scenes = new[] { "Assets/_Project/Scenes/Level_Chapter4_Graybox.unity" },
                locationPathName = Path.Combine(outDir, "Ch4WalkCh4.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development,
            };
            var r = BuildPipeline.BuildPlayer(opts);
            Debug.Log($"[Ch4Walk] {r.summary.result} exists={File.Exists(opts.locationPathName)}");
        }
    }
}