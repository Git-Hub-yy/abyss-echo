using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// S43 第五章终局选择测试。
    ///
    ///   T1 撤离控制台存在
    ///   T2 初始未做选择
    ///   T3 选 A → 结局 A
    ///   T4 选 B → 结局 B
    ///   T5 选 C → 结局 C
    ///   T6 三个结局文案都存在且不同
    ///   T7 已选择后不再接受新选择
    /// </summary>
    public class AutoTest_S43_Ch5 : MonoBehaviour
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
            _log.AppendLine("ABYSS ECHO · S43 第五章终局选择测试");
            _log.AppendLine("=====================================");

            AbyssEcho.Save.SaveSystem.Delete();
            yield return new WaitForSeconds(0.8f);

            var consoles = FindObjectsByType<AbyssEcho.Puzzle.EvacuationConsole>(FindObjectsSortMode.None);
            Check("T1 场景中有撤离控制台", consoles.Length >= 1, $"共 {consoles.Length} 个");
            if (consoles.Length == 0) { Finish(); yield break; }

            var console = consoles[0];

            // ── T2 初始未选择 ──
            console.ResetConsole();
            yield return null;
            Check("T2 初始未做选择", !console.HasChosen,
                  $"ChosenEnding='{console.ChosenEnding}'");

            // ── T3/T4/T5 三个选项 ──
            string[] ids = { "A", "B", "C" };
            string[] names = { "只带自己走", "带走记录", "留下封门" };

            for (int i = 0; i < 3; i++)
            {
                console.ResetConsole();
                yield return null;

                console.Choose(ids[i]);
                yield return null;

                bool ok = console.ChosenEnding == ids[i] && console.HasChosen;
                Check($"T{3 + i} 选「{names[i]}」→ 结局 {ids[i]}", ok,
                      $"ChosenEnding={console.ChosenEnding}");
            }

            // ── T6 三个结局文案 ──
            var ch5 = AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter5;
            var defA = AbyssEcho.Narrative.ChapterEndingLines.Get(ch5, "A");
            var defB = AbyssEcho.Narrative.ChapterEndingLines.Get(ch5, "B");
            var defC = AbyssEcho.Narrative.ChapterEndingLines.Get(ch5, "C");

            bool allExist = defA != null && defB != null && defC != null
                            && defA.Lines != null && defA.Lines.Length >= 5
                            && defB.Lines != null && defB.Lines.Length >= 5
                            && defC.Lines != null && defC.Lines.Length >= 5;

            Check("T6 三个结局文案都存在", allExist,
                  allExist ? $"A={defA.Lines.Length}行 B={defB.Lines.Length}行 C={defC.Lines.Length}行"
                           : "❌ 有分支缺失");

            if (allExist)
            {
                bool differ = defA.Lines[0] != defB.Lines[0]
                              && defB.Lines[0] != defC.Lines[0]
                              && defA.Lines[0] != defC.Lines[0];
                Check("T6b 三个结局内容不同", differ,
                      $"A[0]={defA.Lines[0]} / B[0]={defB.Lines[0]} / C[0]={defC.Lines[0]}");
            }

            // ── T7 已选择后不可更改 ──
            console.ResetConsole();
            yield return null;
            console.Choose("A");
            yield return null;
            string first = console.ChosenEnding;

            console.Choose("C");   // 试图更改
            yield return null;

            Check("T7 已选择后不可更改", console.ChosenEnding == first,
                  $"第一次={first} 尝试改成C 后={console.ChosenEnding}");

            Finish();
        }

        private void Check(string name, bool ok, string detail = "")
        {
            if (ok) { _pass++; _log.AppendLine($"[通过] {name} {detail}"); }
            else { _fail++; _log.AppendLine($"[失败] {name} {detail}"); }
        }

        private void Finish()
        {
            _log.AppendLine("=====================================");
            _log.AppendLine($"结果：通过 {_pass}，失败 {_fail}");
            string text = _log.ToString();
            Debug.Log("<b>[S43Ch5Test]</b>\n" + text);
            try
            {
                string dir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports"));
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "S43Ch5_AutoTestResult.txt"), text, new UTF8Encoding(false));
            }
            catch { }
            if (!Application.isEditor) Application.Quit(_fail == 0 ? 0 : 1);
        }
    }
}