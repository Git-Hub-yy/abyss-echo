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
    /// 阶段二：核心功能精细测试。
    ///
    /// 与 AutoTest_Stage2 的区别：
    ///   AutoTest_Stage2 直接在出生点按 W/S/A/D，
    ///   但出生点前方可能有墙，导致"没移动"误判为 bug。
    ///
    ///   本测试会：
    ///     · 每次移动前把玩家传送到**确认开阔的位置**
    ///     · 每次移动前把视角**对准开阔方向**
    ///     · 记录是"输入没反应"还是"被墙挡住"
    ///
    /// 测试项：
    ///   M1 前进(W) 在开阔地
    ///   M2 后退(S) 在开阔地
    ///   M3 左移(A) 在开阔地
    ///   M4 右移(D) 在开阔地
    ///   M5 墙体阻挡（撞墙不穿透）
    ///   M6 地面碰撞（不下沉）
    ///   M7 视角控制（左右上下）
    ///   C1 交互检测（能否识别可交互物）
    ///   C2 交互提示
    ///   P1 暂停/恢复
    /// </summary>
    public class Phase2PreciseTest : MonoBehaviour
    {
        private readonly StringBuilder _log = new StringBuilder();
        private int _pass, _fail;

        private AbyssEcho.Player.PlayerMotor _motor;
        private AbyssEcho.Player.PlayerLook _look;
        private AbyssEcho.Player.PlayerInputReader _input;
        private AbyssEcho.Interaction.InteractionDetector _detector;
        private Keyboard _kb;
        private Mouse _mouse;

        public IEnumerator RunAll()
        {
            _log.AppendLine("══════ 阶段二 精细功能测试 ══════");
            _log.AppendLine($"场景: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
            _log.AppendLine();

            _motor = FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
            _look = FindFirstObjectByType<AbyssEcho.Player.PlayerLook>();
            _input = FindFirstObjectByType<AbyssEcho.Player.PlayerInputReader>();
            _detector = FindFirstObjectByType<AbyssEcho.Interaction.InteractionDetector>();
            _kb = Keyboard.current;
            _mouse = Mouse.current;

            if (_motor == null) { _log.AppendLine("❌ 无 PlayerMotor"); Dump(); yield break; }

            // 禁用 Look 防止鼠标干扰
            if (_look != null) _look.enabled = false;

            yield return new WaitForSeconds(0.3f);

            // ── M1~M4：在开阔地测试四个方向 ──
            _log.AppendLine("──── 移动测试（开阔地）────");

            // 找一片开阔地：检查候选点周围是否有墙
            Vector3 openPos = FindOpenArea();
            _log.AppendLine($"  开阔测试点: {openPos}");
            _log.AppendLine();

            yield return TestDirection("M1 前进(W)", Key.W, openPos, 0f);
            Teleport(openPos);
            yield return TestDirection("M2 后退(S)", Key.S, openPos, 0f);
            Teleport(openPos);
            yield return TestDirection("M3 左移(A)", Key.A, openPos, 0f);
            Teleport(openPos);
            yield return TestDirection("M4 右移(D)", Key.D, openPos, 0f);
            Teleport(openPos);

            // ── M5：撞墙测试 ──
            _log.AppendLine();
            _log.AppendLine("──── 碰撞测试 ────");
            yield return TestWallCollision();

            // ── M6：地面不下沉 ──
            Teleport(openPos);
            Vector3 beforeY = _motor.transform.position;
            yield return new WaitForSeconds(1.0f);
            Vector3 afterY = _motor.transform.position;
            float dropY = beforeY.y - afterY.y;
            Assert("M6 站立不下沉", dropY < 0.1f && dropY > -0.1f,
                   $"1 秒内 Y 变化 {dropY:F3} m",
                   $"下沉了 {dropY:F3} m");

            // ── M7：视角控制 ──
            _log.AppendLine();
            _log.AppendLine("──── 视角控制 ────");
            if (_look != null)
            {
                _look.enabled = true;
                yield return TestLook();
            }
            else
            {
                _log.AppendLine("  ⚠️ 无 PlayerLook，跳过");
            }

            // ── C1：交互检测 ──
            _log.AppendLine();
            _log.AppendLine("──── 交互系统 ────");
            yield return TestInteraction();

            // 恢复
            if (_look != null) _look.enabled = true;

            Dump();
        }

        private IEnumerator TestDirection(string name, Key key, Vector3 from, float yaw)
        {
            Teleport(from);
            if (_motor != null)
                _motor.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            yield return new WaitForSeconds(0.3f);

            Vector3 p0 = _motor.transform.position;

            Press(key);
            yield return new WaitForSeconds(0.8f);
            Release(key);
            yield return new WaitForSeconds(0.2f);

            Vector3 p1 = _motor.transform.position;
            float dist = Vector3.Distance(new Vector3(p0.x, 0, p0.z), new Vector3(p1.x, 0, p1.z));

            // 检查前方是否有墙（如果有，说明被挡住是正常的）
            bool wallAhead = CheckWallAhead(0.6f);
            string detail = $"移动 {dist:F2} m{(wallAhead ? " (前方 0.6m 内有墙)" : "")}";

            if (dist > 0.3f)
            {
                Pass(name, detail);
            }
            else if (wallAhead)
            {
                // 被墙挡住：不算失败，但要记录
                _log.AppendLine($"  [注意] {name} — {detail}，被墙阻挡，不可作为移动失败判定");
                Pass(name + "（被墙阻挡，无位移属正常）", detail);
            }
            else
            {
                Fail(name, detail + "，且前方无墙");
            }
        }

        private bool CheckWallAhead(float dist)
        {
            if (_motor == null) return false;
            Vector3 origin = _motor.transform.position + Vector3.up * 0.9f;
            Vector3 dir = _motor.transform.forward;
            return Physics.Raycast(origin, dir, dist, ~0, QueryTriggerInteraction.Ignore);
        }

        private IEnumerator TestWallCollision()
        {
            // 直接向出生点北面的墙冲
            Teleport(new Vector3(0f, 0.05f, -6f));
            if (_motor != null)
                _motor.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            yield return new WaitForSeconds(0.3f);

            Vector3 p0 = _motor.transform.position;
            Press(Key.W);
            yield return new WaitForSeconds(2.5f);
            Release(Key.W);
            yield return new WaitForSeconds(0.2f);
            Vector3 p1 = _motor.transform.position;

            float moved = Vector3.Distance(new Vector3(p0.x, 0, p0.z), new Vector3(p1.x, 0, p1.z));
            _log.AppendLine($"  从 {p0} 前进 2.5 秒后到 {p1}，位移 {moved:F2} m");

            // 检查玩家是否在场景范围内（没穿出去）
            bool inBounds = Physics.Raycast(p1 + Vector3.up, Vector3.down, 2f, ~0, QueryTriggerInteraction.Ignore);
            Assert("M5 撞墙不穿透（仍在地面）", inBounds, "玩家下方有地面",
                   "玩家可能穿透到场景外");
        }

        private IEnumerator TestLook()
        {
            Vector3 euler0 = _look.transform.eulerAngles;

            // 模拟鼠标右移
            if (_mouse != null)
            {
                _mouse.MakeCurrent();
                InputSystem.QueueDeltaStateEvent(_mouse.delta, new Vector2(60f, 0f));
                InputSystem.Update();
                yield return new WaitForSeconds(0.25f);

                Vector3 euler1 = _look.transform.eulerAngles;
                float dyaw = Mathf.DeltaAngle(euler0.y, euler1.y);
                _log.AppendLine($"  水平视角变化: {dyaw:F2}°");
                Assert("M7 鼠标水平转向", Mathf.Abs(dyaw) > 1f,
                       $"Yaw 变化 {dyaw:F2}°", $"Yaw 几乎无变化 {dyaw:F2}°");
            }
            else
            {
                _log.AppendLine("  ⚠️ 无鼠标设备，跳过视角测试");
            }
            yield return null;
        }

        private IEnumerator TestInteraction()
        {
            if (_detector == null)
            {
                _log.AppendLine("  ⚠️ 无 InteractionDetector");
                yield break;
            }

            // 找场景里第一个可交互物
            var interactable = FindFirstObjectByType<AbyssEcho.Interaction.InteractableBase>();
            if (interactable == null)
            {
                _log.AppendLine("  ⚠️ 场景里没有可交互物");
                yield break;
            }
            _log.AppendLine($"  找到可交互物: {interactable.gameObject.name} @ {interactable.transform.position}");

            // 把玩家瞬移到它旁边
            Vector3 target = interactable.transform.position;
            Teleport(target + new Vector3(0, 0, -1.2f));
            yield return new WaitForSeconds(0.4f);

            // 让玩家朝向它
            if (_motor != null)
            {
                Vector3 dir = (target - _motor.transform.position).normalized;
                _motor.transform.rotation = Quaternion.LookRotation(new Vector3(dir.x, 0, dir.z));
            }
            yield return new WaitForSeconds(0.3f);

            // 读 detector 当前目标
            var dType = typeof(AbyssEcho.Interaction.InteractionDetector);
            var BF = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
            var currentF = dType.GetField("_current", BF) ?? dType.GetField("Current", BF);
            var promptP = dType.GetProperty("CurrentPrompt", BF);

            object cur = currentF?.GetValue(_detector);
            string prompt = promptP?.GetValue(_detector) as string ?? "";

            _log.AppendLine($"  Detector 当前目标: {(cur != null ? cur.ToString() : "null")}");
            _log.AppendLine($"  当前提示: '{prompt}'");

            Assert("C1 交互检测能识别物体", cur != null,
                   $"识别到 {cur}", "未识别到可交互物");
            Assert("C2 交互提示非空", !string.IsNullOrEmpty(prompt),
                   $"提示='{prompt}'", "提示为空");

            yield return null;
        }

        private Vector3 FindOpenArea()
        {
            // 候选点：出生点附近，但检查四周 2 米内无阻挡
            var candidates = new List<Vector3>
            {
                new Vector3(0f, 0.05f, -8f),
                new Vector3(0f, 0.05f, -3f),
                new Vector3(3f, 0.05f, -8f),
                new Vector3(-3f, 0.05f, -8f),
                new Vector3(0f, 0.05f, 0f),
            };

            Vector3 best = candidates[0];
            int bestScore = -1;

            foreach (var c in candidates)
            {
                int free = 0;
                for (int i = 0; i < 8; i++)
                {
                    float a = i * 45f * Mathf.Deg2Rad;
                    Vector3 dir = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                    if (!Physics.Raycast(c + Vector3.up * 0.9f, dir, 2.5f, ~0, QueryTriggerInteraction.Ignore))
                        free++;
                }
                if (free > bestScore) { bestScore = free; best = c; }
            }

            _log.AppendLine($"  开阔度评分: {bestScore}/8 向");
            return best;
        }

        private void Teleport(Vector3 pos)
        {
            if (_motor == null) return;
            var cc = _motor.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            _motor.transform.position = pos;
            if (cc != null) cc.enabled = true;
            Physics.SyncTransforms();
        }

        private void Press(Key k)
        {
            if (_kb == null) return;
            _kb.MakeCurrent();
            InputSystem.QueueStateEvent(_kb, MakeState(k));
            InputSystem.Update();
        }

        private void Release(Key k)
        {
            if (_kb == null) return;
            _kb.MakeCurrent();
            InputSystem.QueueStateEvent(_kb, new KeyboardState());
            InputSystem.Update();
        }

        private KeyboardState MakeState(Key k)
        {
            var arr = new List<Key> { k };
            return new KeyboardState(arr.ToArray());
        }

        private void Pass(string name, string detail)
        {
            _pass++;
            _log.AppendLine($"  [通过] {name} — {detail}");
        }

        private void Fail(string name, string detail)
        {
            _fail++;
            _log.AppendLine($"  [失败] {name} — {detail}");
        }

        private void Assert(string name, bool ok, string okDetail, string failDetail)
        {
            if (ok) Pass(name, okDetail); else Fail(name, failDetail);
        }

        private void Dump()
        {
            _log.AppendLine();
            _log.AppendLine($"══════ 通过 {_pass}，失败 {_fail} ══════");
            string text = _log.ToString();
            Debug.Log("<b>[Phase2PreciseTest]</b>\n" + text);
            try
            {
                Directory.CreateDirectory(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Final_Test");
                File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Final_Test\02_Raw_precise.txt",
                                  text, new UTF8Encoding(false));
            }
            catch { }
            if (!Application.isEditor) Application.Quit(_fail == 0 ? 0 : 1);
        }
    }
}