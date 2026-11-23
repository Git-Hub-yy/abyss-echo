using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
namespace AbyssEcho.DebugTools {
    public class JKeyRealTest : MonoBehaviour {
        private readonly StringBuilder _log = new StringBuilder();
        private int _pass, _fail;

        private void Start() => StartCoroutine(Run());

        private IEnumerator Run() {
            _log.AppendLine("══════ J 键日志开关 —— 按键模拟测试 ══════");

            var ui = FindFirstObjectByType<AbyssEcho.UI.NarrativeUI>();
            var input = FindFirstObjectByType<AbyssEcho.Player.PlayerInputReader>();
            if (ui == null) { _log.AppendLine("❌ 无 NarrativeUI"); Dump(); yield break; }
            _log.AppendLine($"NarrativeUI @ {ui.gameObject.name}  enabled={ui.enabled}");
            _log.AppendLine($"InputReader = {(input != null ? input.gameObject.name : "NULL")}");

            var t = typeof(AbyssEcho.UI.NarrativeUI);
            var BF = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            var journalOpenF = t.GetField("_journalOpen", BF);
            var openedFrameF = t.GetField("_openedFrame", BF);
            var lastJF = t.GetField("_lastJournalFrame", BF);
            var updateM = t.GetMethod("Update", BF);
            var closeM = t.GetMethod("Close", BF);

            // 检查 Update 是否私有且有反射可调用
            _log.AppendLine($"Update 方法: {(updateM != null ? "找到" : "❌未找到")}");
            _log.AppendLine($"_journalOpen: {(journalOpenF != null ? "找到" : "❌")}");
            _log.AppendLine();

            closeM?.Invoke(ui, null);
            yield return null;

            // 模拟：手动调用 ToggleJournal 看能否关闭
            var toggleM = t.GetMethod("ToggleJournal", BF);
            _log.AppendLine("──── 直接测 ToggleJournal ────");

            toggleM.Invoke(ui, null);
            _log.AppendLine($"  第1次调用后 _journalOpen = {journalOpenF.GetValue(ui)}");
            yield return null; yield return null; yield return null;

            _log.AppendLine($"  3帧后 frameCount={Time.frameCount} _openedFrame={openedFrameF.GetValue(ui)}");

            toggleM.Invoke(ui, null);
            bool closed = !(bool)journalOpenF.GetValue(ui);
            _log.AppendLine($"  第2次调用后 _journalOpen = {journalOpenF.GetValue(ui)}");
            Check("ToggleJournal 能来回切换", closed, $"closed={closed}");

            // 关键检查：_lastJournalFrame 是否卡住
            _log.AppendLine();
            _log.AppendLine("──── 检查 _lastJournalFrame 是否会卡住 ────");
            int lfBefore = (int)lastJF.GetValue(ui);
            _log.AppendLine($"  _lastJournalFrame = {lfBefore}  frameCount = {Time.frameCount}");
            yield return null;
            yield return null;
            int lfAfter = (int)lastJF.GetValue(ui);
            _log.AppendLine($"  2帧后 _lastJournalFrame = {lfAfter}");
            _log.AppendLine($"  → Update 第156行 `if (frameCount == _lastJournalFrame) return;` " +
                            $"当前帧 {Time.frameCount} vs {lfAfter} → {(Time.frameCount == lfAfter ? "❌ 会提前return！" : "✅ 不会")}");

            // 检查 inputEnabled
            _log.AppendLine();
            _log.AppendLine("──── 输入状态 ────");
            if (input != null) {
                _log.AppendLine($"  InputEnabled = {input.InputEnabled}");
                var jeF = typeof(AbyssEcho.Player.PlayerInputReader).GetProperty("JournalPressed");
                _log.AppendLine($"  JournalPressed = {(jeF != null ? jeF.GetValue(input) : "?")}");
            }

            // 检查 NarrativeUI 是否被 Update 的 return 卡住
            _log.AppendLine();
            _log.AppendLine("──── 检查 IsOpen 与 Update 守卫的关系 ────");
            closeM?.Invoke(ui, null);
            yield return null;
            toggleM.Invoke(ui, null);   // 打开
            yield return null;

            bool isOpen = (bool)journalOpenF.GetValue(ui);
            int lfOpen = (int)lastJF.GetValue(ui);
            _log.AppendLine($"  打开后: IsOpen={isOpen} _openedFrame={openedFrameF.GetValue(ui)} _lastJournalFrame={lfOpen}");

            // 等足够多帧
            for (int i = 0; i < 5; i++) yield return null;
            _log.AppendLine($"  5帧后: frameCount={Time.frameCount} _lastJournalFrame={lastJF.GetValue(ui)}");

            bool canClose = Time.frameCount != (int)lastJF.GetValue(ui) &&
                            (Time.frameCount - (int)openedFrameF.GetValue(ui)) > 2;
            _log.AppendLine($"  → 能否响应关闭键: {(canClose ? "✅ 能" : "❌ 不能")}");
            Check("打开后能响应关闭键", canClose, $"frame={Time.frameCount} last={lastJF.GetValue(ui)} opened={openedFrameF.GetValue(ui)}");

            Dump();
        }

        private void Check(string name, bool ok, string detail) {
            if (ok) { _pass++; _log.AppendLine($"[通过] {name}  {detail}"); }
            else { _fail++; _log.AppendLine($"[失败] {name}  {detail}"); }
        }

        private void Dump() {
            _log.AppendLine();
            _log.AppendLine($"通过 {_pass}，失败 {_fail}");
            string text = _log.ToString();
            Debug.Log("<b>[JKeyRealTest]</b>\n" + text);
            try {
                Directory.CreateDirectory(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports");
                File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\JKeyRealTest.txt", text, new UTF8Encoding(false));
            } catch { }
            if (!Application.isEditor) Application.Quit(_fail == 0 ? 0 : 1);
        }
    }
}