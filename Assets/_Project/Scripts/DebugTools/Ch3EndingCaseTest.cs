using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// S64b：第三章三结局**逐项验证**。
    ///
    /// 每次运行只测一种选择（通过环境变量 CH3_CASE 指定），
    /// 因为结局播放一次后章节就结算了，不能在同一进程里重来。
    ///
    ///   CH3_CASE=tape  → 放录音带 → 期望结局 A「回声」
    ///   CH3_CASE=fuse  → 放保险丝 → 期望结局 C「深入」
    ///   CH3_CASE=none  → 什么都不放 → 期望结局 B「离开」
    /// </summary>
    public class Ch3EndingCaseTest : MonoBehaviour
    {
        public bool runOnStart = true;

        private readonly StringBuilder _log = new StringBuilder();
        private int _pass, _fail, _unverified;
        private static readonly BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private void Start()
        {
            if (runOnStart) StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            string testCase = System.Environment.GetEnvironmentVariable("CH3_CASE") ?? "none";

            AbyssEcho.LevelGen.ChapterLayout.Current =
                AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter3;
            var ending = FindFirstObjectByType<AbyssEcho.Narrative.ChapterEndingSystem>();
            ending?.GetType().GetMethod("ApplyChapterConfig", BF)?.Invoke(ending, null);
            yield return new WaitForSeconds(0.6f);

            _log.AppendLine("══════ S64b 第三章结局逐项验证 ══════");
            _log.AppendLine($"测试用例 = {testCase}");
            _log.AppendLine();

            var altar = FindFirstObjectByType<AbyssEcho.Puzzle.OfferingAltar>();
            var inv = AbyssEcho.Inventory.PlayerInventory.Instance;
            if (altar == null || inv == null || ending == null)
            {
                _log.AppendLine("❌ 缺组件"); Dump(testCase); yield break;
            }

            var t = typeof(AbyssEcho.Narrative.ChapterEndingSystem);
            var linesF = t.GetField("endingLines", BF);
            var footerF = t.GetField("chapterFooter", BF);
            var titleF = t.GetField("chapterTitle", BF);

            // ══ 按用例做选择 ══
            _log.AppendLine("──── 执行选择 ────");
            string expectedId, expectedFooter, expectedFirstLine;

            switch (testCase)
            {
                case "tape":
                    inv.Add("item.tape");
                    altar.Offer("item.tape");
                    expectedId = "A";
                    expectedFooter = "结局：回声";
                    expectedFirstLine = "你把录音带放在石台上。";
                    _log.AppendLine("  放：录音带 (item.tape)");
                    break;

                case "fuse":
                    inv.Add("item.fuse");
                    altar.Offer("item.fuse");
                    expectedId = "C";
                    expectedFooter = "结局：深入";
                    expectedFirstLine = "你把保险丝放在石台上。";
                    _log.AppendLine("  放：保险丝 (item.fuse)");
                    break;

                default:
                    expectedId = "B";
                    expectedFooter = "结局：离开";
                    expectedFirstLine = "你什么也没有放。";
                    _log.AppendLine("  放：什么都不放");
                    break;
            }

            yield return new WaitForSeconds(0.2f);

            string actualId = altar.GetEndingId();
            _log.AppendLine($"  altar.GetEndingId() = \"{actualId}\"");
            Check("T1 供奉台返回的结局分支正确", actualId == expectedId,
                  $"期望={expectedId} 实际={actualId}");

            // ══ 触发结局 ══
            _log.AppendLine();
            _log.AppendLine("──── 触发结局 ────");
            var tcF = t.GetField("triggerCenter", BF);
            Vector3 tc = (Vector3)tcF.GetValue(ending);
            var motor = FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
            var cc = motor != null ? motor.GetComponent<CharacterController>() : null;
            if (cc != null) cc.enabled = false;
            motor.transform.position = new Vector3(tc.x, 0.05f, tc.z);
            if (cc != null) cc.enabled = true;
            _log.AppendLine($"  玩家传送到 {tc}");

            float wait = 0f;
            var phaseF = t.GetField("_phase", BF);
            while (wait < 15f)
            {
                string ph = phaseF?.GetValue(ending)?.ToString() ?? "";
                if (ph == "Lines" || ph == "Finished") break;
                wait += Time.deltaTime;
                yield return null;
            }
            yield return new WaitForSeconds(0.5f);

            _log.AppendLine($"  {wait:F1}s 后 _phase = {phaseF?.GetValue(ending)}");

            // ══ 检查实际播放的内容 ══
            _log.AppendLine();
            _log.AppendLine("──── 实际播放的结局 ────");
            string footer = footerF?.GetValue(ending) as string ?? "";
            string title = titleF?.GetValue(ending) as string ?? "";
            var lines = linesF?.GetValue(ending) as string[];

            _log.AppendLine($"  标题 = \"{title}\"");
            _log.AppendLine($"  结语 = \"{footer}\"");
            _log.AppendLine($"  行数 = {lines?.Length ?? 0}");
            if (lines != null)
                for (int i = 0; i < Mathf.Min(3, lines.Length); i++)
                    _log.AppendLine($"    [{i}] {lines[i]}");

            Check("T2 结语正确", footer == expectedFooter,
                  $"期望=\"{expectedFooter}\" 实际=\"{footer}\"");
            Check("T3 第一句正确",
                  lines != null && lines.Length > 0 && lines[0] == expectedFirstLine,
                  $"期望=\"{expectedFirstLine}\" 实际=\"{(lines != null && lines.Length > 0 ? lines[0] : "")}\"");

            // ══ 检查不能重复选择 ══
            _log.AppendLine();
            _log.AppendLine("──── 重复选择检查 ────");
            string before = altar.OfferedItemId;
            bool hasBefore = altar.HasOffered;
            altar.Offer("item.tape");
            _log.AppendLine($"  再次调用 Offer 后 HasOffered = {altar.HasOffered}");
            _log.AppendLine($"  OfferedItemId: \"{before}\" → \"{altar.OfferedItemId}\"");
            Check("T4 不能重复供奉", altar.OfferedItemId == before,
                  $"保持不变={altar.OfferedItemId == before}");

            // ══ 检查不能重复触发结局 ══
            var triggeredF = t.GetField("_triggered", BF);
            bool trig1 = (bool)(triggeredF?.GetValue(ending) ?? false);
            _log.AppendLine($"  _triggered = {trig1}");
            Check("T5 结局已标记触发（不会重复）", trig1, $"triggered={trig1}");

            _log.AppendLine();
            Unverified("U1 真人看到的画面表现");

            Dump(testCase);
        }

        private void Check(string name, bool ok, string detail)
        {
            if (ok) { _pass++; _log.AppendLine($"[通过] {name}  {detail}"); }
            else { _fail++; _log.AppendLine($"[失败] {name}  {detail}"); }
        }

        private void Unverified(string n) { _unverified++; _log.AppendLine($"[待人工验收] {n}"); }

        private void Dump(string testCase)
        {
            _log.AppendLine();
            _log.AppendLine("=====================================");
            _log.AppendLine($"用例 {testCase}：通过 {_pass}，失败 {_fail}，待人工验收 {_unverified}");

            string text = _log.ToString();
            Debug.Log("<b>[Ch3EndingCaseTest]</b>\n" + text);

            try
            {
                Directory.CreateDirectory(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports");
                File.WriteAllText($@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Ch3Ending_Case_{testCase}.txt",
                                  text, new UTF8Encoding(false));
            }
            catch { }

            if (!Application.isEditor) Application.Quit(_fail == 0 ? 0 : 1);
        }
    }
}