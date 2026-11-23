using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
namespace AbyssEcho.Core.EditorTools {
    public static class BuildToolsCh3EndingTruth {
        [MenuItem("ABYSS ECHO/调查/构建第三章结局验证")]
        public static void BuildCh3() {
            string repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), ".."));
            string outDir = Path.Combine(repoRoot, "Builds", "Ch3Ending_Ch3");
            Directory.CreateDirectory(outDir);
            var opts = new BuildPlayerOptions {
                scenes = new[] { "Assets/_Project/Scenes/Level_Chapter3_Graybox.unity" },
                locationPathName = Path.Combine(outDir, "Ch3EndingCh3.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development,
            };
            var r = BuildPipeline.BuildPlayer(opts);
            Debug.Log($"[Ch3Ending] {r.summary.result} exists={File.Exists(opts.locationPathName)}");
        }
    }
}