using System.Collections;
using System.IO;
using System.Text;
using AbyssEcho.LevelGen;
using AbyssEcho.Narrative;
using AbyssEcho.UI;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// S14 自动测试：第二章结局、章节选择、完整流程。
    /// </summary>
    public class AutoTest_S14 : MonoBehaviour
    {
        public bool runOnStart = true;

        private int _pass, _fail;
        private readonly StringBuilder _log = new StringBuilder();

        private void Start()
        {
            if (runOnStart) StartCoroutine(RunAll());
        }

        private IEnumerator RunAll()
        {
            _log.AppendLine("ABYSS ECHO · S14 结局与章节选择测试");
            _log.AppendLine("=====================================");

            yield return new WaitForSeconds(0.5f);

            T95_EndingLinesExist();
            T96_Chapter2Lines();
            T97_EndingFollowsChapter();
            T98_ChapterSelectPanel();
            T99_ChapterSwitchPersists();
            T100_BothChaptersPlayable();

            _log.AppendLine("=====================================");
            _log.AppendLine($"结果：通过 {_pass}，失败 {_fail}");

            string text = _log.ToString();
            Debug.Log("<b>[AutoTest_S14]</b>\n" + text);
            WriteReport(text);

            // 恢复第一章
            ChapterLayout.ResetToFirst();

            yield return new WaitForSeconds(0.2f);
            if (!Application.isEditor) Application.Quit(_fail == 0 ? 0 : 1);
        }

        private void T95_EndingLinesExist()
        {
            var d1 = ChapterEndingLines.Get(ChapterLayout.Chapter.Chapter1);
            bool ok = d1 != null && d1.Lines != null && d1.Lines.Length >= 3;

            Check("T95 第一章结局文案", ok,
                  $"{(d1 != null ? d1.ChapterTitle : "?")}，{d1?.Lines?.Length ?? 0} 句");
        }

        private void T96_Chapter2Lines()
        {
            var d2 = ChapterEndingLines.Get(ChapterLayout.Chapter.Chapter2);
            bool ok = d2 != null && d2.Lines != null && d2.Lines.Length >= 3
                      && d2.ChapterTitle.Contains("第二章");

            Check("T96 第二章结局文案", ok,
                  $"{(d2 != null ? d2.ChapterTitle : "?")}，{d2?.Lines?.Length ?? 0} 句");
        }

        private void T97_EndingFollowsChapter()
        {
            var ending = FindFirstObjectByType<ChapterEndingSystem>();
            if (ending == null) { Fail("T97 结局随章节切换", "无结局系统"); return; }

            // 当前应该是第一章
            bool isCh1 = ending.chapterTitle.Contains("第一章");

            Check("T97 结局系统已装载章节文案", isCh1,
                  $"当前标题={ending.chapterTitle} 触发点={ending.triggerCenter}");
        }

        private void T98_ChapterSelectPanel()
        {
            var panel = new ChapterSelectPanel();
            bool hasTwo = panel.Chapters.Count == 2;

            panel.Select(ChapterLayout.Chapter.Chapter2);
            bool selected = panel.Selected == ChapterLayout.Chapter.Chapter2;

            Check("T98 章节选择面板可用", hasTwo && selected,
                  $"章节数={panel.Chapters.Count} 选中={panel.Selected}");
        }

        private void T99_ChapterSwitchPersists()
        {
            var prev = ChapterLayout.Current;

            ChapterLayout.Current = ChapterLayout.Chapter.Chapter2;
            bool switched = ChapterLayout.Current == ChapterLayout.Chapter.Chapter2;

            // 读回（模拟新一次访问）
            bool persisted = PlayerPrefs.GetInt("game.currentChapter", 1) == 2;

            ChapterLayout.Current = prev;

            Check("T99 章节切换可持久化", switched && persisted,
                  $"切换={switched} 持久化={persisted}");
        }

        private void T100_BothChaptersPlayable()
        {
            var panel = new ChapterSelectPanel();
            bool ch1 = panel.IsPlayable(ChapterLayout.Chapter.Chapter1);
            bool ch2 = panel.IsPlayable(ChapterLayout.Chapter.Chapter2);

            // 两章的房间数据都要能取到且非空
            var r1 = ChapterLayout.GetRooms(ChapterLayout.Chapter.Chapter1);
            var r2 = ChapterLayout.GetRooms(ChapterLayout.Chapter.Chapter2);

            bool ok = ch1 && ch2 && r1.Count > 0 && r2.Count > 0;

            Check("T100 两章都可玩", ok,
                  $"第一章={r1.Count}房间 第二章={r2.Count}房间");
        }

        // ────────────────────────── 工具 ──────────────────────────

        private void Check(string name, bool ok, string detail = "")
        {
            if (ok) { _pass++; _log.AppendLine($"[通过] {name} {detail}"); }
            else { _fail++; _log.AppendLine($"[失败] {name} {detail}"); }
        }

        private void Fail(string name, string detail)
        {
            _fail++;
            _log.AppendLine($"[失败] {name} {detail}");
        }

        private static void WriteReport(string text)
        {
            try
            {
                string dir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports"));
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "S14_AutoTestResult.txt"), text, new UTF8Encoding(false));
            }
            catch { }
        }
    }
}