using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
namespace AbyssEcho.Core.EditorTools {
    public static class BuildToolsCh1MatShot {
        [MenuItem("ABYSS ECHO/美术 v8.0/构建第一章材质截图")]
        public static void BuildCh1() {
            string repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), ".."));
            string outDir = Path.Combine(repoRoot, "Builds", "Ch1Mat_Ch1");
            Directory.CreateDirectory(outDir);
            var opts = new BuildPlayerOptions {
                scenes = new[] { "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity" },
                locationPathName = Path.Combine(outDir, "Ch1MatCh1.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development,
            };
            var r = BuildPipeline.BuildPlayer(opts);
            Debug.Log($"[Ch1Mat] {r.summary.result} exists={File.Exists(opts.locationPathName)}");
        }
    }
}