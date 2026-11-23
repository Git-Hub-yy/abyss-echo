using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S3A 构建"灰盒流程测试包"：场景中开启 ChapterWalkTest 自动行走，跑完自动退出。
    /// 输出：Builds/v0.3_Stage3A_Test/
    /// 用法：-executeMethod AbyssEcho.Core.EditorTools.BuildToolsS3A.BuildWalkTest
    /// </summary>
    public static class BuildToolsS3A
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity";

        public static void BuildWalkTest()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var walk = UnityEngine.Object.FindFirstObjectByType<AbyssEcho.DebugTools.ChapterWalkTest>();
            if (walk == null)
            {
                var playerGo = GameObject.Find("Player");
                if (playerGo != null) walk = playerGo.AddComponent<AbyssEcho.DebugTools.ChapterWalkTest>();
            }
            if (walk == null)
            {
                Debug.LogError("[ABYSS ECHO] 找不到 ChapterWalkTest");
                EditorApplication.Exit(1);
                return;
            }

            walk.runOnStart = true;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[ABYSS ECHO] 已开启 ChapterWalkTest 自动行走并保存场景");

            string projectRoot = Path.GetDirectoryName(Path.GetFullPath(Application.dataPath));
            string outDir = Path.GetFullPath(Path.Combine(projectRoot, "../Builds/v0.3_Stage3A_Test"));
            Directory.CreateDirectory(outDir);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = Path.Combine(outDir, "ABYSS_ECHO_WalkTest.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };

            Debug.Log("[ABYSS ECHO] 开始构建流程测试包：" + options.locationPathName);
            var report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            Debug.Log("[ABYSS ECHO] 构建结果：" + s.result + " | " + (s.totalSize / 1024 / 1024) + " MB | 错误 " + s.totalErrors);
            if (s.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}