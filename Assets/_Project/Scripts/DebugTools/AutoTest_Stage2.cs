using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using AbyssEcho.Player;
using AbyssEcho.Tools;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// S2 自动化验收测试。
    ///
    /// 设计原因：
    ///  - 人工"看起来正常"不能证明"移动速度正确、蹲下会卡住、体力会耗尽"；
    ///  - 本脚本通过代码模拟真实输入（直接驱动 Input System 的 Keyboard/Mouse 设备状态），
    ///    对每一项验收标准做**可量化的断言**，并输出 PASS/FAIL 报告。
    ///
    /// 运行方式：
    ///  1) 场景中 Player 所在物体上挂 AutoTest_Stage2 组件（或运行时通过菜单 S2 创建）；
    ///  2) Play；
    ///  3) 结果写入 Console 与 Reports/S2_AutoTestResult.txt。
    /// </summary>
    public class AutoTest_Stage2 : MonoBehaviour
    {
        [Tooltip("是否在 Start 时自动运行")]
        public bool runOnStart = true;
        [Tooltip("是否把结果写入工程 Reports 目录")]
        public bool writeReport = true;

        private readonly List<string> _log = new List<string>();
        private int _pass;
        private int _fail;

        private PlayerMotor _motor;
        private PlayerLook _look;
        private PlayerInputReader _input;
        private FlashlightSystem _flash;
        private Keyboard _keyboard;
        private Mouse _mouse;

        private void Start()
        {
            if (runOnStart) StartCoroutine(RunAll());
        }

        private void Awake()
        {
            _motor = FindFirstObjectByType<PlayerMotor>();
            _look = FindFirstObjectByType<PlayerLook>();
            _input = FindFirstObjectByType<PlayerInputReader>();
            _flash = FindFirstObjectByType<FlashlightSystem>();
        }

        public IEnumerator RunAll()
        {
            yield return new WaitForSeconds(0.5f);

            _log.Clear();
            _pass = 0;
            _fail = 0;

            Header("ABYSS ECHO · S2 自动化验收测试");
            Info("Unity", Application.unityVersion);
            Info("GPU", SystemInfo.graphicsDeviceName);

            if (_motor == null || _look == null || _input == null)
            {
                Fail("组件存在性", "缺少 PlayerMotor / PlayerLook / PlayerInputReader 之一");
                Finish();
                yield break;
            }
            Pass("组件存在性", "PlayerMotor / PlayerLook / PlayerInputReader 均已挂载");

            _keyboard = Keyboard.current;
            _mouse = Mouse.current;
            if (_keyboard == null) Fail("输入设备", "未检测到键盘");
            else Pass("输入设备", "键盘已连接");
            if (_mouse == null) Fail("输入设备", "未检测到鼠标");
            else Pass("输入设备", "鼠标已连接");

            if (_keyboard == null || _mouse == null) { Finish(); yield break; }

            // ── 链路自检：确认输入注入通路可用（失败则后续断言没有意义）──
            Press(Key.W);
            yield return null;
            yield return null;
            bool rawW = _keyboard.wKey.isPressed;
            Vector2 readerMove = _input.Move;
            bool linkOk = rawW && readerMove.y > 0.5f;
            Assert("T00 输入链路自检", linkOk,
                   $"键盘按下可被 PlayerInputReader 读到（Move.y={readerMove.y:F2}）",
                   $"输入链路异常：wKey.isPressed={rawW}，Move.y={readerMove.y:F2}");
            Release(Key.W);
            yield return null;

            // ── 测试 1：静止时不下沉、位置稳定 ──
            yield return new WaitForSeconds(1.0f);
            Vector3 restPos = _motor.transform.position;
            yield return new WaitForSeconds(0.6f);
            float drift = Vector3.Distance(restPos, _motor.transform.position);
            Assert("T01 站立不下沉", drift < 0.05f, $"站立 0.6 秒位移 {drift:F4} m（应 < 0.05）", $"站立时漂移 {drift:F4} m，角色可能在缓慢下沉");

            // ── 测试 2：WASD 移动 ──
            yield return MoveTest(Key.W, "T02 前进(W)");
            yield return MoveTest(Key.S, "T03 后退(S)");
            yield return MoveTest(Key.A, "T04 左移(A)");
            yield return MoveTest(Key.D, "T05 右移(D)");

            // ── 测试 3：奔跑比走路快 ──
            float walkSpeed = 0f, runSpeed = 0f;
            yield return MeasureSpeed(false, v => walkSpeed = v);
            yield return MeasureSpeed(true, v => runSpeed = v);
            Assert("T06 奔跑快于行走", runSpeed > walkSpeed * 1.5f,
                   $"行走 {walkSpeed:F2} m/s，奔跑 {runSpeed:F2} m/s（奔跑应 > 行走 × 1.5）",
                   $"奔跑速度异常：行走 {walkSpeed:F2}，奔跑 {runSpeed:F2}");

            // ── 测试 4：体力消耗 ──
            float staminaBefore = _motor.CurrentStamina;
            yield return HoldRun(2.0f);
            float staminaAfter = _motor.CurrentStamina;
            Assert("T07 奔跑消耗体力", staminaAfter < staminaBefore - 0.5f,
                   $"奔跑 2 秒体力 {staminaBefore:F2} → {staminaAfter:F2}",
                   $"体力未消耗：{staminaBefore:F2} → {staminaAfter:F2}");

            // ── 测试 5：蹲下降低高度与速度 ──
            float hStand = _motor.GetComponent<CharacterController>().height;
            Press(Key.LeftCtrl);
            yield return new WaitForSeconds(0.5f);
            float hCrouch = _motor.GetComponent<CharacterController>().height;
            Assert("T08 蹲下降低碰撞高度", hCrouch < hStand - 0.3f,
                   $"站立高度 {hStand:F2} → 蹲下 {hCrouch:F2}", $"蹲下高度未改变：{hCrouch:F2}");
            Assert("T09 蹲下状态正确", _motor.IsCrouching, "IsCrouching = true", "IsCrouching 仍为 false");

            float crouchSpeed = 0f;
            yield return MeasureSpeed(false, v => crouchSpeed = v);
            Assert("T10 蹲下移动更慢", crouchSpeed < walkSpeed * 0.8f,
                   $"蹲行 {crouchSpeed:F2} m/s < 行走 {walkSpeed:F2} m/s",
                   $"蹲行速度未减慢：{crouchSpeed:F2} vs 行走 {walkSpeed:F2}");

            // T10 让角色向前蹲行，可能停在低矮通道下方。
            // 必须先把角色移回开阔地，再测试"头顶有障碍时不能起身"的正面行为。
            // （T11 初版失败即因为此——那是功能正确、测试场景不对，属于测试缺陷。）
            Teleport(new Vector3(0f, 0.05f, -6f));
            yield return new WaitForSeconds(0.3f);

            Release(Key.LeftCtrl);
            yield return new WaitForSeconds(0.6f);
            float hStandAgain = _motor.GetComponent<CharacterController>().height;
            Assert("T11 开阔地起身恢复高度", Mathf.Abs(hStandAgain - hStand) < 0.05f,
                   $"起身高度恢复为 {hStandAgain:F2}", $"起身后高度 {hStandAgain:F2}，应为 {hStand:F2}");

            // ── 测试：低矮通道下不能起身（正面验证头顶检测）──
            yield return HeadroomBlockTest(hStand);

            // ── 测试 6：鼠标视角 ──
            InjectMouseDelta(new Vector2(50f, 0f));
            yield return null;
            bool mouseLinkOk = Mathf.Abs(_input.Look.x) > 1f;
            Assert("T11d 鼠标输入链路自检", mouseLinkOk,
                   $"鼠标增量可被读到（Look.x={_input.Look.x:F2}）",
                   $"鼠标链路异常：Look.x={_input.Look.x:F2}");
            yield return null;

            float yawBefore = _motor.transform.eulerAngles.y;
            yield return MouseMove(new Vector2(120f, 0f), 0.35f);
            float yawAfter = _motor.transform.eulerAngles.y;
            float yawDelta = Mathf.DeltaAngle(yawBefore, yawAfter);
            Assert("T12 鼠标水平转向", Mathf.Abs(yawDelta) > 1f,
                   $"Yaw 变化 {yawDelta:F2}°", "鼠标水平移动未改变朝向");

            Transform camT = _look.GetComponent<PlayerLook>() != null ? _motor.transform.Find("CameraPivot/Main Camera") : null;
            if (camT != null)
            {
                float pitchBefore = camT.localEulerAngles.x;
                yield return MouseMove(new Vector2(0f, 200f), 0.35f);
                float pitchAfter = NormalizeAngle(camT.localEulerAngles.x);
                Assert("T13 鼠标垂直俯仰", Mathf.Abs(NormalizeAngle(pitchAfter - pitchBefore)) > 1f,
                       $"Pitch 变化 {NormalizeAngle(pitchAfter - pitchBefore):F2}°", "鼠标垂直移动未改变俯仰");

                // 极限俯仰夹紧
                yield return MouseMove(new Vector2(0f, 4000f), 0.6f);
                float clampedPitch = NormalizeAngle(camT.localEulerAngles.x);
                Assert("T14 俯仰角夹紧", clampedPitch <= 86f && clampedPitch >= -86f,
                       $"极限俯仰后 Pitch = {clampedPitch:F2}°（应 ≤ 85）", $"俯仰未夹紧：{clampedPitch:F2}°");
                yield return MouseMove(new Vector2(0f, -4000f), 0.6f);
            }

            // ── 测试 7：手电筒 ──
            if (_flash != null)
            {
                bool wasOn = _flash.IsOn;
                Press(Key.F);
                yield return new WaitForSeconds(0.25f);
                Release(Key.F);
                yield return new WaitForSeconds(0.15f);
                Assert("T15 手电筒开启", _flash.IsOn != wasOn,
                       $"手电筒状态 {'开' + " → " + (_flash.IsOn ? "关" : "开")}", "按 F 未切换手电筒");
            }
            else
            {
                Fail("T15 手电筒", "场景中未找到 FlashlightSystem");
            }

            // ── 测试 8：移动碰撞（不能穿墙） ──
            yield return WallCollisionTest();

            Finish();
        }

        // ────────────────────────── 测试辅助 ──────────────────────────

        private IEnumerator MoveTest(Key key, string name)
        {
            Vector3 before = _motor.transform.position;
            Press(key);
            yield return new WaitForSeconds(0.6f);
            Release(key);
            yield return new WaitForSeconds(0.15f);
            Vector3 after = _motor.transform.position;
            float dist = Vector3.Distance(new Vector3(before.x, 0, before.z), new Vector3(after.x, 0, after.z));
            Assert(name, dist > 0.3f, $"移动距离 {dist:F2} m（应 > 0.3）", $"按下 {key} 后几乎未移动：{dist:F3} m");
        }

        private IEnumerator MeasureSpeed(bool run, Action<float> onResult)
        {
            // 先回到视野中央的稳定状态，再逐步加速
            Vector3 before = _motor.transform.position;
            if (run) Press(Key.LeftShift);
            Press(Key.W);
            yield return new WaitForSeconds(0.8f);   // 让加速完成

            Vector3 p0 = _motor.transform.position;
            yield return new WaitForSeconds(0.5f);
            Vector3 p1 = _motor.transform.position;

            Release(Key.W);
            if (run) Release(Key.LeftShift);
            yield return new WaitForSeconds(0.3f);

            float speed = new Vector3(p1.x - p0.x, 0f, p1.z - p0.z).magnitude / 0.5f;
            onResult?.Invoke(speed);

            // 防止测试过程中角色跑出边界：若离起点过远，则拉回
            if (Vector3.Distance(_motor.transform.position, before) > 14f)
                Teleport(new Vector3(0f, 0.05f, -6f));
        }

        private IEnumerator HoldRun(float seconds)
        {
            Press(Key.LeftShift);
            Press(Key.W);
            yield return new WaitForSeconds(seconds);
            Release(Key.W);
            Release(Key.LeftShift);
            yield return new WaitForSeconds(0.1f);
        }

        private IEnumerator HeadroomBlockTest(float standHeight)
        {
            // 低矮通道中心位于 (0, 8)，净高 1.4m，顶板底面约 y=1.6
            Teleport(new Vector3(0f, 0.05f, 8f));
            Press(Key.LeftCtrl);
            yield return new WaitForSeconds(0.4f);

            bool crouchedUnder = _motor.IsCrouching;
            Release(Key.LeftCtrl);
            yield return new WaitForSeconds(0.6f);

            float h = _motor.GetComponent<CharacterController>().height;
            Assert("T11b 低矮通道内不能起身", crouchedUnder && h < standHeight - 0.2f,
                   $"通道内保持蹲下，高度 {h:F2}（站立需 {standHeight:F2}）",
                   $"在低矮通道内错误起身：高度 {h:F2}");

            // 离开通道，确认可以正常起身
            Teleport(new Vector3(0f, 0.05f, -6f));
            yield return new WaitForSeconds(0.6f);
            float hAfter = _motor.GetComponent<CharacterController>().height;
            Assert("T11c 离开通道后恢复站立", Mathf.Abs(hAfter - standHeight) < 0.05f,
                   $"离开通道后高度 {hAfter:F2}", $"离开通道后高度仍为 {hAfter:F2}");
        }

        private IEnumerator MouseMove(Vector2 delta, float duration)
        {
            float t = 0f;
            Vector2 perFrame = delta * (Time.deltaTime / Mathf.Max(0.01f, duration));
            while (t < duration)
            {
                InjectMouseDelta(perFrame);
                t += Time.deltaTime;
                yield return null;
            }
            // 清尾：让 delta 归零
            InjectMouseDelta(Vector2.zero);
            yield return new WaitForSeconds(0.1f);
        }

        private IEnumerator WallCollisionTest()
        {
            // ⚠️ 前置条件：本测试依赖"前进 = 朝北(+Z)"。
            // 之前的鼠标测试已经把角色旋转过（实测 Yaw 变化约 169°），
            // 若不复位，"按 W" 会走向侧向，从而撞到别的物体——初版因此误判（S2 实测发现）。
            ResetFacingAndPosition(new Vector3(-14f, 0.05f, 15f), 0f);
            yield return new WaitForSeconds(0.6f);   // 等待落地并稳定

            Press(Key.LeftShift);
            Press(Key.W);
            yield return new WaitForSeconds(2.5f);   // 4.2m/s × 2.5s ≈ 10.5m，足以撞上 5m 外的墙
            Release(Key.W);
            Release(Key.LeftShift);
            yield return new WaitForSeconds(0.3f);

            Vector3 p = _motor.transform.position;
            bool blockedByWall = p.z > 17.5f && p.z < 19.8f;   // 已到达墙前但未穿透
            bool insideWall = p.z >= 19.8f;

            Assert("T16 撞墙不穿透", !insideWall && blockedByWall,
                   $"冲到墙前停在 Z={p.z:F2}（墙面内表面 Z=19.8，角色半径 0.32 → 应停在约 19.48）",
                   insideWall ? $"玩家穿过了墙面！Z={p.z:F2}" : $"玩家未到达墙前，Z={p.z:F2}（路线可能被其它物体阻挡）");

            // 额外验证：贴墙后侧向移动仍被限制（不会挤入墙内）
            Press(Key.D);
            yield return new WaitForSeconds(0.8f);
            Release(Key.D);
            yield return new WaitForSeconds(0.2f);
            float z2 = _motor.transform.position.z;
            Assert("T16b 贴墙侧移不穿模", z2 < 19.8f,
                   $"贴墙侧移后 Z={z2:F2} 仍小于墙面", $"侧移后穿入墙面：Z={z2:F2}");

            Teleport(new Vector3(0f, 0.05f, -6f));
            yield return new WaitForSeconds(0.4f);
        }

        private void Teleport(Vector3 pos)
        {
            var cc = _motor.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            _motor.transform.position = pos;
            if (cc != null) cc.enabled = true;
        }

        /// <summary>同时复位位置与朝向（朝向变更需要同步 PlayerLook 内部的 yaw，否则会被下一帧覆盖）。</summary>
        private void ResetFacingAndPosition(Vector3 pos, float yawDegrees)
        {
            Teleport(pos);
            _motor.transform.rotation = Quaternion.Euler(0f, yawDegrees, 0f);
            if (_look != null) _look.SetYaw(yawDegrees);
        }

        // ────────────────────────── 输入注入 ──────────────────────────
        //
        // 使用 Input System 官方推荐的 QueueStateEvent 注入真实设备状态事件，
        // 而不是直接改内部状态（InputState 在 1.6.1 中非公开 API）。
        // 参考：com.unity.inputsystem 文档 Events.md
        //   InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.LeftCtrl, Key.A));

        private readonly HashSet<Key> _heldKeys = new HashSet<Key>();

        /// <summary>
        /// 按键注入（按住 / 松开）。
        ///
        /// 为什么用 InputState.Change 而不是 QueueStateEvent：
        ///  S2 实测发现 QueueStateEvent 在独立 exe 中对键盘无效（wKey.isPressed 仍为 false），
        ///  而 InputState.Change 是 Input System 官方测试框架（InputTestFixture）采用的方式，
        ///  它会直接写入状态内存并触发状态变化监视器，InputAction 能可靠收到。
        /// </summary>
        private void Press(Key key)
        {
            _heldKeys.Add(key);
            ApplyKeyboardState();
        }

        private void Release(Key key)
        {
            _heldKeys.Remove(key);
            ApplyKeyboardState();
        }

        private void ReleaseAll()
        {
            _heldKeys.Clear();
            ApplyKeyboardState();
        }

        private void ApplyKeyboardState()
        {
            if (_keyboard == null) return;
            var keys = new Key[_heldKeys.Count];
            _heldKeys.CopyTo(keys);
            InputState.Change(_keyboard, new KeyboardState(keys));
        }

        /// <summary>
        /// 鼠标增量注入。
        ///
        /// 注意：Mouse.delta 是"增量型"控件，每帧只反映当帧移动量。
        /// 单纯 Change(delta, v) 在部分路径下会被帧末重置为 0，
        /// 因此这里同时写入 position 以构成完整的 MouseState，确保 delta 被保留到本帧读取。
        /// </summary>
        private void InjectMouseDelta(Vector2 delta)
        {
            if (_mouse == null) return;

            // 说明（S2 实测结论）：
            //  Mouse.delta 是增量型控件，Mouse.OnNextUpdate() 会在每次输入更新后把它清零，
            //  因此必须在"下一次输入更新之前"把增量送入系统。
            //  这里用 QueueStateEvent 排队完整 MouseState，让 Input System 在本帧的
            //  动态更新中应用它（比直接 InputState.Change 更贴近真实设备事件）。
            InputSystem.QueueStateEvent(_mouse, new MouseState
            {
                position = _mousePosition,
                delta = delta,
                scroll = Vector2.zero
            });
        }

        private Vector2 _mousePosition = new Vector2(640f, 360f);

        // ────────────────────────── 结果输出 ──────────────────────────

        private void Header(string text) => _log.Add("\n===== " + text + " =====");
        private void Info(string k, string v) => _log.Add($"[信息] {k}: {v}");

        private void Pass(string name, string detail)
        {
            _pass++;
            _log.Add($"[通过] {name} — {detail}");
        }

        private void Fail(string name, string detail)
        {
            _fail++;
            _log.Add($"[失败] {name} — {detail}");
        }

        private void Assert(string name, bool cond, string okDetail, string failDetail)
        {
            if (cond) Pass(name, okDetail);
            else Fail(name, failDetail);
        }

        private void Finish()
        {
            _log.Add($"\n===== 总结：通过 {_pass} 项，失败 {_fail} 项 =====");
            _log.Add(_fail == 0 ? "结论：✅ S2 自动化验收通过" : "结论：❌ 存在失败项，请勿进入 S3");

            string text = string.Join("\n", _log);
            Debug.Log("<b>ABYSS ECHO · S2 自动化验收结果</b>\n" + text);

            if (!writeReport) return;

            string written = null;

            // 1) 打包版：写到 exe 所在目录（构建测试包时最方便读取）
            if (!Application.isEditor)
            {
                try
                {
                    string exeDir = System.IO.Path.GetDirectoryName(Application.dataPath);
                    string path = System.IO.Path.Combine(exeDir ?? ".", "S2_AutoTestResult.txt");
                    System.IO.File.WriteAllText(path, text, new UTF8Encoding(false));
                    written = path;
                }
                catch (Exception e) { Debug.LogWarning("写入 exe 目录失败：" + e.Message); }
            }
            else
            {
                // 2) 编辑器内：写到工程外的 Reports 目录
                try
                {
                    string projectRoot = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Application.dataPath));
                    string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(projectRoot, "../Reports/S2_AutoTestResult.txt"));
                    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                    System.IO.File.WriteAllText(path, text, new UTF8Encoding(false));
                    written = path;
                }
                catch (Exception e) { Debug.LogWarning("写入 Reports 失败：" + e.Message); }
            }

            if (written != null) Debug.Log("S2 测试结果已写入：" + written);

            // 构建测试包时：完成后自动退出并返回退出码
            if (!Application.isEditor || Application.isBatchMode)
                Application.Quit(_fail == 0 ? 0 : 1);
        }

        private static float NormalizeAngle(float a)
        {
            a %= 360f;
            if (a > 180f) a -= 360f;
            return a;
        }
    }
}