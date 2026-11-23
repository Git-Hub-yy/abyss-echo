using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// 构建"S2 自动化验收"专用包：
    /// 场景中挂有 AutoTest_Stage2，运行后自动执行 16 项断言并把结果写到 exe 同级的 result 文件，
    /// 然后退出进程（退出码 0 = 全部通过，1 = 有失败）。
    ///
    /// 为什么用打包方式而不是编辑器播放模式：
    ///  - 批处理模式下进入 Play Mode 无法可靠驱动协程/物理循环（Unity 的已知限制）；
    ///  - 打包运行是"真实游戏环境"，更接近最终交付形态，验证更有说服力。
    /// </summary>
    public static class BuildToolsS2
    {
        private const string TestScenePath = "Assets/_Project/Scenes/TEST_Player.unity";

        public static void BuildAndRunTest()
        {
            // 打开场景，确保测试组件处于"自动运行"状态（仅本次构建，构建后不保存场景）
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                TestScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);

            var test = UnityEngine.Object.FindFirstObjectByType<AbyssEcho.DebugTools.AutoTest_Stage2>();
            if (test == null)
            {
                var playerGo = GameObject.Find("Player");
                if (playerGo != null) test = playerGo.AddComponent<AbyssEcho.DebugTools.AutoTest_Stage2>();
            }
            if (test == null)
            {
                Debug.LogError("[ABYSS ECHO] 找不到 AutoTest_Stage2，无法构建测试包");
                EditorApplication.Exit(1);
                return;
            }
            test.runOnStart = true;

            // 关键：构建读取的是磁盘上的场景文件，因此必须把修改保存回场景，
            // 否则 exe 里 runOnStart 仍然是序列化的 false（S2 实测踩坑）。
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, TestScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[ABYSS ECHO] 已将 runOnStart=true 保存到测试场景");

            string projectRoot = Path.GetDirectoryName(Path.GetFullPath(Application.dataPath));
            string outDir = Path.GetFullPath(Path.Combine(projectRoot, "../Builds/v0.2_Stage2_Test"));
            Directory.CreateDirectory(outDir);

            string exePath = Path.Combine(outDir, "ABYSS_ECHO_S2Test.exe");

            var options = new BuildPlayerOptions
            {
                scenes = new[] { TestScenePath },
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                // 不自动运行：由外部脚本启动，便于捕获退出码与日志
                options = BuildOptions.Development
            };

            Debug.Log("[ABYSS ECHO] 开始构建 S2 测试包：" + exePath);
            var report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            Debug.Log("[ABYSS ECHO] 构建结果：" + s.result + " | 大小 " + (s.totalSize / 1024 / 1024) + " MB | 错误数 " + s.totalErrors);

            // 不保存场景修改（runOnStart 只在构建时临时为 true）
            if (s.result != BuildResult.Succeeded)
            {
                Debug.LogError("[ABYSS ECHO] 构建失败");
                EditorApplication.Exit(1);
            }
        }
    }
}