using System.Collections;
using System.IO;
using System.Text;
using AbyssEcho.Interaction;
using AbyssEcho.Inventory;
using AbyssEcho.Narrative;
using AbyssEcho.Puzzle;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// S4 第二部分自动测试：谜题 2（门禁密码）+ 叙事系统。
    ///
    /// 通过标准：全部断言通过 → 退出码 0
    /// </summary>
    public class AutoTest_S4B : MonoBehaviour
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
            _log.AppendLine("ABYSS ECHO · S4 第二部分自动测试（门禁谜题 + 叙事）");
            _log.AppendLine("=====================================");

            yield return new WaitForSeconds(0.5f);

            T13_JournalExists();
            T14_JournalAddDedup();
            T15_NoteExists();
            T16_NoteReadWorks();
            T17_KeypadExists();
            T18_KeypadWrongCode();
            T19_KeypadCorrectCode();
            T20_KeypadUnlocksDoor();
            T21_NarrativeUIExists();
            T22_KeypadUIExists();
            T23_TotalInteractables();
            T24_NoDuplicateObjects();

            _log.AppendLine("=====================================");
            _log.AppendLine($"结果：通过 {_pass}，失败 {_fail}");

            string text = _log.ToString();
            Debug.Log("<b>[AutoTest_S4B]</b>\n" + text);
            WriteReport(text);

            yield return new WaitForSeconds(0.2f);
            if (!Application.isEditor) Application.Quit(_fail == 0 ? 0 : 1);
        }

        // ────────────────────────── 测试用例 ──────────────────────────

        private void T13_JournalExists()
        {
            Check("T13 日志系统存在", JournalSystem.Instance != null);
        }

        private void T14_JournalAddDedup()
        {
            var j = JournalSystem.Instance;
            if (j == null) { Fail("T14 日志去重", "无日志系统"); return; }

            int before = j.Entries.Count;
            j.AddEntry("测试条目", "内容A");
            int afterFirst = j.Entries.Count;
            j.AddEntry("测试条目", "内容B");      // 同标题应被忽略
            int afterSecond = j.Entries.Count;

            bool added = afterFirst == before + 1;
            bool deduped = afterSecond == afterFirst;
            Check("T14 日志去重", added && deduped, $"added={added} deduped={deduped}");
        }

        private void T15_NoteExists()
        {
            var notes = FindObjectsByType<ReadableNote>(FindObjectsSortMode.None);
            Check("T15 线索纸条已放置", notes.Length >= 1, $"找到 {notes.Length} 个");
        }

        private void T16_NoteReadWorks()
        {
            var notes = FindObjectsByType<ReadableNote>(FindObjectsSortMode.None);
            if (notes.Length == 0) { Fail("T16 纸条可阅读", "无纸条"); return; }

            var note = notes[0];
            bool bodyNotEmpty = !string.IsNullOrWhiteSpace(note.body);
            // 纸条里写成 "7 4 1 2"（带空格便于阅读），因此先去掉空格再比对
            string normalized = System.Text.RegularExpressions.Regex.Replace(note.body, @"\s", "");
            bool hasCode = normalized.Contains("1212");

            // 触发阅读（会写入 Journal）
            note.OnInteract(null);

            var j = JournalSystem.Instance;
            bool journaled = j != null && j.Has(note.journalTitle);

            Check("T16 纸条可阅读且含密码", bodyNotEmpty && hasCode && journaled,
                  $"bodyNotEmpty={bodyNotEmpty} hasCode={hasCode} journaled={journaled}");
        }

        private void T17_KeypadExists()
        {
            var pads = FindObjectsByType<KeypadPanel>(FindObjectsSortMode.None);
            Check("T17 门禁键盘已放置", pads.Length >= 1, $"找到 {pads.Length} 个");
        }

        private void T18_KeypadWrongCode()
        {
            var pads = FindObjectsByType<KeypadPanel>(FindObjectsSortMode.None);
            if (pads.Length == 0) { Fail("T18 错误密码被拒绝", "无键盘"); return; }

            var pad = pads[0];
            bool wrongRejected = !pad.Submit("0000") && !pad.IsUnlocked;
            Check("T18 错误密码被拒绝", wrongRejected, $"IsUnlocked={pad.IsUnlocked}");
        }

        private void T19_KeypadCorrectCode()
        {
            var pads = FindObjectsByType<KeypadPanel>(FindObjectsSortMode.None);
            if (pads.Length == 0) { Fail("T19 正确密码被接受", "无键盘"); return; }

            var pad = pads[0];
            bool correctAccepted = pad.Submit(pad.correctCode) && pad.IsUnlocked;
            Check("T19 正确密码被接受", correctAccepted, $"code={pad.correctCode} IsUnlocked={pad.IsUnlocked}");
        }

        private void T20_KeypadUnlocksDoor()
        {
            var pads = FindObjectsByType<KeypadPanel>(FindObjectsSortMode.None);
            if (pads.Length == 0) { Fail("T20 密码正确后门解锁", "无键盘"); return; }

            var pad = pads[0];
            if (pad.doorsToUnlock == null || pad.doorsToUnlock.Length == 0)
            {
                Fail("T20 密码正确后门解锁", "键盘未关联任何门");
                return;
            }

            bool allUnlocked = true;
            foreach (var d in pad.doorsToUnlock)
                if (d != null && d.locked) allUnlocked = false;

            Check("T20 密码正确后门解锁", allUnlocked, $"关联 {pad.doorsToUnlock.Length} 扇门");
        }

        private void T21_NarrativeUIExists()
        {
            var ui = FindFirstObjectByType<AbyssEcho.UI.NarrativeUI>();
            Check("T21 叙事 UI 已挂载", ui != null);
        }

        private void T22_KeypadUIExists()
        {
            var ui = FindFirstObjectByType<AbyssEcho.UI.KeypadUI>();
            Check("T22 键盘 UI 已挂载", ui != null);
        }

        private void T23_TotalInteractables()
        {
            var all = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            int n = 0;
            foreach (var m in all) if (m is IInteractable) n++;
            Check("T23 可交互物总数 ≥ 6", n >= 6, $"找到 {n} 个");
        }

        private void T24_NoDuplicateObjects()
        {
            // 幂等性检查：确保没有重复创建同名根节点
            var roots = FindObjectsByType<Transform>(FindObjectsSortMode.None);
            int rootsFound = 0;
            foreach (var t in roots)
                if (t.name == "--- INTERACTABLES ---") rootsFound++;

            Check("T24 交互物根节点唯一", rootsFound == 1, $"找到 {rootsFound} 个根节点");
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
                File.WriteAllText(Path.Combine(dir, "S4B_AutoTestResult.txt"), text, new UTF8Encoding(false));
            }
            catch { }
        }
    }
}