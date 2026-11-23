using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// 批处理测试入口：加载 TEST_Player 场景并运行 S2 自动化验收测试。
    ///
    /// 为什么会话会"卡"在批处理播放模式：
    ///  - 批处理模式下没有真实显示器循环，因此需要手动驱动 EditorApplication.update，
    ///    让协程与物理正常推进；
    ///  - 测试脚本结束时会调用 Application.Quit(exitCode)，本入口捕获退出码。
    ///
    /// 用法：
    ///   Unity.exe -batchmode -projectPath &lt;工程&gt; -executeMethod AbyssEcho.Core.EditorTools.BatchTestS2.RunS2AutoTest
    /// </summary>
    [InitializeOnLoad]
    public static class BatchTestS2
    {
        private const string ScenePath = "Assets/_Project/Scenes/TEST_Player.unity";
        private const string PrefabPath = "Assets/_Project/Prefabs/Player/PF_Player.prefab";

        private static double _startTime;
        private static bool _running;
        private static bool _failed;

        [InitializeOnLoadMethod]
        private static void Init()
        {
            // 仅当命令行显式请求时运行，避免日常打开工程误触发
            string[] args = Environment.GetCommandLineArgs();
            bool shouldRun = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-executeMethod" && i + 1 < args.Length &&
                    args[i + 1].Contains("BatchTestS2.RunS2AutoTest"))
                {
                    shouldRun = true;
                }
            }
            if (!shouldRun) return;

            Debug.Log("[ABYSS ECHO] BatchTestS2 已注册，等待编译完成后启动测试");
            EditorApplication.delayCall += () => StartTest();
        }

        private static void StartTest()
        {
            if (_running) return;
            _running = true;
            _startTime = EditorApplication.timeSinceStartup;

            Debug.Log("[ABYSS ECHO] 加载测试场景：" + ScenePath);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // 确保测试组件存在：找不到就在玩家身上加一个（不修改场景文件）
            var test = UnityEngine.Object.FindFirstObjectByType<AbyssEcho.DebugTools.AutoTest_Stage2>();
            if (test == null)
            {
                var playerGo = GameObject.Find("Player");
                if (playerGo == null)
                {
                    Debug.LogError("[ABYSS ECHO] 场景中找不到 Player，无法执行测试");
                    Finish(false);
                    return;
                }
                test = playerGo.AddComponent<AbyssEcho.DebugTools.AutoTest_Stage2>();
                test.runOnStart = false;   // 由本入口控制启动时机
                Debug.Log("[ABYSS ECHO] 已在 Player 上临时添加 AutoTest_Stage2");
            }

            EditorApplication.update += Tick;

            // 进入播放模式
            EditorApplication.EnterPlaymode();

            // 启动协程：等播放模式就绪后运行测试
            EditorApplication.CallbackFunction kickoff = null;
            kickoff = () =>
            {
                if (!EditorApplication.isPlaying) return;
                EditorApplication.update -= kickoff;
                test.StartCoroutine(RunAndFinish(test));
            };
            EditorApplication.update += kickoff;
        }

        private static System.Collections.IEnumerator RunAndFinish(AbyssEcho.DebugTools.AutoTest_Stage2 test)
        {
            yield return test.RunAll();
            yield return new WaitForSeconds(0.5f);
            _failed = !TestPassed();
            EditorApplication.ExitPlaymode();
            yield return new WaitForSeconds(0.3f);
            Finish(!_failed);
        }

        private static bool TestPassed()
        {
            string path = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(Application.dataPath, "../Reports/S2_AutoTestResult.txt"));
            if (!System.IO.File.Exists(path)) return false;
            string text = System.IO.File.ReadAllText(path);
            return text.Contains("S2 自动化验收通过");
        }

        private static void Tick()
        {
            // 超时保护：180 秒
            if (EditorApplication.timeSinceStartup - _startTime > 180.0)
            {
                Debug.LogError("[ABYSS ECHO] 自动测试超时（180 秒），强制结束");
                Finish(false);
            }
        }

        private static void Finish(bool success)
        {
            EditorApplication.update -= Tick;
            Debug.Log("[ABYSS ECHO] 自动测试结束，结果：" + (success ? "通过" : "失败"));
            EditorApplication.Exit(success ? 0 : 1);
        }
    }
}