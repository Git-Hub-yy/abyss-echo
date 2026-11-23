using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// S81：验证「跳过结局」与「帮助面板新增条目」。
    ///
    ///   T1 HelpUI 的 Sections 里包含「空格」和「M」
    ///   T2 SkipToFinale() 能把 Lines 阶段直接推到 Finale
    ///   T3 跳过后再走 2 秒 → IsFinished = true
    /// </summary>
    public class SkipEndingTest : MonoBehaviour
    {
        private readonly StringBuilder _log = new StringBuilder();
        private int _pass, _fail;
        private static readonly BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private void Start() => StartCoroutine(Run());

        private IEnumerator Run()
        {
            _log.AppendLine("══════ 跳过结局 / 帮助面板 验证 ══════");

            // ══════ T1：帮助面板条目 ══════
            _log.AppendLine();
            _log.AppendLine("──── T1 帮助面板按键表 ────");

            var helpType = typeof(AbyssEcho.UI.ControlsHelpUI);
            var sectionsF = helpType.GetField("Sections", BindingFlags.Static | BindingFlags.NonPublic);

            if (sectionsF == null)
            {
                Check("T1 Sections 字段存在", false, "null");
            }
            else
            {
                var sections = sectionsF.GetValue(null) as System.Array;
                if (sections == null) { Check("T1 Sections 非空", false, "null"); }
                else
                {
                    bool hasSpace = false, hasM = false;
                    var allKeys = new StringBuilder();

                    foreach (var sec in sections)
                    {
                        // 元组 (string group, (string key, string desc)[] items)
                        var secType = sec.GetType();
                        var groupF = secType.GetField("Item1");
                        var itemsF = secType.GetField("Item2");
                        string group = groupF?.GetValue(sec) as string ?? "?";
                        var items = itemsF?.GetValue(sec) as System.Array;

                        allKeys.Append($"  [{group}] ");
                        if (items != null)
                        {
                            foreach (var it in items)
                            {
                                var itType = it.GetType();
                                string key = itType.GetField("Item1")?.GetValue(it) as string ?? "?";
                                string desc = itType.GetField("Item2")?.GetValue(it) as string ?? "?";
                                allKeys.Append($"{key}={desc}; ");

                                if (key == "空格") hasSpace = true;
                                if (key == "M") hasM = true;
                            }
                        }
                        allKeys.AppendLine();
                    }

                    _log.AppendLine("  完整按键表：");
                    _log.AppendLine(allKeys.ToString());

                    Check("T1a 帮助里有「空格 跳过结局」", hasSpace, "");
                    Check("T1b 帮助里有「M 和平模式」", hasM, "");
                }
            }

            // ══════ T2：SkipToFinale ══════
            _log.AppendLine();
            _log.AppendLine("──── T2 SkipToFinale ────");

            var ending = FindFirstObjectByType<AbyssEcho.Narrative.ChapterEndingSystem>();
            if (ending == null)
            {
                Check("T2 ChapterEndingSystem 存在", false, "场景里没有");
                Dump(); yield break;
            }

            var eType = typeof(AbyssEcho.Narrative.ChapterEndingSystem);
            var phaseF = eType.GetField("_phase", BF);
            var lineIndexF = eType.GetField("_lineIndex", BF);
            var fadeAlphaF = eType.GetField("_fadeAlpha", BF);
            var timerF = eType.GetField("_timer", BF);
            var isFinishedP = eType.GetProperty("IsFinished", BF);
            var endingLinesF = eType.GetField("endingLines", BF);
            var skipM = eType.GetMethod("SkipToFinale", BF);

            if (skipM == null) { Check("T2 SkipToFinale 方法存在", false, "null"); Dump(); yield break; }

            // 把状态设成 Lines（模拟正在播文字）
            var lines = endingLinesF?.GetValue(ending) as string[];
            int lineCount = lines != null ? lines.Length : 0;
            _log.AppendLine($"  endingLines 共 {lineCount} 句");

            // Phase 是私有嵌套枚举，用 Enum.Parse
            var phaseType = phaseF.FieldType;
            object linesPhase = System.Enum.Parse(phaseType, "Lines");

            phaseF.SetValue(ending, linesPhase);
            lineIndexF?.SetValue(ending, 0);
            fadeAlphaF?.SetValue(ending, 0.3f);   // 模拟还在淡入
            timerF?.SetValue(ending, 0.5f);

            _log.AppendLine($"  设置前: _phase=Lines _lineIndex=0 _fadeAlpha=0.3");

            // 调用跳过
            skipM.Invoke(ending, null);

            object afterPhase = phaseF.GetValue(ending);
            int afterIndex = lineIndexF != null ? (int)lineIndexF.GetValue(ending) : -1;
            float afterFade = fadeAlphaF != null ? (float)fadeAlphaF.GetValue(ending) : -1f;

            _log.AppendLine($"  设置后: _phase={afterPhase} _lineIndex={afterIndex} _fadeAlpha={afterFade:F2}");

            Check("T2a 跳过进入 Finale", afterPhase.ToString() == "Finale", $"phase={afterPhase}");
            Check("T2b 文字索引推到末尾", afterIndex == lineCount, $"index={afterIndex} total={lineCount}");
            Check("T2c 黑幕完全不透明", Mathf.Abs(afterFade - 1f) < 0.01f, $"fade={afterFade:F2}");

            // ══════ T3：Finale 2 秒后 IsFinished ══════
            _log.AppendLine();
            _log.AppendLine("──── T3 跳过后 2 秒结局完成 ────");
            _log.AppendLine($"  当前 IsFinished = {isFinishedP?.GetValue(ending)}");

            // 等 2.5 秒（Finale 判定是 _timer > 2.0f）
            float waited = 0f;
            while (waited < 2.5f)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            bool finished = (bool)isFinishedP.GetValue(ending);
            _log.AppendLine($"  等待 {waited:F2} 秒后 IsFinished = {finished}");
            Check("T3 跳过后结局正常完成", finished, $"IsFinished={finished}");

            Dump();
        }

        private void Check(string name, bool ok, string detail)
        {
            if (ok) { _pass++; _log.AppendLine($"[通过] {name}  {detail}"); }
            else { _fail++; _log.AppendLine($"[失败] {name}  {detail}"); }
        }

        private void Dump()
        {
            _log.AppendLine();
            _log.AppendLine($"通过 {_pass}，失败 {_fail}");
            string text = _log.ToString();
            Debug.Log("<b>[SkipEndingTest]</b>\n" + text);
            try
            {
                Directory.CreateDirectory(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports");
                File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\SkipEndingTest.txt", text, new UTF8Encoding(false));
            }
            catch { }
            if (!Application.isEditor) Application.Quit(_fail == 0 ? 0 : 1);
        }
    }
}