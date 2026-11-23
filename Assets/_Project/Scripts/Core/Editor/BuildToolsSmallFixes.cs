using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
namespace AbyssEcho.Core.EditorTools {
    public static class BuildToolsSmallFixes {
        public static void Build() {
            string repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), ".."));
            string outDir = Path.Combine(repoRoot, "Builds", "SmallFix");
            Directory.CreateDirectory(outDir);
            var r = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity" },
                locationPathName = Path.Combine(outDir, "SmallFix.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development,
            });
            Debug.Log($"[SmallFix] {r.summary.result}");
        }
    }
}