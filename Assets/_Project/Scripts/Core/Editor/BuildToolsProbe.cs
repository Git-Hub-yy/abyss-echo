using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
namespace AbyssEcho.Core.EditorTools {
    public static class BuildToolsProbe {
        [MenuItem("ABYSS ECHO/调查/构建相机探测")]
        public static void BuildCh4() {
            string repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), ".."));
            string outDir = Path.Combine(repoRoot, "Builds", "Probe_Ch4");
            Directory.CreateDirectory(outDir);
            var opts = new BuildPlayerOptions {
                scenes = new[] { "Assets/_Project/Scenes/Level_Chapter4_Graybox.unity" },
                locationPathName = Path.Combine(outDir, "ProbeCh4.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development,
            };
            BuildPipeline.BuildPlayer(opts);
        }
    }
}