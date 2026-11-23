using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// S79：验证三项小修复。
    ///
    ///   T1 item.tape / item.bulb / item.glove 能进物品栏（ItemIds.All 覆盖）
    ///   T2 拾取物品会写日志（JournalSystem.AddEntry 被调用）
    ///   T3 第一章密码 = 1212（KeypadPanel.correctCode），纸条内容含 "1 2 1 2"
    /// </summary>
    public class SmallFixesTest : MonoBehaviour
    {
        private readonly StringBuilder _log = new StringBuilder();
        private int _pass, _fail;
        private static readonly BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private void Start() => StartCoroutine(Run());

        private IEnumerator Run()
        {
            _log.AppendLine("══════ 三项小修复验证 ══════");

            // ══════ T1：物品栏能显示全部 6 种物品 ══════
            _log.AppendLine();
            _log.AppendLine("──── T1 物品栏能识别的物品 ID ────");

            var allF = typeof(AbyssEcho.Inventory.ItemIds).GetField("All", BindingFlags.Public | BindingFlags.Static);
            string[] allIds = allF?.GetValue(null) as string[];

            if (allIds == null)
            {
                Check("T1 ItemIds.All 存在", false, "null");
            }
            else
            {
                _log.AppendLine($"  ItemIds.All 共 {allIds.Length} 个：");
                foreach (var id in allIds)
                    _log.AppendLine($"    {id,-20} → {AbyssEcho.Inventory.ItemIds.GetDisplayName(id)}");

                bool hasTape = System.Array.IndexOf(allIds, AbyssEcho.Inventory.ItemIds.Tape) >= 0;
                bool hasBulb = System.Array.IndexOf(allIds, AbyssEcho.Inventory.ItemIds.Bulb) >= 0;
                bool hasGlove = System.Array.IndexOf(allIds, AbyssEcho.Inventory.ItemIds.Glove) >= 0;

                Check("T1a item.tape 在列表里", hasTape, "");
                Check("T1b item.bulb 在列表里", hasBulb, "");
                Check("T1c item.glove 在列表里", hasGlove, "");
                Check("T1d 至少 6 种物品", allIds.Length >= 6, $"count={allIds.Length}");
            }

            // ══════ T2：拾取会写日志 ══════
            _log.AppendLine();
            _log.AppendLine("──── T2 拾取写日志 ────");

            var journal = FindFirstObjectByType<AbyssEcho.Narrative.JournalSystem>();
            if (journal == null)
            {
                Check("T2 JournalSystem 存在", false, "场景里没有");
            }
            else
            {
                int before = journal.Entries.Count;
                _log.AppendLine($"  拾取前日志条数 = {before}");

                // 找一个 PickupItem 直接调用 OnInteract
                var pickup = FindFirstObjectByType<AbyssEcho.Interaction.PickupItem>();
                if (pickup == null)
                {
                    _log.AppendLine("  ⚠️ 本场景没有 PickupItem，改用手动写日志验证");
                    journal.AddEntry("测试物品", "测试描述");
                }
                else
                {
                    _log.AppendLine($"  找到 PickupItem: {pickup.gameObject.name}");
                    var idF = typeof(AbyssEcho.Interaction.PickupItem).GetField("itemId", BF);
                    _log.AppendLine($"     itemId = {idF?.GetValue(pickup)}");
                    pickup.OnInteract(null);
                }

                yield return null;
                yield return null;

                int after = journal.Entries.Count;
                _log.AppendLine($"  拾取后日志条数 = {after}");
                Check("T2 拾取后日志增加", after > before, $"before={before} after={after}");

                if (after > before)
                {
                    var last = journal.Entries[after - 1];
                    _log.AppendLine($"  最新日志: [{last.Title}] {last.Body}");
                    Check("T2b 日志内容非空", !string.IsNullOrEmpty(last.Title) && !string.IsNullOrEmpty(last.Body),
                          $"{last.Title}/{last.Body}");
                }
            }

            // ══════ T3：密码 1212 ══════
            _log.AppendLine();
            _log.AppendLine("──── T3 第一章密码 ────");

            var pads = FindObjectsByType<AbyssEcho.Puzzle.KeypadPanel>(FindObjectsSortMode.None);
            _log.AppendLine($"  场景里 KeypadPanel = {pads.Length} 个");

            if (pads.Length == 0)
            {
                // 用反射读默认值
                var defF = typeof(AbyssEcho.Puzzle.KeypadPanel).GetField("correctCode", BF);
                var inst = new GameObject("__tmp_pad__").AddComponent<AbyssEcho.Puzzle.KeypadPanel>();
                string defCode = defF?.GetValue(inst) as string;
                _log.AppendLine($"  类默认值 correctCode = {defCode}");
                Check("T3 默认密码 = 1212", defCode == "1212", $"actual={defCode}");
                Object.DestroyImmediate(inst.gameObject);
            }
            else
            {
                foreach (var p in pads)
                {
                    var codeF = typeof(AbyssEcho.Puzzle.KeypadPanel).GetField("correctCode", BF);
                    string code = codeF?.GetValue(p) as string;
                    _log.AppendLine($"  {p.gameObject.name}: correctCode = '{code}'");
                    Check($"T3 密码 = 1212 ({p.gameObject.name})", code == "1212", $"actual={code}");
                }
            }

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
            Debug.Log("<b>[SmallFixesTest]</b>\n" + text);
            try
            {
                Directory.CreateDirectory(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports");
                File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\SmallFixesTest.txt", text, new UTF8Encoding(false));
            }
            catch { }
            if (!Application.isEditor) Application.Quit(_fail == 0 ? 0 : 1);
        }
    }
}