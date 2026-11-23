using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
namespace AbyssEcho.Core.EditorTools {
    public static class BuildToolsCh5Detect {
        public static void Build() {
            string repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), ".."));
            string outDir = Path.Combine(repoRoot, "Builds", "Ch5Detect");
            Directory.CreateDirectory(outDir);
            var opts = new BuildPlayerOptions {
                scenes = new[] { "Assets/_Project/Scenes/Level_Chapter5_Graybox.unity" },
                locationPathName = Path.Combine(outDir, "Ch5Detect.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development,
            };
            var r = BuildPipeline.BuildPlayer(opts);
            Debug.Log($"[Ch5Detect] {r.summary.result}");
        }
    }
}