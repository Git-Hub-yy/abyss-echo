using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// 阶段二：剩余失败项精确诊断。
    ///
    /// 针对 AutoTest_Stage2 的 3 个失败：
    ///   T11b 低矮通道内不能起身
    ///   T15  手电筒开启
    ///   T16  撞墙不穿透
    ///
    /// 检查这些失败是"测试坐标不对"还是"功能真的有问题"。
    /// </summary>
    public class Phase2RemainingDiag : MonoBehaviour
    {
        private readonly StringBuilder _log = new StringBuilder();

        public IEnumerator RunAll()
        {
            _log.AppendLine("══════ 剩余失败项诊断 ══════");
            _log.AppendLine();

            var motor = FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
            var flash = FindFirstObjectByType<AbyssEcho.Tools.FlashlightSystem>();
            var kb = Keyboard.current;

            // ── 等标题卡 ──
            var card = FindFirstObjectByType<AbyssEcho.UI.ChapterTitleCard>();
            if (card != null && card.enabled)
            {
                float w = 0f;
                while (card != null && card.enabled && w < 12f) { yield return new WaitForSeconds(0.25f); w += 0.25f; }
            }
            yield return new WaitForSeconds(0.5f);

            // ══════ T15 手电筒深度检查 ══════
            _log.AppendLine("──── T15 手电筒 ────");
            if (flash == null)
            {
                _log.AppendLine("  ❌ 场景里找不到 FlashlightSystem");
            }
            else
            {
                _log.AppendLine($"  找到 FlashlightSystem @ {flash.gameObject.name}");
                _log.AppendLine($"  flash.enabled = {flash.enabled}");
                _log.AppendLine($"  初始 IsOn = {flash.IsOn}");

                var lights = flash.GetComponentsInChildren<Light>(true);
                _log.AppendLine($"  子物体 Light 数 = {lights.Length}");
                foreach (var l in lights)
                    _log.AppendLine($"    {l.gameObject.name}: enabled={l.enabled} intensity={l.intensity:F1} range={l.range:F1}");

                // 尝试直接调用 Toggle
                var t = typeof(AbyssEcho.Tools.FlashlightSystem);
                var BF = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
                var toggleM = t.GetMethod("Toggle", BF);

                _log.AppendLine($"  Toggle() 方法: {(toggleM != null ? "存在" : "❌不存在")}");

                if (toggleM != null)
                {
                    bool before = flash.IsOn;
                    toggleM.Invoke(flash, null);
                    yield return null;
                    bool after = flash.IsOn;
                    _log.AppendLine($"  直接调用 Toggle(): {before} → {after}  {(before != after ? "✅ 能切换" : "❌ 不能切换")}");
                }

                // 尝试 F 键
                bool beforeKey = flash.IsOn;
                if (kb != null)
                {
                    InputSystem.QueueStateEvent(kb, new KeyboardState(Key.F));
                    InputSystem.Update();
                    yield return new WaitForSeconds(0.3f);
                    InputSystem.QueueStateEvent(kb, new KeyboardState());
                    InputSystem.Update();
                    yield return new WaitForSeconds(0.2f);
                }
                bool afterKey = flash.IsOn;
                _log.AppendLine($"  按 F 键: {beforeKey} → {afterKey}  {(beforeKey != afterKey ? "✅ 按键生效" : "❌ 按键无效")}");

                // 检查 input.FlashlightPressed
                var input = FindFirstObjectByType<AbyssEcho.Player.PlayerInputReader>();
                if (input != null)
                {
                    var fp = typeof(AbyssEcho.Player.PlayerInputReader).GetProperty("FlashlightPressed",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    _log.AppendLine($"  PlayerInputReader.FlashlightPressed = {fp?.GetValue(input)}");
                }
            }
            _log.AppendLine();

            // ══════ T11b 低矮通道几何检查 ══════
            _log.AppendLine("──── T11b 低矮通道 ────");
            Vector3 crouchSpot = new Vector3(0f, 0.05f, 8f);
            _log.AppendLine($"  测试点: {crouchSpot}");
            _log.AppendLine($"  该点上方射线测试（找天花板高度）：");

            var hits = Physics.RaycastAll(crouchSpot + Vector3.up * 0.1f, Vector3.up, 5f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            if (hits.Length == 0)
            {
                _log.AppendLine("    ❌ 头顶 5 米内无任何物体（不是低矮通道，是开阔空间）");
            }
            else
            {
                foreach (var h in hits)
                    _log.AppendLine($"    {h.collider.gameObject.name} @ y={h.point.y:F2}（距 {h.distance:F2}m）");
                _log.AppendLine($"    → 头顶最低物体高度 y={hits[0].point.y:F2}，玩家站立需 1.78m，蹲下 1.15m");
                _log.AppendLine($"    → {(hits[0].point.y < 1.78f ? "确实是低矮通道" : "高度足够站立，不是低矮通道")}");
            }
            _log.AppendLine();

            // ══════ T16 撞墙测试环境检查 ══════
            _log.AppendLine("──── T16 撞墙 ────");
            _log.AppendLine("  检查玩家从 (0,-6) 向北移动的路径：");
            var origin = new Vector3(0f, 1.0f, -6f);
            var fwdHits = Physics.RaycastAll(origin, Vector3.forward, 25f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(fwdHits, (a, b) => a.distance.CompareTo(b.distance));
            if (fwdHits.Length == 0)
                _log.AppendLine("    前方 25 米内无阻挡");
            else
            {
                int shown = 0;
                foreach (var h in fwdHits)
                {
                    if (shown++ >= 8) break;
                    _log.AppendLine($"    z={h.point.z:F2}  {h.collider.gameObject.name}");
                }
            }

            Dump();
        }

        private void Dump()
        {
            string text = _log.ToString();
            Debug.Log("<b>[Phase2RemainingDiag]</b>\n" + text);
            try
            {
                Directory.CreateDirectory(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Final_Test");
                File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Final_Test\02_Raw_remaining.txt",
                                  text, new UTF8Encoding(false));
            }
            catch { }
            if (!Application.isEditor) Application.Quit(0);
        }
    }
}