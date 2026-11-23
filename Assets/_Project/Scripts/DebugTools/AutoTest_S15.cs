using System.Collections;
using System.IO;
using System.Text;
using AbyssEcho.UI;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// S15 自动测试：操作说明面板 + 日志开关修复。
    /// </summary>
    public class AutoTest_S15 : MonoBehaviour
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
            _log.AppendLine("ABYSS ECHO · S15 操作提示与日志修复测试");
            _log.AppendLine("=====================================");

            yield return new WaitForSeconds(0.5f);

            T101_ControlsHelpExists();
            T102_ControlsHelpToggle();
            T103_JournalCanClose();
            T104_JournalToggleRoundTrip();

            _log.AppendLine("=====================================");
            _log.AppendLine($"结果：通过 {_pass}，失败 {_fail}");

            string text = _log.ToString();
            Debug.Log("<b>[AutoTest_S15]</b>\n" + text);
            WriteReport(text);

            yield return new WaitForSeconds(0.2f);
            if (!Application.isEditor) Application.Quit(_fail == 0 ? 0 : 1);
        }

        private void T101_ControlsHelpExists()
        {
            var help = FindFirstObjectByType<ControlsHelpUI>();
            Check("T101 操作说明面板存在", help != null,
                  help != null ? $"自动显示={help.showOnFirstEnter}" : "");
        }

        private void T102_ControlsHelpToggle()
        {
            var help = FindFirstObjectByType<ControlsHelpUI>();
            if (help == null) { Fail("T102 操作说明可开关", "无面板"); return; }

            help.Close();
            bool before = help.IsOpen;

            help.Open(autoHide: false);
            bool opened = help.IsOpen;

            help.Close();
            bool closed = !help.IsOpen;

            Check("T102 操作说明可开关", !before && opened && closed,
                  $"初始={before} 打开={opened} 关闭={closed}");
        }

        private void T103_JournalCanClose()
        {
            var ui = FindFirstObjectByType<NarrativeUI>();
            if (ui == null) { Fail("T103 日志可关闭", "无 NarrativeUI"); return; }

            ui.Close();
            ui.ToggleJournal();
            bool opened = ui.IsOpen;

            ui.Close();
            bool closed = !ui.IsOpen;

            Check("T103 日志面板可打开并关闭", opened && closed,
                  $"打开={opened} 关闭={closed}");
        }

        private void T104_JournalToggleRoundTrip()
        {
            var ui = FindFirstObjectByType<NarrativeUI>();
            if (ui == null) { Fail("T104 日志可反复开关", "无 NarrativeUI"); return; }

            ui.Close();

            // 模拟连续 3 次开关
            bool allOk = true;
            for (int i = 0; i < 3; i++)
            {
                ui.ToggleJournal();
                if (!ui.IsOpen) { allOk = false; break; }

                ui.Close();
                if (ui.IsOpen) { allOk = false; break; }
            }

            Check("T104 日志可反复开关", allOk, "连续 3 轮开关");
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
                File.WriteAllText(Path.Combine(dir, "S15_AutoTestResult.txt"), text, new UTF8Encoding(false));
            }
            catch { }
        }
    }
}