using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// 阶段二：验证「旧输入系统 Input.GetKeyDown 在测试环境是否可用」。
    ///
    /// 背景：
    ///   FlashlightSystem、NarrativeUI、InventoryUI、PeaceModeManager 等
    ///   大量使用 Input.GetKeyDown（旧输入系统）。
    ///
    ///   自动化测试通过 InputSystem.QueueStateEvent 模拟按键，
    ///   这只会更新新输入系统，**不会**更新 UnityEngine.Input。
    ///
    /// 本测试：
    ///   同时用两种方式注入 F 键，观察哪条路径能生效。
    /// </summary>
    public class LegacyInputTest : MonoBehaviour
    {
        private readonly StringBuilder _log = new StringBuilder();

        public IEnumerator RunAll()
        {
            _log.AppendLine("══════ 旧输入系统可用性验证 ══════");
            _log.AppendLine();

            var flash = FindFirstObjectByType<AbyssEcho.Tools.FlashlightSystem>();
            var kb = Keyboard.current;

            // 等标题卡
            var card = FindFirstObjectByType<AbyssEcho.UI.ChapterTitleCard>();
            if (card != null && card.enabled)
            {
                float w = 0f;
                while (card != null && card.enabled && w < 12f) { yield return new WaitForSeconds(0.25f); w += 0.25f; }
            }
            yield return new WaitForSeconds(0.5f);

            _log.AppendLine($"FlashlightSystem: {(flash != null ? "存在" : "❌")}");
            _log.AppendLine($"Keyboard.current: {(kb != null ? "存在" : "❌")}");
            _log.AppendLine($"InputSystem.settings.updateMode: {InputSystem.settings.updateMode}");
            _log.AppendLine();

            // 测试 A：InputSystem 路径
            _log.AppendLine("──── 方式 A：InputSystem.QueueStateEvent 注入 F ────");
            if (flash != null && kb != null)
            {
                bool before = flash.IsOn;
                InputSystem.QueueStateEvent(kb, new KeyboardState(Key.F));
                InputSystem.Update();
                yield return new WaitForSeconds(0.25f);
                InputSystem.QueueStateEvent(kb, new KeyboardState());
                InputSystem.Update();
                yield return new WaitForSeconds(0.25f);
                bool after = flash.IsOn;
                _log.AppendLine($"  IsOn: {before} → {after}  {(before != after ? "✅ 生效" : "❌ 未生效")}");
                _log.AppendLine($"  → 说明 InputSystem 注入{(before != after ? "能" : "不能")}被 Input.GetKeyDown 读到");
            }
            yield return null;

            // 测试 B：InputState.Change 路径（AutoTest_Stage2 用的方式）
            _log.AppendLine();
            _log.AppendLine("──── 方式 B：InputState.Change 注入 F ────");
            if (flash != null && kb != null)
            {
                bool before = flash.IsOn;
                InputState.Change(kb, new KeyboardState(Key.F));
                yield return new WaitForSeconds(0.25f);
                InputState.Change(kb, new KeyboardState());
                yield return new WaitForSeconds(0.25f);
                bool after = flash.IsOn;
                _log.AppendLine($"  IsOn: {before} → {after}  {(before != after ? "✅ 生效" : "❌ 未生效")}");
            }
            yield return null;

            // 测试 C：直接调用 Toggle（确认功能本身正常）
            _log.AppendLine();
            _log.AppendLine("──── 方式 C：直接调用 Toggle() ────");
            if (flash != null)
            {
                var t = typeof(AbyssEcho.Tools.FlashlightSystem);
                var toggleM = t.GetMethod("Toggle",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

                bool before = flash.IsOn;
                toggleM.Invoke(flash, null);
                yield return new WaitForSeconds(0.15f);
                bool after = flash.IsOn;
                _log.AppendLine($"  IsOn: {before} → {after}  {(before != after ? "✅ 功能正常" : "❌ 功能异常")}");

                // 检查灯光实际状态
                var lights = flash.GetComponentsInChildren<Light>(true);
                foreach (var l in lights)
                    _log.AppendLine($"    {l.gameObject.name}: enabled={l.enabled} intensity={l.intensity:F1}");

                // 恢复关灯
                if (flash.IsOn) toggleM.Invoke(flash, null);
            }

            _log.AppendLine();
            _log.AppendLine("──── 结论 ────");
            _log.AppendLine("  如果 A/B 未生效但 C 正常，说明：");
            _log.AppendLine("   · 手电筒**功能本身正常**");
            _log.AppendLine("   · 自动化测试**无法验证旧输入系统按键**");
            _log.AppendLine("   · 需要人工按 F 键确认");

            Dump();
        }

        private void Dump()
        {
            string text = _log.ToString();
            Debug.Log("<b>[LegacyInputTest]</b>\n" + text);
            try
            {
                Directory.CreateDirectory(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Final_Test");
                File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Final_Test\02_Raw_legacyinput.txt",
                                  text, new UTF8Encoding(false));
            }
            catch { }
            if (!Application.isEditor) Application.Quit(0);
        }
    }
}