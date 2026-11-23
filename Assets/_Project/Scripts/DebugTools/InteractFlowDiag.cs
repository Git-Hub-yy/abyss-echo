using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// 交互全链路模拟：真正调用 OnInteract，并检查状态变化。
    /// 目的：确认"按 E"这个动作在代码层面是否真的有效。
    /// </summary>
    public class InteractFlowDiag : MonoBehaviour
    {
        public bool runOnStart = true;
        private readonly StringBuilder _log = new StringBuilder();

        private void Start()
        {
            if (runOnStart) StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            yield return new WaitForSeconds(0.8f);

            _log.AppendLine("=== 交互全链路模拟 ===");
            _log.AppendLine();

            // ── 1. 便条 ──
            var note = FindFirstObjectByType<AbyssEcho.Narrative.ReadableNote>();
            var nar = FindFirstObjectByType<AbyssEcho.UI.NarrativeUI>();
            if (note != null && nar != null)
            {
                _log.AppendLine("--- 便条 ---");
                _log.AppendLine($"  交互前 NarrativeUI.IsOpen = {nar.IsOpen}");
                note.OnInteract(null);
                yield return null;
                _log.AppendLine($"  调用 OnInteract 后 IsOpen = {nar.IsOpen} " + (nar.IsOpen ? "✅ 打开了" : "❌ 没打开"));

                // 检查 detector 状态
                var det = FindFirstObjectByType<AbyssEcho.Interaction.InteractionDetector>();
                _log.AppendLine($"  InteractionDetector.enabled = {(det != null ? det.enabled : false)} " +
                                "（面板打开时应为 False）");

                nar.Close();
                yield return null;
                _log.AppendLine($"  关闭后 IsOpen = {nar.IsOpen}，detector.enabled = {(det != null ? det.enabled : false)}");
                _log.AppendLine();
            }

            // ── 2. 门禁键盘 ──
            var keypad = FindFirstObjectByType<AbyssEcho.Puzzle.KeypadPanel>();
            var kui = FindFirstObjectByType<AbyssEcho.UI.KeypadUI>();
            if (keypad != null && kui != null)
            {
                _log.AppendLine("--- 门禁键盘 ---");
                _log.AppendLine($"  键盘位置 = {keypad.transform.position}");
                _log.AppendLine($"  交互前 KeypadUI.IsOpen = {kui.IsOpen}");
                keypad.OnInteract(null);
                yield return null;
                _log.AppendLine($"  调用 OnInteract 后 KeypadUI.IsOpen = {kui.IsOpen} " +
                                (kui.IsOpen ? "✅ 打开了" : "❌ 没打开"));
                _log.AppendLine($"  IsUnlocked = {keypad.IsUnlocked}");

                // 测试提交密码
                bool ok = keypad.Submit("1212");
                yield return null;
                _log.AppendLine($"  提交 1212 结果 = {ok}，IsUnlocked 现在 = {keypad.IsUnlocked}");
                _log.AppendLine();
            }
            else
            {
                _log.AppendLine($"--- 门禁键盘 --- ❌ keypad={keypad != null} kui={kui != null}");
            }

            // ── 3. 检查门状态 ──
            _log.AppendLine("--- 三扇门状态 ---");
            foreach (var d in FindObjectsByType<AbyssEcho.Interaction.DoorInteractable>(FindObjectsSortMode.None))
            {
                _log.AppendLine($"  {d.name}: locked={d.locked} unlockable={d.unlockableByInteraction} " +
                                $"IsOpen={d.IsOpen} 位置={d.transform.position}");
            }

            Flush();
            yield return null;
            if (!Application.isEditor) Application.Quit(0);
        }

        private void Flush()
        {
            string text = _log.ToString();
            Debug.Log("<b>[InteractFlow]</b>\n" + text);
            try
            {
                string dir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports"));
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "InteractFlowDiag.txt"), text, new UTF8Encoding(false));
            }
            catch { }
        }
    }
}