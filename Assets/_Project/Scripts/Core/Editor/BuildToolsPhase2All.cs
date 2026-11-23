using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
namespace AbyssEcho.Core.EditorTools {
    public static class BuildToolsPhase2All {
        private static void BuildCh(int n) {
            string repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), ".."));
            string outDir = Path.Combine(repoRoot, "Builds", $"P2Ch{n}");
            Directory.CreateDirectory(outDir);
            var r = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { $"Assets/_Project/Scenes/Level_Chapter{n}_Graybox.unity" },
                locationPathName = Path.Combine(outDir, $"P2Ch{n}.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development,
            });
            Debug.Log($"[P2Ch{n}] {r.summary.result}");
        }
        public static void Build2() => BuildCh(2);
        public static void Build3() => BuildCh(3);
        public static void Build4() => BuildCh(4);
        public static void Build5() => BuildCh(5);
    }
}