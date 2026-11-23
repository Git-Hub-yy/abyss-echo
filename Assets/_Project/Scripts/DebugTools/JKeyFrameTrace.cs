using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
namespace AbyssEcho.DebugTools {
    public class JKeyFrameTrace : MonoBehaviour {
        private readonly StringBuilder _log = new StringBuilder();
        private int _frameCount;

        private void Start() => StartCoroutine(Run());

        private IEnumerator Run() {
            _log.AppendLine("══════ J 键帧级追踪 ══════");
            var ui = FindFirstObjectByType<AbyssEcho.UI.NarrativeUI>();
            var input = FindFirstObjectByType<AbyssEcho.Player.PlayerInputReader>();
            if (ui == null) { _log.AppendLine("❌"); Dump(); yield break; }

            var t = typeof(AbyssEcho.UI.NarrativeUI);
            var BF = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            var journalOpenF = t.GetField("_journalOpen", BF);
            var openedFrameF = t.GetField("_openedFrame", BF);
            var lastJF = t.GetField("_lastJournalFrame", BF);
            var toggleM = t.GetMethod("ToggleJournal", BF);
            var closeM = t.GetMethod("Close", BF);

            closeM?.Invoke(ui, null);
            yield return null;

            // 打开
            toggleM.Invoke(ui, null);
            _log.AppendLine($"打开: _journalOpen={journalOpenF.GetValue(ui)} _openedFrame={openedFrameF.GetValue(ui)}");

            // 逐帧追踪 60 帧，看 Update 的守卫条件
            _log.AppendLine();
            _log.AppendLine("帧数   frameCount  openedFrame  lastJFrame  守卫168(<=2)?  守卫156(==)?");
            for (int i = 0; i < 8; i++) {
                yield return null;
                int fc = Time.frameCount;
                int of = (int)openedFrameF.GetValue(ui);
                int lf = (int)lastJF.GetValue(ui);
                bool g168 = (fc - of) <= 2;
                bool g156 = (fc == lf);
                _log.AppendLine($"{i+1,-6} {fc,-11} {of,-12} {lf,-12} {g168,-14} {g156}");
            }

            // 关键：检查 _journalOpen 在这期间是否被意外重置
            _log.AppendLine();
            _log.AppendLine($"8帧后 _journalOpen = {journalOpenF.GetValue(ui)}");

            // 现在模拟第二次按 J（直接调用 ToggleJournal 模拟 Update 会做的事）
            _log.AppendLine();
            _log.AppendLine("──── 模拟第二次按 J（走 Update 的分支）────");
            int fcNow = Time.frameCount;
            int ofNow = (int)openedFrameF.GetValue(ui);
            _log.AppendLine($"  frameCount={fcNow} _openedFrame={ofNow} → (fc-of)={fcNow-ofNow} > 2 ? {(fcNow-ofNow)>2}");

            if ((bool)journalOpenF.GetValue(ui) && (fcNow - ofNow) > 2) {
                // Update 第170行会做这个
                if (toggleM != null) toggleM.Invoke(ui, null);   // ToggleJournal 在 IsOpen 时会关
                _log.AppendLine($"  调用后 _journalOpen = {journalOpenF.GetValue(ui)}");
            }

            // 检查 ApplyInputLock 是否重新锁死了光标
            _log.AppendLine();
            _log.AppendLine("──── 光标与输入状态 ────");
            _log.AppendLine($"  Cursor.lockState={Cursor.lockState} visible={Cursor.visible}");
            if (input != null) _log.AppendLine($"  InputEnabled={input.InputEnabled}");

            // 检查 NarrativeUI.enabled
            _log.AppendLine($"  NarrativeUI.enabled={ui.enabled}");

            Dump();
        }

        private void Dump() {
            string text = _log.ToString();
            Debug.Log("<b>[JKeyFrameTrace]</b>\n" + text);
            try {
                Directory.CreateDirectory(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports");
                File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\JKeyFrameTrace.txt", text, new UTF8Encoding(false));
            } catch { }
            if (!Application.isEditor) Application.Quit(0);
        }
    }
}