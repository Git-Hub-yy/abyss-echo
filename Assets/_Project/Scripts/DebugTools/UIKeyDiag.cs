using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// UI 按键诊断：检查 Tab 到底会打开什么。
    /// 直接调用各面板的 API 并记录状态，绕开键盘输入。
    /// </summary>
    public class UIKeyDiag : MonoBehaviour
    {
        public bool runOnStart = true;
        private readonly StringBuilder _log = new StringBuilder();

        private void Start()
        {
            if (runOnStart) StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            yield return new WaitForSeconds(1.0f);

            _log.AppendLine("=== UI 按键诊断 ===");
            _log.AppendLine($"场景：{UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");

            var inv = FindFirstObjectByType<AbyssEcho.UI.InventoryUI>();
            var nar = FindFirstObjectByType<AbyssEcho.UI.NarrativeUI>();
            var pause = FindFirstObjectByType<AbyssEcho.UI.PauseMenuUI>();
            var keypad = FindFirstObjectByType<AbyssEcho.UI.KeypadUI>();

            _log.AppendLine();
            _log.AppendLine("--- 组件是否存在 ---");
            _log.AppendLine($"InventoryUI: {(inv != null ? inv.gameObject.name + " enabled=" + inv.enabled : "❌ 无")}");
            _log.AppendLine($"NarrativeUI: {(nar != null ? nar.gameObject.name + " enabled=" + nar.enabled : "❌ 无")}");
            _log.AppendLine($"PauseMenuUI: {(pause != null ? pause.gameObject.name + " enabled=" + pause.enabled : "❌ 无")}");
            _log.AppendLine($"KeypadUI: {(keypad != null ? keypad.gameObject.name + " enabled=" + keypad.enabled : "❌ 无")}");

            _log.AppendLine();
            _log.AppendLine("--- 初始状态 ---");
            _log.AppendLine($"inventory.IsOpen = {(inv != null ? inv.IsOpen : false)}");
            _log.AppendLine($"narrative.IsOpen = {(nar != null ? nar.IsOpen : false)}");

            // 模拟"按 Tab"
            _log.AppendLine();
            _log.AppendLine("--- 模拟按 Tab（调用 InventoryUI.Toggle）---");
            if (inv != null)
            {
                inv.Toggle();
                yield return null;

                _log.AppendLine($"  调用后 inventory.IsOpen = {inv.IsOpen}");
                _log.AppendLine($"  调用后 narrative.IsOpen = {(nar != null ? nar.IsOpen : false)}");
                _log.AppendLine($"  调用后 pause.IsOpen = {(pause != null ? pause.IsOpen : false)}");
                _log.AppendLine($"  → 打开的是：{(inv.IsOpen ? "物品栏 ✅" : "物品栏未打开 ❌")}");
            }

            // 模拟"按 J"
            _log.AppendLine();
            _log.AppendLine("--- 模拟按 J（调用 NarrativeUI.ToggleJournal）---");
            if (inv != null) inv.Close();
            yield return null;

            if (nar != null)
            {
                nar.ToggleJournal();
                yield return null;

                _log.AppendLine($"  调用后 inventory.IsOpen = {(inv != null ? inv.IsOpen : false)}");
                _log.AppendLine($"  调用后 narrative.IsOpen = {nar.IsOpen}");
                _log.AppendLine($"  → 打开的是：{(nar.IsOpen ? "日志 ✅" : "日志未打开 ❌")}");
            }

            // 整理
            if (nar != null) nar.Close();
            if (inv != null) inv.Close();

            Flush();
            yield return null;
            if (!Application.isEditor) Application.Quit(0);
        }

        private void Flush()
        {
            string text = _log.ToString();
            Debug.Log("<b>[UIKeyDiag]</b>\n" + text);
            try
            {
                string dir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports"));
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "UIKeyDiag.txt"), text, new UTF8Encoding(false));
            }
            catch { }
        }
    }
}