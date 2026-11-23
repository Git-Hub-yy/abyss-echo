using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// S64：第三章三结局**真实性验证**。
    ///
    /// ══════════════════ 要验证什么 ══════════════════
    ///
    ///   玩家反馈："我明明放了保险丝，结局第一句却是'你什么也没有放'"
    ///
    ///   怀疑根因：
    ///     ChapterEndingSystem.Awake() 里调用 ApplyChapterConfig()，
    ///     那一刻调用 altar.GetEndingId()。
    ///     但场景刚加载时 HasOffered = false → 返回 "B"（离开）。
    ///     玩家之后才放保险丝，改的是 HasOffered，
    ///     而 endingLines 早就定死成 B 的文案了。
    ///
    ///   本测试要证明：
    ///     T1 场景刚加载时 endingLines 是哪个结局
    ///     T2 放保险丝后 altar.GetEndingId() 变成什么
    ///     T3 放保险丝后 endingLines **有没有跟着变**
    ///     T4 A/B/C 三种情况分别实际播放出什么文案
    /// </summary>
    public class Ch3EndingTruthTest : MonoBehaviour
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
            // ⚠️ 测试时序：Awake 已按 PlayerPrefs 里的旧章节加载过文案，
            //   这里强制切到 Chapter3 并重新应用配置。
            AbyssEcho.LevelGen.ChapterLayout.Current =
                AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter3;
            var ending0 = FindFirstObjectByType<AbyssEcho.Narrative.ChapterEndingSystem>();
            ending0?.GetType().GetMethod("ApplyChapterConfig", BF)?.Invoke(ending0, null);
            yield return new WaitForSeconds(0.5f);

            _log.AppendLine("══════ S64 第三章三结局真实性验证 ══════");

            var ending = FindFirstObjectByType<AbyssEcho.Narrative.ChapterEndingSystem>();
            var altar = FindFirstObjectByType<AbyssEcho.Puzzle.OfferingAltar>();

            if (ending == null) { _log.AppendLine("❌ 找不到 ChapterEndingSystem"); Dump(); yield break; }
            if (altar == null) { _log.AppendLine("❌ 找不到 OfferingAltar"); Dump(); yield break; }

            var t = typeof(AbyssEcho.Narrative.ChapterEndingSystem);
            var linesF = t.GetField("endingLines", BF);
            var footerF = t.GetField("chapterFooter", BF);

            // ══ T1 场景刚加载时的状态 ══
            _log.AppendLine();
            _log.AppendLine("──── T1 场景刚加载（Awake 已跑完）────");
            _log.AppendLine($"  altar.HasOffered = {altar.HasOffered}");
            _log.AppendLine($"  altar.OfferedItemId = \"{altar.OfferedItemId}\"");
            _log.AppendLine($"  altar.GetEndingId() = \"{altar.GetEndingId()}\"");
            _log.AppendLine($"  ChapterEndingSystem.footer = \"{footerF?.GetValue(ending)}\"");
            var lines1 = linesF?.GetValue(ending) as string[];
            _log.AppendLine($"  endingLines[0] = \"{(lines1 != null && lines1.Length > 0 ? lines1[0] : "空")}\"");
            _log.AppendLine($"  endingLines 长度 = {lines1?.Length ?? 0}");

            Check("T1 初始结局是 B（离开）", altar.GetEndingId() == "B",
                  $"GetEndingId={altar.GetEndingId()}");

            // ══ T2 模拟玩家放保险丝 ══
            _log.AppendLine();
            _log.AppendLine("──── T2 模拟玩家放保险丝（结局 C）────");

            var inv = AbyssEcho.Inventory.PlayerInventory.Instance;
            if (inv == null) { _log.AppendLine("❌ 找不到 PlayerInventory"); Dump(); yield break; }

            // 给玩家保险丝
            inv.Add("item.fuse");
            _log.AppendLine($"  玩家背包有 fuse = {inv.Has("item.fuse")}");

            // 调用供奉
            altar.Offer("item.fuse");
            yield return new WaitForSeconds(0.3f);

            string id2 = altar.GetEndingId();
            _log.AppendLine($"  供奉后 HasOffered = {altar.HasOffered}");
            _log.AppendLine($"  供奉后 OfferedItemId = \"{altar.OfferedItemId}\"");
            _log.AppendLine($"  供奉后 GetEndingId() = \"{id2}\"");
            Check("T2a 供奉保险丝后 GetEndingId = C", id2 == "C", $"实际={id2}");

            // ══ T3 关键：真正触发结局，看播放什么 ══
            _log.AppendLine();
            _log.AppendLine("──── T3 【关键】真正触发结局，看播放什么 ────");

            // 把玩家传送到触发区中心，让 Update 自己触发 BeginEnding
            var tcF0 = t.GetField("triggerCenter", BF);
            Vector3 tcz = (Vector3)tcF0.GetValue(ending);
            var motor = FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
            var cc0 = motor != null ? motor.GetComponent<CharacterController>() : null;
            if (cc0 != null) cc0.enabled = false;
            motor.transform.position = new Vector3(tcz.x, 0.05f, tcz.z);
            if (cc0 != null) cc0.enabled = true;
            _log.AppendLine($"  玩家已传送到触发点 {tcz}");

            float wait = 0f;
            var phaseF = t.GetField("_phase", BF);
            while (wait < 15f) {
                string ph = phaseF?.GetValue(ending)?.ToString() ?? "";
                if (ph == "Lines" || ph == "Finished") break;
                wait += Time.deltaTime;
                yield return null;
            }
            _log.AppendLine($"  等待 {wait:F1}s 后 _phase = {phaseF?.GetValue(ending)}");

            var lines2 = linesF?.GetValue(ending) as string[];
            _log.AppendLine($"  供奉后 footer = \"{footerF?.GetValue(ending)}\"");
            _log.AppendLine($"  供奉后 endingLines[0] = \"{(lines2 != null && lines2.Length > 0 ? lines2[0] : "空")}\"");
            _log.AppendLine($"  供奉后 endingLines 长度 = {lines2?.Length ?? 0}");

            bool changed = lines1 != null && lines2 != null &&
                           lines1.Length > 0 && lines2.Length > 0 &&
                           lines1[0] != lines2[0];
            _log.AppendLine();
            _log.AppendLine($"  文案是否变化 = {(changed ? "✅ 变了" : "❌ 没变")}");

            if (!changed)
            {
                _log.AppendLine();
                _log.AppendLine("  🔴🔴🔴 确认缺陷 🔴🔴🔴");
                _log.AppendLine("     altar.GetEndingId() 已经变成 C，");
                _log.AppendLine("     但 endingLines 还是场景加载时定下的 B 版本。");
                _log.AppendLine("     玩家看到的结局与他的选择不符。");
                _log.AppendLine();
                _log.AppendLine("     根因：ApplyChapterConfig() 只在 Awake() 里调用一次，");
                _log.AppendLine("           玩家之后的选择不会重新读取。");
            }

            Check("T3 endingLines 跟随玩家选择更新", changed,
                  changed ? "文案已更新" : $"仍是\"{lines1?[0]}\"");

            // ══ T4 三种选择分别得到什么文案 ══
            _log.AppendLine();
            _log.AppendLine("──── T4 A/B/C 三种选择的文案对照 ────");

            string[] ids = { "B", "A", "C" };
            var chapter = AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter3;
            var lt = typeof(AbyssEcho.Narrative.ChapterEndingLines);
            foreach (var id in ids)
            {
                string fieldName = "Chapter3_Ending" + id;
                var fld = lt.GetField(fieldName, System.Reflection.BindingFlags.Public |
                                                  System.Reflection.BindingFlags.Static);
                if (fld == null) { _log.AppendLine($"  [{id}] 找不到字段 {fieldName}"); continue; }
                var def = fld.GetValue(null);
                if (def == null) { _log.AppendLine($"  [{id}] 字段为 null"); continue; }

                var dType = def.GetType();
                var footerProp = dType.GetField("Footer", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                var linesProp = dType.GetField("Lines", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                string footer = footerProp?.GetValue(def) as string ?? "?";
                var ls = linesProp?.GetValue(def) as string[];

                _log.AppendLine($"  [{id}] {footer}  共 {(ls?.Length ?? 0)} 句");
                if (ls != null)
                    for (int i = 0; i < Mathf.Min(3, ls.Length); i++)
                        _log.AppendLine($"        {ls[i]}");
            }

            // ══ T5 触发区 ══
            _log.AppendLine();
            _log.AppendLine("──── T5 结局触发区 ────");
            var tcF = t.GetField("triggerCenter", BF);
            var tsF = t.GetField("triggerSize", BF);
            Vector3 tc = (Vector3)tcF.GetValue(ending);
            Vector3 ts = (Vector3)tsF.GetValue(ending);
            _log.AppendLine($"  触发中心 = {tc}  尺寸 = {ts}");
            _log.AppendLine($"  Z 范围 = [{tc.z - ts.z * 0.5f:F1}, {tc.z + ts.z * 0.5f:F1}]");

            _log.AppendLine();
            Unverified("U1 真人放保险丝后看到的结局文案");

            Dump();
        }

        private void Check(string name, bool ok, string detail)
        {
            if (ok) { _pass++; _log.AppendLine($"[通过] {name}  {detail}"); }
            else { _fail++; _log.AppendLine($"[失败] {name}  {detail}"); }
        }

        private void Unverified(string n) { _unverified++; _log.AppendLine($"[待人工验收] {n}"); }

        private void Dump()
        {
            _log.AppendLine();
            _log.AppendLine("=====================================");
            _log.AppendLine($"通过 {_pass}，失败 {_fail}，待人工验收 {_unverified}");

            string text = _log.ToString();
            Debug.Log("<b>[Ch3EndingTruthTest]</b>\n" + text);

            try
            {
                Directory.CreateDirectory(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports");
                File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Ch3EndingTruthTest.txt",
                                  text, new UTF8Encoding(false));
            }
            catch { }

            if (!Application.isEditor) Application.Quit(_fail == 0 ? 0 : 1);
        }
    }
}