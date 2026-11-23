using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
namespace AbyssEcho.Core.EditorTools {
    public static class BuildToolsCh2MatShot {
        [MenuItem("ABYSS ECHO/美术 v8.2/构建第二章材质截图")]
        public static void BuildCh2() {
            string repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), ".."));
            string outDir = Path.Combine(repoRoot, "Builds", "Ch2Mat_Ch2");
            Directory.CreateDirectory(outDir);
            var opts = new BuildPlayerOptions {
                scenes = new[] { "Assets/_Project/Scenes/Level_Chapter2_Graybox.unity" },
                locationPathName = Path.Combine(outDir, "Ch2MatCh2.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development,
            };
            var r = BuildPipeline.BuildPlayer(opts);
            Debug.Log($"[Ch2Mat] {r.summary.result} exists={File.Exists(opts.locationPathName)}");
        }
    }
}