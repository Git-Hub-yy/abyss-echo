using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// 阶段二统一测试启动器。
    ///
    /// 用命令行参数选择要跑的测试套件：
    ///   -t stage2      玩家移动/视角/碰撞
    ///   -t death       死亡系统
    ///   -t interact    交互系统
    ///   -t combat      战斗系统
    ///   -t monster     怪物 AI
    ///   -t item        道具系统
    ///   -t pause       暂停恢复
    ///   -t all         全部顺序执行
    ///
    /// 结果写入 Reports/Final_Test/02_Raw_<test>.txt
    /// </summary>
    public static class Phase2Launcher
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (!Debug.isDebugBuild) return;

            string test = ReadArg("-t");
            if (string.IsNullOrEmpty(test)) return;

            Debug.Log($"[Phase2] 启动测试: {test}");

            var go = new GameObject("--- PHASE2 ---");
            Object.DontDestroyOnLoad(go);
            var runner = go.AddComponent<Phase2Runner>();
            runner.testName = test;
        }

        private static string ReadArg(string name)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }
    }

    /// <summary>
    /// 执行器：负责挂载对应的测试组件并收集结果。
    /// </summary>
    public class Phase2Runner : MonoBehaviour
    {
        public string testName;
        private readonly StringBuilder _log = new StringBuilder();

        private void Start() => StartCoroutine(Run());

        private IEnumerator Run()
        {
            _log.AppendLine($"══════ 阶段二测试：{testName} ══════");
            _log.AppendLine($"场景: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
            _log.AppendLine($"时间: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            _log.AppendLine();

            yield return new WaitForSeconds(0.6f);

            switch (testName)
            {
                case "stage2":
                    yield return RunStage2();
                    break;
                case "death":
                    yield return RunDeath();
                    break;
                case "precise":
                    yield return RunPrecise();
                    break;
                case "inputdiag":
                    yield return RunInputDiag();
                    break;
                case "movement":
                    yield return RunMovement();
                    break;
                case "remaining":
                    yield return RunRemaining();
                    break;
                case "legacyinput":
                    yield return RunLegacy();
                    break;
                case "systems":
                    yield return RunSystems();
                    break;
                case "playerflow":
                    yield return RunPlayerFlow();
                    break;
                case "fall":
                    gameObject.AddComponent<FallCheckTest>();
                    yield return new WaitForSeconds(14f);
                    break;
                case "camcheck":
                    gameObject.AddComponent<CamCheckTest>();
                    yield return new WaitForSeconds(9f);
                    break;
                case "interdiag":
                    yield return RunInterDiag();
                    break;
                default:
                    _log.AppendLine($"❌ 未知测试: {testName}");
                    break;
            }

            _log.AppendLine();
            _log.AppendLine("══════ 测试结束 ══════");
            Dump();
        }

        private IEnumerator RunStage2()
        {
            var comp = gameObject.AddComponent<AutoTest_Stage2>();
            comp.runOnStart = false;
            comp.writeReport = false;

            // 等待章节标题卡结束（它会禁用 PlayerMotor 5 秒）
            var card = FindFirstObjectByType<AbyssEcho.UI.ChapterTitleCard>();
            if (card != null && card.enabled)
            {
                _log.AppendLine("  ⏳ 等待章节标题卡结束...");
                float w = 0f;
                while (card != null && card.enabled && w < 12f) { yield return new WaitForSeconds(0.25f); w += 0.25f; }
                _log.AppendLine($"  ✅ 标题卡结束（{w:F1}s）");
            }
            yield return new WaitForSeconds(0.5f);

            _log.AppendLine("──── 运行 AutoTest_Stage2 ────");

            // 通过反射读 _log / _pass / _fail
            var t = typeof(AutoTest_Stage2);
            var BF = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
            var logF = t.GetField("_log", BF);
            var passF = t.GetField("_pass", BF);
            var failF = t.GetField("_fail", BF);

            yield return comp.RunAll();
            yield return new WaitForSeconds(0.2f);

            if (logF != null)
            {
                var list = logF.GetValue(comp) as System.Collections.IList;
                if (list != null)
                    foreach (var s in list) _log.AppendLine("  " + s);
            }

            int p = passF != null ? (int)passF.GetValue(comp) : -1;
            int f = failF != null ? (int)failF.GetValue(comp) : -1;
            _log.AppendLine();
            _log.AppendLine($"  >>> 通过 {p}，失败 {f}");

            Destroy(comp);
        }

        private IEnumerator RunInterDiag()
        {
            var comp = gameObject.AddComponent<InteractionDiagTest>();
            yield return comp.RunAll();
            Destroy(comp);
        }

        private IEnumerator RunPlayerFlow()
        {
            var comp = gameObject.AddComponent<Phase2PlayerFlowTest>();
            yield return comp.RunAll();
            Destroy(comp);
        }

        private IEnumerator RunSystems()
        {
            var comp = gameObject.AddComponent<Phase2SystemsTest>();
            yield return comp.RunAll();
            Destroy(comp);
        }

        private IEnumerator RunLegacy()
        {
            var comp = gameObject.AddComponent<LegacyInputTest>();
            yield return comp.RunAll();
            Destroy(comp);
        }

        private IEnumerator RunRemaining()
        {
            var comp = gameObject.AddComponent<Phase2RemainingDiag>();
            yield return comp.RunAll();
            Destroy(comp);
        }

        private IEnumerator RunMovement()
        {
            var comp = gameObject.AddComponent<MovementDiagTest>();
            yield return comp.RunAll();
            Destroy(comp);
        }

        private IEnumerator RunInputDiag()
        {
            var comp = gameObject.AddComponent<InputDiagTest>();
            yield return comp.RunAll();
            Destroy(comp);
        }

        private IEnumerator RunPrecise()
        {
            var comp = gameObject.AddComponent<Phase2PreciseTest>();
            _log.AppendLine("──── 运行 Phase2PreciseTest ────");
            yield return comp.RunAll();
            Destroy(comp);
        }

        private IEnumerator RunDeath()
        {
            var comp = gameObject.AddComponent<AutoTest_Death>();
            _log.AppendLine("──── 运行 AutoTest_Death ────");

            // AutoTest_Death 的 Start 会自动跑 RunAll，等它跑完
            yield return new WaitForSeconds(4.0f);

            var t = typeof(AutoTest_Death);
            var BF = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
            var logF = t.GetField("_log", BF);
            var passF = t.GetField("_pass", BF);
            var failF = t.GetField("_fail", BF);

            if (logF != null)
            {
                var val = logF.GetValue(comp);
                if (val is StringBuilder sbr) _log.Append(sbr.ToString());
                else if (val != null) _log.AppendLine(val.ToString());
            }

            int p = passF != null ? (int)passF.GetValue(comp) : -1;
            int f = failF != null ? (int)failF.GetValue(comp) : -1;
            _log.AppendLine();
            _log.AppendLine($"  >>> 通过 {p}，失败 {f}");

            Destroy(comp);
        }

        private void Dump()
        {
            string text = _log.ToString();
            Debug.Log("<b>[Phase2]</b>\n" + text);
            try
            {
                Directory.CreateDirectory(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Final_Test");
                File.WriteAllText($@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Final_Test\02_Raw_{testName}.txt",
                                  text, new UTF8Encoding(false));
            }
            catch { }
            Application.Quit(0);
        }
    }
}