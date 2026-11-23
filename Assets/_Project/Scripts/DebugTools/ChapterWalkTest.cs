using System.Collections;
using System.Collections.Generic;
using System.Text;
using AbyssEcho.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// S3A 灰盒流程实测：用**真实角色**沿导航点自动行走，验证第一章能否从头走到尾。
    ///
    /// 为什么必须有这一步：
    ///  布局校验（LayoutValidator）只检查"数据上门洞是否对齐"，
    ///  但无法保证 ① 门洞高度够玩家站立通过 ② 地面无缝 ③ 角色不会被门楣/墙段卡住。
    ///  只有让角色真的走过去，才能证明"玩家能走通"。
    ///
    /// 行走方式：给定一串世界坐标航点，角色自动朝目标前进（模拟按 W + 转向），
    /// 每到达一个航点记一次，超时或卡住则判定失败。
    /// </summary>
    public class ChapterWalkTest : MonoBehaviour
    {
        [Tooltip("是否在启动时自动开始")]
        public bool runOnStart = true;
        [Tooltip("单个航点最长允许的行走时间（秒），超过判定卡住")]
        public float waypointTimeout = 25f;
        [Tooltip("距离航点小于该值视为到达")]
        public float arriveRadius = 0.9f;

        private PlayerMotor _motor;
        private PlayerLook _look;
        private PlayerInputReader _input;
        private Keyboard _keyboard;

        private readonly List<string> _log = new List<string>();
        private int _passed, _failed;

        private void Awake()
        {
            _motor = FindFirstObjectByType<PlayerMotor>();
            _look = FindFirstObjectByType<PlayerLook>();
            _input = FindFirstObjectByType<PlayerInputReader>();
        }

        private void Start()
        {
            if (runOnStart) StartCoroutine(RunAll());
        }

        public IEnumerator RunAll()
        {
            yield return new WaitForSeconds(0.8f);
            _keyboard = Keyboard.current;

            Header("ABYSS ECHO · S3A 灰盒流程实测");
            if (_motor == null || _look == null || _input == null)
            {
                Fail("组件检查", "缺少 PlayerMotor / PlayerLook / PlayerInputReader");
                Finish();
                yield break;
            }
            Pass("组件检查", "玩家组件齐全");

            // 起点稳定性检查：确认玩家站在地面上、没有下坠（灰盒最关键的完整性验证）
            Vector3 p0 = _motor.transform.position;
            yield return new WaitForSeconds(1.0f);
            Vector3 p1 = _motor.transform.position;
            float drop = p0.y - p1.y;
            if (Mathf.Abs(drop) < 0.15f && _motor.IsGrounded)
                Pass("起点站立稳定", $"1 秒内高度变化 {drop:F3}m，已着地（玩家在 y={p1.y:F2}）");
            else
                Fail("起点站立稳定", $"玩家未站稳：高度变化 {drop:F3}m，着地={_motor.IsGrounded}（y={p1.y:F2}）");

            // ── 移动链路诊断：直接测量 1 秒内按 W 的实际位移 ──
            yield return MovementProbe();

            // 自动行走需要朝向由脚本控制：关闭 PlayerLook 的鼠标输入影响
            var waypoints = BuildRoute();
            Info("航点数量", waypoints.Count.ToString());

            // 记录起点
            Vector3 startPos = _motor.transform.position;
            Info("起点", Fmt(startPos));

            int index = 0;
            foreach (var wp in waypoints)
            {
                bool ok = false;
                yield return WalkTo(wp.Position, wp.Name, r => ok = r);
                if (ok) _passed++; else _failed++;
                index++;
            }

            Header("流程结果");
            Info("终点位置", Fmt(_motor.transform.position));
            Info("总行进距离", Vector3.Distance(startPos, _motor.transform.position).ToString("F1") + " m");

            Finish();
        }

        // ────────────────────────── 航点路线 ──────────────────────────

        private class Waypoint
        {
            public string Name;
            public Vector3 Position;
            public Waypoint(string name, Vector3 pos) { Name = name; Position = pos; }
        }

        /// <summary>按第一章流程给出关键航点（每穿过一扇门就设一个）。</summary>
        private List<Waypoint> BuildRoute() => new List<Waypoint>
        {
            // ── A 区：深潜入口 ──
            new Waypoint("A0→A1 出舱",        new Vector3(0f, 0.05f, -37f)),
            new Waypoint("A1 走廊",           new Vector3(0f, 0.05f, -34.5f)),
            new Waypoint("A1→A2 进闸室",      new Vector3(0f, 0.05f, -30f)),
            new Waypoint("A2 闸室中心",       new Vector3(0f, 0.05f, -24.5f)),
            new Waypoint("A2→A3 门禁前后",    new Vector3(0f, 0.05f, -18f)),
            new Waypoint("A3 接待室",         new Vector3(0f, 0.05f, -12f)),
            new Waypoint("A3→B 压力走廊",     new Vector3(0f, 0.05f, -5.5f)),

            // ── B 区：生活区 ──
            new Waypoint("B1 餐厅",           new Vector3(0f, 0.05f, 3f)),
            new Waypoint("B1→B2 通道",        new Vector3(0f, 0.05f, 8.7f)),
            new Waypoint("B2 走廊中段",       new Vector3(0f, 0.05f, 10.9f)),
            new Waypoint("B2 西端",           new Vector3(-10f, 0.05f, 10.9f)),
            new Waypoint("B2 东端",           new Vector3(10f, 0.05f, 10.9f)),
            new Waypoint("B3 医疗室",         new Vector3(11.5f, 0.05f, 17.8f)),
            new Waypoint("B3→B2 返回",        new Vector3(11.5f, 0.05f, 10.9f)),
            new Waypoint("B2→B1 回餐厅",      new Vector3(0f, 0.05f, 8.7f)),

            // ── 电梯：B 区 → C 区 ──
            //   ⚠️ 路线必须沿真实通路：
            //      B1(0,3) → 餐厅通道(0,8.7) → B2 中段(0,10.9) → B2 东端(13,10.9) → 电梯井(15.7,10.9)
            //   直接横穿 B1 会撞到 B1 东北角实墙（实测 East_B1_Full）。
            new Waypoint("B1 中央",           new Vector3(0f, 0.05f, 3f)),
            new Waypoint("B1→B2 餐厅通道",    new Vector3(0f, 0.05f, 8.7f)),
            new Waypoint("B2 走廊中段(去电梯)", new Vector3(0f, 0.05f, 10.9f)),
            new Waypoint("B2 东端(电梯入口)", new Vector3(12.5f, 0.05f, 10.9f)),
            new Waypoint("电梯井",            new Vector3(15.7f, 0.05f, 10.9f)),
            new Waypoint("电梯通道中段",      new Vector3(15.7f, 0.05f, 20f)),
            // ⚠️ 路线要点：电梯通道宽仅 1.4m（X∈[15.0,16.4]），必须先笔直向北穿过
            //    通道顶端的门（Z=28.4），进入前廊后再转向西。若直接斜着走向 C1 会撞西墙。
            new Waypoint("电梯通道顶端(门内)", new Vector3(15.7f, 0.05f, 28.0f)),
            new Waypoint("前廊东端(出门后)",  new Vector3(15.7f, 0.05f, 29.2f)),
            new Waypoint("控制区前廊中段",    new Vector3(8f, 0.05f, 29.2f)),
            // 门口对齐：先站到门洞正下方，再向北穿过（真实玩家的过门方式）
            new Waypoint("C1 南门口(对齐)",   new Vector3(0f, 0.05f, 29.4f)),

            // ── C 区：中央控制室（C1 中心 (0,35)，X[-7,7] Z[30,40]）──
            new Waypoint("C1 主厅南",         new Vector3(0f, 0.05f, 31f)),
            new Waypoint("C1 主厅中心",       new Vector3(0f, 0.05f, 35f)),
            new Waypoint("C2 监控室",         new Vector3(12.4f, 0.05f, 35f)),
            new Waypoint("C2→C1 返回",        new Vector3(0f, 0.05f, 35f)),
            new Waypoint("C3 服务器机房",     new Vector3(-12.4f, 0.05f, 35f)),
            new Waypoint("C5 广播室",         new Vector3(-12.4f, 0.05f, 42f)),
            new Waypoint("C5→C3 返回",        new Vector3(-12.4f, 0.05f, 35f)),
            new Waypoint("C3→C1 返回",        new Vector3(0f, 0.05f, 35f)),

            // ── 第一章终点：压力闸门（C1 北侧）──
            new Waypoint("压力闸门（章节终点）", new Vector3(0f, 0.05f, 42.5f)),
        };

        // ────────────────────────── 行走逻辑 ──────────────────────────

        private IEnumerator WalkTo(Vector3 target, string name, System.Action<bool> onDone)
        {
            Vector3 lastPos = _motor.transform.position;
            float elapsed = 0f;

            // 卡住检测：按"真实时间"而非帧数判断。
            // ⚠️ S3A 实测踩坑：无 VSync 时帧率可达 3000+ fps，单帧位移仅 4.6mm，
            //   若按帧或按毫米阈值判断，会把正常移动误判为卡住。
            //   正确做法：记录"最近一次有效位移"的时间戳，超过 stuckSeconds 没有推进才算卡住。
            const float STUCK_SECONDS = 3.0f;
            const float MOVE_EPSILON = 0.01f;     // 3 秒内累计移动不足 1cm
            float progressWindowStart = Time.time;
            Vector3 progressAnchor = _motor.transform.position;

            while (elapsed < waypointTimeout)
            {
                Vector3 pos = _motor.transform.position;
                Vector3 flatDelta = new Vector3(target.x - pos.x, 0f, target.z - pos.z);

                if (flatDelta.magnitude <= arriveRadius)
                {
                    StopMoving();
                    Pass(name, $"到达 {Fmt(target)}，用时 {elapsed:F1}s");
                    onDone?.Invoke(true);
                    yield break;
                }

                // 朝目标转向
                if (_look != null)
                {
                    float yaw = Mathf.Atan2(flatDelta.x, flatDelta.z) * Mathf.Rad2Deg;
                    _look.SetYaw(yaw);
                }

                // 距离远就奔跑，近处走路
                bool run = flatDelta.magnitude > 4f;
                SetMoving(run);

                // 时间窗口卡住判定
                float windowMove = Vector3.Distance(
                    new Vector3(pos.x, 0f, pos.z),
                    new Vector3(progressAnchor.x, 0f, progressAnchor.z));

                if (windowMove > MOVE_EPSILON)
                {
                    progressAnchor = pos;
                    progressWindowStart = Time.time;
                }
                else if (Time.time - progressWindowStart > STUCK_SECONDS)
                {
                    StopMoving();
                    string diag = $"位置 {Fmt(pos)} → 目标 {Fmt(target)}（剩余 {flatDelta.magnitude:F1}m）" +
                                  $" | 朝向Yaw={_motor.transform.eulerAngles.y:F0}" +
                                  $" | 着地={_motor.IsGrounded}" +
                                  $" | 速度={_motor.CurrentSpeed:F2}" +
                                  $" | 前方障碍={ProbeObstacle(flatDelta)}";
                    Fail(name, $"卡住 {STUCK_SECONDS}s：" + diag);
                    onDone?.Invoke(false);
                    yield break;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            StopMoving();
            Fail(name, $"超时 {waypointTimeout}s 未到达 {Fmt(target)}，当前 {Fmt(_motor.transform.position)}");
            onDone?.Invoke(false);
        }

        private void SetMoving(bool run)
        {
            if (_keyboard == null) return;
            var keys = new List<Key> { Key.W };
            if (run) keys.Add(Key.LeftShift);
            InputState.Change(_keyboard, new KeyboardState(keys.ToArray()));
        }

        private void StopMoving()
        {
            if (_keyboard == null) return;
            InputState.Change(_keyboard, new KeyboardState());
        }

        // ────────────────────────── 输出 ──────────────────────────

        /// <summary>
        /// 直接测量移动链路：静止→按 W 1 秒→松开，报告实际位移、内部速度、deltaTime、帧数。
        /// 用于区分"输入没到" vs "Move 没生效"。
        /// </summary>
        private IEnumerator MovementProbe()
        {
            SetMoving(false);
            yield return new WaitForSeconds(0.3f);

            Vector3 p0 = _motor.transform.position;
            float t0 = Time.time;
            int frames = 0;
            float dtSum = 0f;
            float maxFrameMove = 0f;
            Vector3 prev = p0;

            SetMoving(false);   // 仅走路
            // 手动逐帧注入 + 采样
            while (Time.time - t0 < 1.0f)
            {
                SetMoving(false);
                Vector3 cur = _motor.transform.position;
                float fm = Vector3.Distance(new Vector3(cur.x, 0, cur.z), new Vector3(prev.x, 0, prev.z));
                if (fm > maxFrameMove) maxFrameMove = fm;
                prev = cur;
                dtSum += Time.deltaTime;
                frames++;
                yield return null;
            }
            SetMoving(false);

            Vector3 p1 = _motor.transform.position;
            float dist = Vector3.Distance(new Vector3(p0.x, 0, p0.z), new Vector3(p1.x, 0, p1.z));

            Info("移动诊断·1秒实际位移", $"{dist:F2} m");
            Info("移动诊断·帧数", frames.ToString());
            Info("移动诊断·平均deltaTime", (dtSum / Mathf.Max(1, frames)).ToString("F5"));
            Info("移动诊断·单帧最大位移", maxFrameMove.ToString("F5"));
            Info("移动诊断·内部速度CurrentSpeed", _motor.CurrentSpeed.ToString("F2"));
            Info("移动诊断·着地", _motor.IsGrounded.ToString());
            Info("移动诊断·玩家朝向Yaw", _motor.transform.eulerAngles.y.ToString("F1"));

            if (dist > 1.5f)
                Pass("移动链路", $"1 秒行走 {dist:F2}m（正常应为 ≈2.2m）");
            else
                Fail("移动链路", $"1 秒只移动 {dist:F2}m（期望 ≈2.2m）；frames={frames}, dt={dtSum / Mathf.Max(1, frames):F5}");
        }

        private static string Fmt(Vector3 v) => $"({v.x:F1}, {v.y:F1}, {v.z:F1})";

        /// <summary>沿目标方向做一次短射线，报告撞到了什么（用于诊断卡点）。</summary>
        private string ProbeObstacle(Vector3 dir)
        {
            if (dir.sqrMagnitude < 0.0001f) return "无方向";
            Vector3 origin = _motor.transform.position + Vector3.up * 0.9f;
            if (Physics.Raycast(origin, dir.normalized, out RaycastHit hit, 1.6f))
                return $"{hit.collider.name}@{hit.distance:F2}m";
            return "无遮挡";
        }

        private void Header(string t) => _log.Add("\n===== " + t + " =====");
        private void Info(string k, string v) => _log.Add($"[信息] {k}: {v}");
        private void Pass(string n, string d) { _passed++; _log.Add($"[通过] {n} — {d}"); _log.Add("__PASS__" + n); }
        private void Fail(string n, string d) { _failed++; _log.Add($"[失败] {n} — {d}"); _log.Add("__FAIL__" + n); }

        private void Finish()
        {
            if (_failed == 0 && _passed >= 16) _log.Add("_(不用于统计)_");
            _log.Add(_failed == 0 ? "结论：✅ 第一章灰盒流程走通" : $"结论：❌ 有 {_failed} 个航点失败");

            // 清理内部标记行（供外部解析用，不写入最终报告）
            var clean = new List<string>();
            foreach (var l in _log)
                if (!l.StartsWith("__") && l != "_(不用于统计)_") clean.Add(l);

            string text = string.Join("\n", clean);
            Debug.Log("<b>ABYSS ECHO · S3A 流程实测</b>\n" + text);

            try
            {
                string dir = Application.isEditor
                    ? System.IO.Path.GetFullPath(System.IO.Path.Combine(
                        System.IO.Path.GetDirectoryName(Application.dataPath), "../Reports"))
                    : System.IO.Path.GetDirectoryName(Application.dataPath);
                System.IO.File.WriteAllText(
                    System.IO.Path.Combine(dir ?? ".", "S3A_WalkTestResult.txt"),
                    text, new UTF8Encoding(false));
            }
            catch (System.Exception e) { Debug.LogWarning("写报告失败：" + e.Message); }

            // 构建包中：完成即退出（0 通过 / 1 失败）
            if (!Application.isEditor || Application.isBatchMode)
                Application.Quit(_failed == 0 ? 0 : 1);
        }
    }
}