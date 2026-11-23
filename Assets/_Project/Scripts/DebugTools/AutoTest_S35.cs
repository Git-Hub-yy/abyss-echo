using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// S35 供奉台与三结局测试。
    ///
    ///   T1 供奉台存在
    ///   T2 三个拾取物都在场景里
    ///   T3 空手时供奉台不消耗、不报错
    ///   T4 有录音带 → 供奉 → 结局 A
    ///   T5 有保险丝 → 供奉 → 结局 C
    ///   T6 什么都没供奉 → 结局 B
    ///   T7 三个结局文案都存在且不同
    /// </summary>
    public class AutoTest_S35 : MonoBehaviour
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
            _log.AppendLine("ABYSS ECHO · S35 供奉台与三结局测试");
            _log.AppendLine("=====================================");

            AbyssEcho.Save.SaveSystem.Delete();
            yield return new WaitForSeconds(0.8f);

            var altars = FindObjectsByType<AbyssEcho.Puzzle.OfferingAltar>(FindObjectsSortMode.None);
            Check("T1 场景中有供奉台", altars.Length >= 1, $"共 {altars.Length} 个");
            if (altars.Length == 0) { Finish(); yield break; }

            var altar = altars[0];
            var inv = AbyssEcho.Inventory.PlayerInventory.Instance;
            Check("T1b 玩家物品栏就绪", inv != null, inv != null ? "ok" : "❌ null");
            if (inv == null) { Finish(); yield break; }

            // ── T2 三个拾取物 ──
            var pickups = FindObjectsByType<AbyssEcho.Interaction.PickupItem>(FindObjectsSortMode.None);
            bool hasTape = false, hasBulb = false, hasFuse = false;
            foreach (var p in pickups)
            {
                if (p.itemId == AbyssEcho.Inventory.ItemIds.Tape) hasTape = true;
                if (p.itemId == AbyssEcho.Inventory.ItemIds.Bulb) hasBulb = true;
                if (p.itemId == AbyssEcho.Inventory.ItemIds.Fuse) hasFuse = true;
            }
            Check("T2 三个拾取物都存在", hasTape && hasBulb && hasFuse,
                  $"录音带={hasTape} 灯泡={hasBulb} 保险丝={hasFuse}（共 {pickups.Length} 个拾取物）");

            // ── T3 空手供奉 ──
            inv.Clear();
            altar.ResetAltar();
            yield return null;
            altar.OnInteract(null);
            yield return null;
            Check("T3 空手时不会误触发", !altar.HasOffered,
                  $"HasOffered={altar.HasOffered}");

            // ── T4 录音带 → 结局 A ──
            inv.Clear();
            altar.ResetAltar();
            inv.Add(AbyssEcho.Inventory.ItemIds.Tape);
            yield return null;
            altar.OnInteract(null);
            yield return null;

            string endingA = altar.GetEndingId();
            bool tapeConsumed = !inv.Has(AbyssEcho.Inventory.ItemIds.Tape);
            Check("T4 供录音带 → 结局 A", endingA == "A" && tapeConsumed,
                  $"结局={endingA} 录音带已消耗={tapeConsumed}");

            // ── T5 保险丝 → 结局 C ──
            inv.Clear();
            altar.ResetAltar();
            inv.Add(AbyssEcho.Inventory.ItemIds.Fuse);
            yield return null;
            altar.OnInteract(null);
            yield return null;

            string endingC = altar.GetEndingId();
            bool fuseConsumed = !inv.Has(AbyssEcho.Inventory.ItemIds.Fuse);
            Check("T5 供保险丝 → 结局 C", endingC == "C" && fuseConsumed,
                  $"结局={endingC} 保险丝已消耗={fuseConsumed}");

            // ── T6 不供奉 → 结局 B ──
            inv.Clear();
            altar.ResetAltar();
            yield return null;
            string endingB = altar.GetEndingId();
            Check("T6 不供奉 → 结局 B", endingB == "B", $"结局={endingB}");

            // ── T7 三个结局文案 ──
            var ch3 = AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter3;
            var defA = AbyssEcho.Narrative.ChapterEndingLines.Get(ch3, "A");
            var defB = AbyssEcho.Narrative.ChapterEndingLines.Get(ch3, "B");
            var defC = AbyssEcho.Narrative.ChapterEndingLines.Get(ch3, "C");

            bool allExist = defA != null && defB != null && defC != null;
            bool allHaveLines = allExist &&
                                defA.Lines != null && defA.Lines.Length >= 5 &&
                                defB.Lines != null && defB.Lines.Length >= 5 &&
                                defC.Lines != null && defC.Lines.Length >= 5;

            Check("T7 三个结局文案都存在", allHaveLines,
                  allExist ? $"A={defA.Lines?.Length}行 B={defB.Lines?.Length}行 C={defC.Lines?.Length}行"
                           : "❌ 有分支缺失");

            // 三个结局内容必须不同（防止"三个结局其实一样"）
            if (allHaveLines)
            {
                bool differ = defA.Lines[0] != defB.Lines[0] && defB.Lines[0] != defC.Lines[0] && defA.Lines[0] != defC.Lines[0];
                Check("T7b 三个结局内容不同", differ,
                      $"A[0]={defA.Lines[0]} / B[0]={defB.Lines[0]} / C[0]={defC.Lines[0]}");
            }

            // ── T8 向后兼容：第一、二章不受影响 ──
            var ch1Def = AbyssEcho.Narrative.ChapterEndingLines.Get(
                AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter1, "A");
            var ch2Def = AbyssEcho.Narrative.ChapterEndingLines.Get(
                AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter2, "C");

            Check("T8 一二章结局未受影响",
                  ch1Def != null && ch1Def.Lines != null && ch1Def.Lines.Length == 5 &&
                  ch2Def != null && ch2Def.Lines != null && ch2Def.Lines.Length == 6,
                  $"第一章={ch1Def?.Lines?.Length}行 第二章={ch2Def?.Lines?.Length}行");

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
            Debug.Log("<b>[S35Test]</b>\n" + text);
            try
            {
                string dir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports"));
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "S35_AutoTestResult.txt"), text, new UTF8Encoding(false));
            }
            catch { }
            if (!Application.isEditor) Application.Quit(_fail == 0 ? 0 : 1);
        }
    }
}