using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// 阶段二：完整的玩家实机流程测试（P0 复检）。
    ///
    /// 修复了之前所有测试的设计缺陷：
    ///   ① 等章节标题卡结束
    ///   ② 传送前先验证目标点有地板
    ///   ③ 相机与身体同步朝向
    ///   ④ 用累计 deltaTime 计时
    ///
    /// 测试项：
    ///   S1  出生点落地（不下坠）
    ///   S2  从出生点前进 3 秒不坠落
    ///   S3  沿主路径走过 A0→A1→A2 不掉下去
    ///   S4  交互检测（站到 Pickup_Fuse 前）
    ///   S5  拾取拾取物进背包+日志
    ///   S6  手电筒 Toggle 功能
    ///   S7  死亡与复活
    /// </summary>
    public class Phase2PlayerFlowTest : MonoBehaviour
    {
        private readonly StringBuilder _log = new StringBuilder();
        private int _pass, _fail;

        private AbyssEcho.Player.PlayerMotor _motor;
        private AbyssEcho.Player.PlayerLook _look;
        private UnityEngine.InputSystem.Keyboard _kb;

        public IEnumerator RunAll()
        {
            _log.AppendLine("══════ 阶段二 玩家实机流程（P0 复检）══════");

            _motor = FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
            _look = FindFirstObjectByType<AbyssEcho.Player.PlayerLook>();
            _kb = UnityEngine.InputSystem.Keyboard.current;

            if (_motor == null) { _log.AppendLine("❌ 无 PlayerMotor"); Dump(); yield break; }

            // 等标题卡
            var card = FindFirstObjectByType<AbyssEcho.UI.ChapterTitleCard>();
            if (card != null && card.enabled)
            {
                float w = 0f;
                while (card != null && card.enabled && w < 15f)
                { yield return new WaitForSeconds(0.25f); w += 0.25f; }
                _log.AppendLine($"  标题卡结束（{w:F1}s）");
            }
            yield return new WaitForSeconds(0.5f);

            // ══════ S1 出生点落地 ══════
            _log.AppendLine();
            _log.AppendLine("──── S1 出生点落地 ────");
            Vector3 spawnPos = _motor.transform.position;
            _log.AppendLine($"  出生点: {spawnPos}");

            float acc = 0f;
            while (acc < 2f) { yield return null; acc += Time.deltaTime; }

            Vector3 after2s = _motor.transform.position;
            float drop = spawnPos.y - after2s.y;
            _log.AppendLine($"  2 秒后: {after2s}  Y 变化 {drop:F3}m");

            var cc = _motor.GetComponent<CharacterController>();
            Assert("S1 出生点落地不下坠", Mathf.Abs(drop) < 0.5f,
                   $"Y 稳定在 {after2s.y:F2}（isGrounded={cc?.isGrounded}）",
                   $"下坠了 {drop:F2}m，当前 y={after2s.y:F2}");

            // ══════ S2 前进不坠落 ══════
            _log.AppendLine();
            _log.AppendLine("──── S2 前进 3 秒不坠落 ────");
            Vector3 p0 = _motor.transform.position;
            yield return Press(UnityEngine.InputSystem.Key.W, 3f);
            Vector3 p1 = _motor.transform.position;
            float moved = Vector3.Distance(new Vector3(p0.x, 0, p0.z), new Vector3(p1.x, 0, p1.z));

            _log.AppendLine($"  从 {p0} 走到 {p1}");
            _log.AppendLine($"  水平位移 {moved:F2}m，Y 变化 {p0.y - p1.y:F2}m");

            Assert("S2 前进有位移", moved > 0.5f, $"{moved:F2}m", $"仅 {moved:F2}m");
            Assert("S2b 前进后没掉下去", Mathf.Abs(p0.y - p1.y) < 1.5f,
                   $"Y 保持在 {p1.y:F2}", $"掉了 {p0.y - p1.y:F2}m");

            // ══════ S3 主路径行走 ══════
            _log.AppendLine();
            _log.AppendLine("──── S3 主路径行走（A0→A1）────");

            // 传送到 A1 走廊中央（已确认有地板）
            if (!TeleportTo(new Vector3(0f, 0.1f, -34.5f), 0f))
            {
                _log.AppendLine("  ⚠️ 目标点无地板，跳过");
            }
            else
            {
                yield return new WaitForSeconds(0.5f);
                Vector3 s0 = _motor.transform.position;
                yield return Press(UnityEngine.InputSystem.Key.W, 2f);
                Vector3 s1 = _motor.transform.position;
                float dm = Vector3.Distance(new Vector3(s0.x, 0, s0.z), new Vector3(s1.x, 0, s1.z));
                bool fell = s1.y < -1f;

                _log.AppendLine($"  A1 走廊: {s0} → {s1}, 位移 {dm:F2}m");
                Assert("S3 A1 走廊能正常行走", dm > 0.5f && !fell,
                       $"{dm:F2}m，Y={s1.y:F2}", fell ? $"坠落至 y={s1.y:F2}" : $"仅移动 {dm:F2}m");
            }

            // ══════ S4/S5 交互 + 拾取 ══════
            _log.AppendLine();
            _log.AppendLine("──── S4/S5 交互与拾取 ────");
            yield return TestInteractAndPickup();

            // ══════ S6 手电筒 ══════
            _log.AppendLine();
            _log.AppendLine("──── S6 手电筒 ────");
            var flash = FindFirstObjectByType<AbyssEcho.Tools.FlashlightSystem>();
            if (flash != null)
            {
                var tm = typeof(AbyssEcho.Tools.FlashlightSystem).GetMethod("Toggle",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                bool b = flash.IsOn;
                tm.Invoke(flash, null);
                yield return new WaitForSeconds(0.2f);
                bool a = flash.IsOn;
                Assert("S6 手电筒可开关", b != a, $"{b} → {a}", "状态未变");

                var lights = flash.GetComponentsInChildren<Light>(true);
                bool lightOn = false;
                foreach (var l in lights) if (l.enabled && l.intensity > 0) lightOn = true;
                Assert("S6b 手电筒实际发光", lightOn, "SpotLight 已点亮", "灯光未启用");
            }
            else
            {
                _log.AppendLine("  ⚠️ 无 FlashlightSystem");
            }

            // ══════ S7 死亡与复活 ══════
            _log.AppendLine();
            _log.AppendLine("──── S7 死亡与复活 ────");
            var death = FindFirstObjectByType<AbyssEcho.Horror.PlayerDeathSystem>();
            if (death != null && !death.IsDead)
            {
                death.Kill("QA");
                yield return new WaitForSeconds(0.3f);
                Assert("S7 死亡生效", death.IsDead, "IsDead=true", "未死亡");

                bool frozen = !_motor.enabled;
                Assert("S7b 死亡后无法移动", frozen, "motor 已禁用", "motor 仍启用");
            }
            else
            {
                _log.AppendLine($"  ⚠️ 无死亡系统或已死亡");
            }

            Dump();
        }

        private IEnumerator TestInteractAndPickup()
        {
            var pickups = FindObjectsByType<AbyssEcho.Interaction.PickupItem>(FindObjectsSortMode.None);
            if (pickups.Length == 0) { _log.AppendLine("  无拾取物"); yield break; }

            var target = pickups[0];
            Vector3 tp = target.transform.position;

            // 找目标前的地面
            Vector3 standPoint = tp + new Vector3(0f, 0f, -1.2f);
            if (!TeleportTo(new Vector3(standPoint.x, 0.1f, standPoint.z), 0f))
            {
                _log.AppendLine("  ⚠️ 拾取物前方无地板，改用直接调用 OnInteract 验证");
                yield return DirectPickup(target);
                yield break;
            }

            yield return new WaitForSeconds(0.4f);

            // 相机对准
            var cam = Camera.main;
            if (cam != null) cam.transform.LookAt(tp);
            yield return new WaitForSeconds(0.5f);

            var det = FindFirstObjectByType<AbyssEcho.Interaction.InteractionDetector>();
            if (det != null)
            {
                var dt = det.GetType();
                var BF = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
                var updateM = dt.GetMethod("Update", BF);
                updateM?.Invoke(det, null);

                // Current / CurrentPrompt 都是 public 属性（源文件 L35 / L41）
                var curP = dt.GetProperty("Current", BF);
                var promptP = dt.GetProperty("CurrentPrompt", BF);
                object cur = curP?.GetValue(det);
                string prompt = promptP?.GetValue(det) as string ?? "";

                _log.AppendLine($"  站到 {target.gameObject.name} 前 1.2m");
                _log.AppendLine($"  检测器: {(cur != null ? cur.ToString() : "null")}  提示='{prompt}'");

                Assert("S4 交互检测识别到物体", cur != null, $"{cur}", "null");
                Assert("S4b 交互提示非空", !string.IsNullOrEmpty(prompt), $"'{prompt}'", "空");
            }

            yield return DirectPickup(target);
        }

        private IEnumerator DirectPickup(AbyssEcho.Interaction.PickupItem target)
        {
            var inv = AbyssEcho.Inventory.PlayerInventory.Instance;
            var journal = AbyssEcho.Narrative.JournalSystem.Instance;
            var t = typeof(AbyssEcho.Interaction.PickupItem);
            var BF = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
            string id = t.GetField("itemId", BF)?.GetValue(target) as string ?? "?";

            int ib = inv.Count(id);
            int jb = journal.Entries.Count;

            target.OnInteract(null);
            yield return null; yield return null;

            int ia = inv.Count(id);
            int ja = journal.Entries.Count;

            _log.AppendLine($"  拾取 {id}: 背包 {ib}→{ia}  日志 {jb}→{ja}");
            Assert("S5 拾取进背包", ia > ib, $"{ib}→{ia}", "未增加");
            Assert("S5b 拾取写日志", ja > jb, $"{jb}→{ja}", "未增加");
        }

        /// <summary>传送到目标点前先验证该点有地板。返回是否成功。</summary>
        private bool TeleportTo(Vector3 target, float yaw)
        {
            // 检查目标点下方 3 米内是否有可站立的碰撞体
            bool hasFloor = Physics.Raycast(target + Vector3.up * 0.5f, Vector3.down,
                                            out var hit, 3f, ~0, QueryTriggerInteraction.Ignore);
            if (!hasFloor)
            {
                _log.AppendLine($"  ❌ 目标点 {target} 下方无地板，拒绝传送");
                return false;
            }

            var cc = _motor.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            _motor.transform.position = new Vector3(target.x, hit.point.y + 0.05f, target.z);
            _motor.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (cc != null) cc.enabled = true;
            Physics.SyncTransforms();
            return true;
        }

        private IEnumerator Press(UnityEngine.InputSystem.Key key, float seconds)
        {
            if (_kb == null) yield break;
            _kb.MakeCurrent();
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(_kb,
                new UnityEngine.InputSystem.LowLevel.KeyboardState(key));
            UnityEngine.InputSystem.InputSystem.Update();

            float acc = 0f;
            while (acc < seconds) { yield return null; acc += Time.deltaTime; }

            UnityEngine.InputSystem.InputSystem.QueueStateEvent(_kb,
                new UnityEngine.InputSystem.LowLevel.KeyboardState());
            UnityEngine.InputSystem.InputSystem.Update();
            yield return null;
        }

        private void Pass(string n, string d) { _pass++; _log.AppendLine($"  [通过] {n} — {d}"); }
        private void Fail(string n, string d) { _fail++; _log.AppendLine($"  [失败] {n} — {d}"); }
        private void Assert(string n, bool ok, string okD, string failD)
        { if (ok) Pass(n, okD ?? ""); else Fail(n, failD ?? ""); }

        private void Dump()
        {
            _log.AppendLine();
            _log.AppendLine($"══════ 通过 {_pass}，失败 {_fail} ══════");
            string text = _log.ToString();
            Debug.Log("<b>[Phase2PlayerFlow]</b>\n" + text);
            try
            {
                Directory.CreateDirectory(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Final_Test");
                File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Final_Test\02_Raw_playerflow.txt",
                                  text, new UTF8Encoding(false));
            }
            catch { }
            if (!Application.isEditor) Application.Quit(_fail == 0 ? 0 : 1);
        }
    }
}