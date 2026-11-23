using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    public static class BuildToolsMaterialShot
    {
        [MenuItem("ABYSS ECHO/美术馆/构建第3章截图")]
        public static void Build3() => BuildCh(3);
        [MenuItem("ABYSS ECHO/美术馆/构建第4章截图")]
        public static void Build4() => BuildCh(4);
        [MenuItem("ABYSS ECHO/美术馆/构建第5章截图")]
        public static void Build5() => BuildCh(5);

        public static void BuildCh(int ch)
        {
            string repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), ".."));
            string outDir = Path.Combine(repoRoot, "Builds", "MatShot" + ch);
            Directory.CreateDirectory(outDir);

            string scenePath = "Assets/_Project/Scenes/Level_Chapter" + ch + "_Graybox.unity";
            if (!File.Exists(scenePath))
            {
                Debug.LogError("[MatShot] 场景不存在: " + scenePath);
                return;
            }

            var opts = new BuildPlayerOptions
            {
                scenes = new[] { scenePath },
                locationPathName = Path.Combine(outDir, "MatShot" + ch + ".exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development,
            };
            var r = BuildPipeline.BuildPlayer(opts);
            Debug.Log("[MatShot] Ch" + ch + " " + r.summary.result + " exists=" + File.Exists(opts.locationPathName));
        }
    }
}