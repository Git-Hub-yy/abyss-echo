using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S3A 正式可玩包（关闭自动行走，供人工试玩灰盒）。
    /// 输出：Builds/v0.3_Stage3A/
    /// </summary>
    public static class BuildToolsS3ARelease
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity";

        public static void BuildRelease()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var walk = UnityEngine.Object.FindFirstObjectByType<AbyssEcho.DebugTools.ChapterWalkTest>();
            if (walk != null)
            {
                walk.runOnStart = false;   // 人工试玩不自动行走
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.SaveAssets();
                Debug.Log("[ABYSS ECHO] 已关闭自动行走并保存场景");
            }

            string projectRoot = Path.GetDirectoryName(Path.GetFullPath(Application.dataPath));
            string outDir = Path.GetFullPath(Path.Combine(projectRoot, "../Builds/v0.3_Stage3A"));
            Directory.CreateDirectory(outDir);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = Path.Combine(outDir, "ABYSS_ECHO.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            Debug.Log("[ABYSS ECHO] 开始构建 S3A 正式包");
            var report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            Debug.Log("[ABYSS ECHO] 构建结果：" + s.result + " | " + (s.totalSize / 1024 / 1024) + " MB | 错误 " + s.totalErrors);
            if (s.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}