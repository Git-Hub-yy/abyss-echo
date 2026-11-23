using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace AbyssEcho.DebugTools {
    public class JKeySimTest : MonoBehaviour {
        private readonly StringBuilder _log = new StringBuilder();
        private int _pass, _fail;

        private void Start() => StartCoroutine(Run());

        private IEnumerator Run() {
            _log.AppendLine("══════ J 键真实按键模拟 ══════");
            var ui = FindFirstObjectByType<AbyssEcho.UI.NarrativeUI>();
            var input = FindFirstObjectByType<AbyssEcho.Player.PlayerInputReader>();
            if (ui == null) { _log.AppendLine("❌ 无 NarrativeUI"); Dump(); yield break; }

            var t = typeof(AbyssEcho.UI.NarrativeUI);
            var BF = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            var journalOpenF = t.GetField("_journalOpen", BF);
            var openedFrameF = t.GetField("_openedFrame", BF);
            var lastJF = t.GetField("_lastJournalFrame", BF);
            var closeM = t.GetMethod("Close", BF);

            closeM.Invoke(ui, null);
            yield return null;

            var kb = InputSystem.AddDevice<Keyboard>();
            _log.AppendLine($"创建虚拟键盘: {kb.deviceId}");
            _log.AppendLine();

            // ── 按键1：打开 ──
            _log.AppendLine("──── 第1次按 J ────");
            PressJ(kb);
            yield return null;
            yield return null;
            bool open1 = (bool)journalOpenF.GetValue(ui);
            _log.AppendLine($"  _journalOpen = {open1}  _openedFrame = {openedFrameF.GetValue(ui)}  frame={Time.frameCount}");
            ReleaseJ(kb);
            yield return null;

            // 等 5 帧让守卫过期
            for (int i = 0; i < 5; i++) yield return null;
            _log.AppendLine($"  5帧后 frame={Time.frameCount} _openedFrame={openedFrameF.GetValue(ui)} " +
                            $"_lastJournalFrame={lastJF.GetValue(ui)}");

            // ── 按键2：关闭 ──
            _log.AppendLine();
            _log.AppendLine("──── 第2次按 J ────");
            PressJ(kb);
            int fcPress = Time.frameCount;
            yield return null;
            yield return null;
            bool open2 = (bool)journalOpenF.GetValue(ui);
            _log.AppendLine($"  按下帧={fcPress}  _journalOpen = {open2}");
            _log.AppendLine($"  _lastJournalFrame = {lastJF.GetValue(ui)}");
            ReleaseJ(kb);
            yield return null;

            Check("第2次按 J 能关闭日志", !open2, $"open={open2}");

            if (open2) {
                _log.AppendLine();
                _log.AppendLine("⚠️ 关闭失败 —— 分析原因：");
                int fc = Time.frameCount;
                int lf = (int)lastJF.GetValue(ui);
                int of = (int)openedFrameF.GetValue(ui);
                _log.AppendLine($"  frameCount={fc} _lastJournalFrame={lf} _openedFrame={of}");
                _log.AppendLine($"  守卫156: frameCount==lastJournalFrame ? {fc == lf}");
                _log.AppendLine($"  守卫168: frameCount-openedFrame={fc-of} <= 2 ? {(fc-of) <= 2}");
                if (fc == lf) _log.AppendLine("  ❌ 守卫156 拦住了！这说明 _lastJournalFrame 在本帧被设过");
            }

            InputSystem.RemoveDevice(kb);
            Dump();
        }

        private void PressJ(Keyboard kb) {
            InputSystem.QueueStateEvent(kb, new KeyboardState(Key.J));
            InputSystem.Update();
        }
        private void ReleaseJ(Keyboard kb) {
            InputSystem.QueueStateEvent(kb, new KeyboardState());
            InputSystem.Update();
        }

        private void Check(string name, bool ok, string detail) {
            if (ok) { _pass++; _log.AppendLine($"[通过] {name}  {detail}"); }
            else { _fail++; _log.AppendLine($"[失败] {name}  {detail}"); }
        }

        private void Dump() {
            _log.AppendLine();
            _log.AppendLine($"通过 {_pass}，失败 {_fail}");
            string text = _log.ToString();
            Debug.Log("<b>[JKeySimTest]</b>\n" + text);
            try {
                Directory.CreateDirectory(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports");
                File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\JKeySimTest.txt", text, new UTF8Encoding(false));
            } catch { }
            if (!Application.isEditor) Application.Quit(_fail == 0 ? 0 : 1);
        }
    }
}