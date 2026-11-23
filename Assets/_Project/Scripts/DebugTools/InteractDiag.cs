using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// 交互链路诊断（只读，不修改任何游戏状态）。
    ///
    /// 逐环节检查"按 E 为什么没反应"：
    ///   ① PlayerInputReader 是否存在、是否订阅了 Interact
    ///   ② InteractionDetector 是否存在、能否探到目标
    ///   ③ 目标对象的 CanInteract / interactable / enabled
    ///   ④ 直接调用 OnInteract 看是否真的能执行
    /// </summary>
    public class InteractDiag : MonoBehaviour
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

            _log.AppendLine("=== 交互链路诊断 ===");
            _log.AppendLine();

            // ① PlayerInputReader
            var input = FindFirstObjectByType<AbyssEcho.Player.PlayerInputReader>();
            _log.AppendLine("--- ① PlayerInputReader ---");
            if (input == null)
            {
                _log.AppendLine("  ❌ 完全找不到 PlayerInputReader！");
            }
            else
            {
                _log.AppendLine($"  对象={input.gameObject.name} enabled={input.enabled} active={input.gameObject.activeInHierarchy}");
                _log.AppendLine($"  InputEnabled={input.InputEnabled}");

                // 用反射看 OnInteract 事件有没有订阅者
                var evField = typeof(AbyssEcho.Player.PlayerInputReader)
                    .GetField("OnInteract", System.Reflection.BindingFlags.Instance |
                                            System.Reflection.BindingFlags.NonPublic |
                                            System.Reflection.BindingFlags.Public);
                if (evField != null)
                {
                    var del = evField.GetValue(input) as System.Delegate;
                    int n = del?.GetInvocationList().Length ?? 0;
                    _log.AppendLine($"  OnInteract 订阅者数量 = {n}" +
                                    (n == 0 ? "   ❌ 没有订阅者，按 E 不会有人收到！" : "   ✅"));
                }
                else
                {
                    _log.AppendLine("  （OnInteract 是 event，反射读不到，下面改用主动触发验证）");
                }
            }
            _log.AppendLine();

            // ② InteractionDetector
            var det = FindFirstObjectByType<AbyssEcho.Interaction.InteractionDetector>();
            _log.AppendLine("--- ② InteractionDetector ---");
            if (det == null)
            {
                _log.AppendLine("  ❌ 找不到 InteractionDetector");
            }
            else
            {
                _log.AppendLine($"  对象={det.gameObject.name} enabled={det.enabled} active={det.gameObject.activeInHierarchy}");
                _log.AppendLine($"  interactDistance={det.interactDistance} castRadius={det.castRadius}");
                _log.AppendLine($"  Current={(det.Current != null ? det.Current.GetType().Name : "null")}");
                _log.AppendLine($"  CurrentPrompt=\"{det.CurrentPrompt}\"");
            }
            _log.AppendLine();

            // ③ 逐个检查所有交互物
            _log.AppendLine("--- ③ 场景中所有交互物状态 ---");
            var bases = FindObjectsByType<AbyssEcho.Interaction.InteractableBase>(FindObjectsSortMode.None);
            _log.AppendLine($"共 {bases.Length} 个");
            foreach (var b in bases)
            {
                if (b == null) continue;
                _log.AppendLine($"  {b.gameObject.name}: " +
                                $"interactable={b.interactable} " +
                                $"isActiveAndEnabled={b.isActiveAndEnabled} " +
                                $"CanInteract={b.CanInteract} " +
                                $"enabled={b.enabled} " +
                                $"collider={(b.GetComponent<Collider>() != null ? "有" : "❌无")} " +
                                $"pos={b.transform.position}");
            }
            _log.AppendLine();

            // ④ 检查三个 UI 的 IsOpen（可能挡住输入）
            _log.AppendLine("--- ④ UI 面板状态 ---");
            var inv = FindFirstObjectByType<AbyssEcho.UI.InventoryUI>();
            var nar = FindFirstObjectByType<AbyssEcho.UI.NarrativeUI>();
            var pause = FindFirstObjectByType<AbyssEcho.UI.PauseMenuUI>();
            var keypad = FindFirstObjectByType<AbyssEcho.UI.KeypadUI>();
            _log.AppendLine($"  InventoryUI.IsOpen = {(inv != null ? inv.IsOpen : false)}");
            _log.AppendLine($"  NarrativeUI.IsOpen = {(nar != null ? nar.IsOpen : false)}");
            _log.AppendLine($"  PauseMenuUI.IsOpen = {(pause != null ? pause.IsOpen : false)}");
            _log.AppendLine($"  KeypadUI.IsOpen = {(keypad != null ? keypad.IsOpen : false)}");
            _log.AppendLine();

            // ⑤ 直接调用一次 OnInteract 验证逻辑本身（选门和便条）
            _log.AppendLine("--- ⑤ 直接调用 OnInteract 验证逻辑 ---");
            foreach (var b in bases)
            {
                if (b == null) continue;
                if (b.gameObject.name == "Door_C2" || b.gameObject.name == "Note_KeypadCode")
                {
                    _log.AppendLine($"  对 {b.gameObject.name} 调用 OnInteract...");
                    try
                    {
                        b.OnInteract(null);
                        _log.AppendLine($"    调用完成，CanInteract 现在={b.CanInteract}");
                    }
                    catch (System.Exception e)
                    {
                        _log.AppendLine($"    ❌ 抛异常：{e.Message}");
                    }
                }
            }

            Flush();
            yield return null;
            if (!Application.isEditor) Application.Quit(0);
        }

        private void Flush()
        {
            string text = _log.ToString();
            Debug.Log("<b>[InteractDiag]</b>\n" + text);
            try
            {
                string dir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports"));
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "InteractDiag.txt"), text, new UTF8Encoding(false));
            }
            catch { }
        }
    }
}