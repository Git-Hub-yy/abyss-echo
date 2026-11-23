using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// 阶段二：输入系统诊断。
    ///
    /// 目的：确认 W/S/A/D 四个方向键在 Input System 层面是否被正确读取。
    ///
    /// 方法：逐个按键，读取 PlayerInputReader.Move 的值。
    /// 这样可以区分：
    ///   · 输入层问题（Move 值不对）
    ///   · 移动层问题（Move 对但玩家不动）
    /// </summary>
    public class InputDiagTest : MonoBehaviour
    {
        private readonly StringBuilder _log = new StringBuilder();

        public IEnumerator RunAll()
        {
            _log.AppendLine("══════ 输入系统诊断 ══════");
            _log.AppendLine();

            var input = FindFirstObjectByType<AbyssEcho.Player.PlayerInputReader>();
            var motor = FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
            var kb = Keyboard.current;

            if (input == null) { _log.AppendLine("❌ 无 PlayerInputReader"); Dump(); yield break; }
            if (kb == null) { _log.AppendLine("❌ 无键盘设备"); Dump(); yield break; }

            _log.AppendLine($"PlayerInputReader: {input.gameObject.name}");
            _log.AppendLine($"InputEnabled: {input.InputEnabled}");
            _log.AppendLine($"PlayerMotor: {(motor != null ? motor.gameObject.name : "null")}");
            _log.AppendLine();

            // 逐个按键测试
            var keys = new (Key key, string name, Vector2 expect)[]
            {
                (Key.W, "W", new Vector2(0, 1)),
                (Key.S, "S", new Vector2(0, -1)),
                (Key.A, "A", new Vector2(-1, 0)),
                (Key.D, "D", new Vector2(1, 0)),
            };

            _log.AppendLine("──── Input System 原始读取 ────");
            _log.AppendLine($"  {"键",-6} {"期望Move",-12} {"实际Move",-16} {"Motor速度",-14} 判定");

            foreach (var (key, name, expect) in keys)
            {
                // 清空所有键
                InputSystem.QueueStateEvent(kb, new KeyboardState());
                InputSystem.Update();
                yield return null;

                // 按下单个键
                InputSystem.QueueStateEvent(kb, new KeyboardState(key));
                InputSystem.Update();

                // 等 PlayerInputReader.Update 跑一帧
                yield return null;
                yield return null;

                Vector2 mv = input.Move;
                float yaw = motor != null ? motor.transform.eulerAngles.y : 0f;
                Vector3 fwd = motor != null ? motor.transform.forward : Vector3.zero;
                Vector3 right = motor != null ? motor.transform.right : Vector3.zero;

                bool ok = Vector2.Dot(mv.normalized, expect.normalized) > 0.7f;
                _log.AppendLine($"  {name,-6} {expect.ToString(),-12} {mv.ToString(),-16} " +
                                $"yaw={yaw:F1} fwd=({fwd.x:F2},{fwd.z:F2}),right=({right.x:F2},{right.z:F2})".PadRight(14) +
                                $" {(ok ? "✅ 输入正确" : "❌ 输入错误")}");

                // 释放
                InputSystem.QueueStateEvent(kb, new KeyboardState());
                InputSystem.Update();
                yield return null;
            }

            _log.AppendLine();
            _log.AppendLine("──── 移动实际效果 ────");

            // 用 W 键实际移动，记录位移向量
            if (motor != null)
            {
                var cc = motor.GetComponent<CharacterController>();
                Vector3 start;

                foreach (var (key, name, expect) in new (Key, string, Vector2)[]
                {
                    (Key.W, "W(前进)", new Vector2(0, 1)),
                    (Key.D, "D(右移)", new Vector2(1, 0)),
                })
                {
                    // 传送到开阔点
                    if (cc != null) cc.enabled = false;
                    motor.transform.position = new Vector3(0f, 0.05f, -8f);
                    motor.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
                    if (cc != null) cc.enabled = true;
                    Physics.SyncTransforms();
                    yield return new WaitForSeconds(0.3f);

                    start = motor.transform.position;

                    InputSystem.QueueStateEvent(kb, new KeyboardState(key));
                    InputSystem.Update();
                    yield return new WaitForSeconds(0.7f);
                    InputSystem.QueueStateEvent(kb, new KeyboardState());
                    InputSystem.Update();
                    yield return new WaitForSeconds(0.2f);

                    Vector3 end = motor.transform.position;
                    Vector3 delta = new Vector3(end.x - start.x, 0, end.z - start.z);

                    _log.AppendLine($"  {name,-10} 位移=({delta.x:F2}, {delta.z:F2})  距离={delta.magnitude:F2}m");
                }
            }

            Dump();
        }

        private void Dump()
        {
            string text = _log.ToString();
            Debug.Log("<b>[InputDiagTest]</b>\n" + text);
            try
            {
                Directory.CreateDirectory(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Final_Test");
                File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Final_Test\02_Raw_inputdiag.txt",
                                  text, new UTF8Encoding(false));
            }
            catch { }
            if (!Application.isEditor) Application.Quit(0);
        }
    }
}