using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// 阶段二：移动测试（帧率感知版）。
    ///
    /// 前两次测试失败的根因怀疑：
    ///   未获得焦点的窗口帧率极低，deltaTime 很小，
    ///   0.6~0.8 秒内实际只跑了几帧，位移接近 0。
    ///
    /// 本测试：
    ///   · 记录耗时、帧数、平均帧率、累计 deltaTime
    ///   · 用**累计 deltaTime** 作为时长基准，而不是墙钟时间
    ///   · 等待足够长的真实时间（3 秒）
    /// </summary>
    public class MovementDiagTest : MonoBehaviour
    {
        private readonly StringBuilder _log = new StringBuilder();

        public IEnumerator RunAll()
        {
            _log.AppendLine("══════ 移动测试（帧率诊断）══════");
            _log.AppendLine();

            var motor = FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
            var input = FindFirstObjectByType<AbyssEcho.Player.PlayerInputReader>();
            var kb = Keyboard.current;

            if (motor == null || input == null || kb == null)
            {
                _log.AppendLine($"❌ motor={motor != null} input={input != null} kb={kb != null}");
                Dump(); yield break;
            }

            // ══════════ 等待章节标题卡结束 ══════════
            //
            //   第一章的 ChapterTitleCard 会在开场禁用 PlayerMotor，
            //   持续 blackHold(1.2) + titleFadeIn(0.8) + titleHold(1.8) + fadeOut(1.2) = 5 秒。
            //   如果不等待，移动测试全部失败 —— 这是测试设计问题，不是游戏 bug。
            var titleCard = FindFirstObjectByType<AbyssEcho.UI.ChapterTitleCard>();
            if (titleCard != null && titleCard.enabled)
            {
                _log.AppendLine("  ⏳ 检测到章节标题卡，等待其结束...");
                float waited = 0f;
                while (titleCard != null && titleCard.enabled && waited < 12f)
                {
                    yield return new WaitForSeconds(0.25f);
                    waited += 0.25f;
                }
                _log.AppendLine($"  ✅ 标题卡已结束（等待 {waited:F1}s）");
            }
            // 再等 0.5 秒让 PlayerMotor 恢复
            yield return new WaitForSeconds(0.5f);

            // 窗口焦点
            _log.AppendLine($"Application.isFocused = {Application.isFocused}");
            _log.AppendLine($"Application.runInBackground = {Application.runInBackground}");
            _log.AppendLine($"Time.timeScale = {Time.timeScale}");
            _log.AppendLine($"motor.enabled = {motor.enabled}");
            _log.AppendLine($"input.InputEnabled = {input.InputEnabled}");
            _log.AppendLine($"Time.captureDeltaTime = {Time.captureDeltaTime}");
            _log.AppendLine();

            var cc = motor.GetComponent<CharacterController>();
            _log.AppendLine($"CharacterController: {(cc != null ? "存在" : "❌无")}");
            if (cc != null)
                _log.AppendLine($"  enabled={cc.enabled} isGrounded={cc.isGrounded} height={cc.height} radius={cc.radius}");
            _log.AppendLine();

            // 测试 W 键，严格记录帧数据
            foreach (var (key, name) in new (Key, string)[] {
                (Key.W, "W(前进)"), (Key.S, "S(后退)"), (Key.A, "A(左移)"), (Key.D, "D(右移)") })
            {
                // 复位到开阔点
                if (cc != null) cc.enabled = false;
                motor.transform.position = new Vector3(0f, 0.05f, -8f);
                motor.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
                if (cc != null) cc.enabled = true;
                Physics.SyncTransforms();
                yield return new WaitForSeconds(0.5f);

                Vector3 p0 = motor.transform.position;
                float t0 = Time.realtimeSinceStartup;
                float accDt = 0f;
                int frames = 0;

                // 按下
                InputSystem.QueueStateEvent(kb, new KeyboardState(key));
                InputSystem.Update();

                // 按累计 deltaTime 计时：直到累计 1.0 秒游戏时间
                while (accDt < 1.0f && frames < 600)
                {
                    yield return null;
                    accDt += Time.deltaTime;
                    frames++;
                }

                // 释放
                InputSystem.QueueStateEvent(kb, new KeyboardState());
                InputSystem.Update();
                yield return null;

                Vector3 p1 = motor.transform.position;
                float wallTime = Time.realtimeSinceStartup - t0;
                Vector3 delta = new Vector3(p1.x - p0.x, 0, p1.z - p0.z);

                _log.AppendLine($"{name}");
                _log.AppendLine($"  墙钟耗时 {wallTime:F2}s  帧数 {frames}  累计deltaTime {accDt:F3}s  " +
                                $"平均帧率 {(frames / Mathf.Max(0.01f, wallTime)):F1} fps");
                _log.AppendLine($"  位移 ({delta.x:F2}, {delta.z:F2})  距离 {delta.magnitude:F2}m");
                _log.AppendLine($"  {(delta.magnitude > 0.5f ? "✅ 移动正常" : "❌ 未移动")}");
                _log.AppendLine();
            }

            Dump();
        }

        private void Dump()
        {
            string text = _log.ToString();
            Debug.Log("<b>[MovementDiagTest]</b>\n" + text);
            try
            {
                Directory.CreateDirectory(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Final_Test");
                File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Final_Test\02_Raw_movement.txt",
                                  text, new UTF8Encoding(false));
            }
            catch { }
            if (!Application.isEditor) Application.Quit(0);
        }
    }
}