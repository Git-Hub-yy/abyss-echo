using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
namespace AbyssEcho.Core.EditorTools {
    public static class BuildToolsCh4Objective {
        [MenuItem("ABYSS ECHO/修复 v9.5/构建第四章目标测试")]
        public static void Build() {
            string repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), ".."));
            string outDir = Path.Combine(repoRoot, "Builds", "Ch4ObjTest");
            Directory.CreateDirectory(outDir);
            var opts = new BuildPlayerOptions {
                scenes = new[] { "Assets/_Project/Scenes/Level_Chapter4_Graybox.unity" },
                locationPathName = Path.Combine(outDir, "Ch4ObjTest.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development,
            };
            var r = BuildPipeline.BuildPlayer(opts);
            Debug.Log($"[Ch4Obj] {r.summary.result} exists={File.Exists(opts.locationPathName)}");
        }
    }
}