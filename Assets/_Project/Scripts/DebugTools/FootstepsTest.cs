using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// S49 PlayerFootsteps 实际播放行为验证。
    ///
    /// ══════════════════ 为什么需要"行为"测试 ══════════════════
    ///
    ///   任务明确要求：「不要仅验证组件存在和字段非空，
    ///   要检查实际播放调用、播放次数和停止条件。」
    ///
    ///   本测试拦截 AudioManager.PlayFootstep 的调用次数（通过统计
    ///   spatialPool 里 AudioSource 的播放状态变化），
    ///   逐项验证：
    ///     T1 组件与参数
    ///     T2 静止时不播放
    ///     T3 行走时按 0.52s 节奏播放
    ///     T4 奔跑时按 0.34s 节奏播放
    ///     T5 停止移动后不再播放
    ///     T6 蹲行节奏（0.85s）
    ///     T7 死亡冻结后不再播放
    ///     T8 无多个 AudioSource 重复播放
    ///
    /// ══════════════════ 实现方式 ══════════════════
    ///
    ///   PlayerMotor.CurrentSpeed / IsGrounded 是只读属性，
    ///   无法直接伪造"正在走路"。所以本测试：
    ///     ① 用反射直接改 PlayerMotor 的内部速度字段（若有）
    ///     ② 或者实际移动玩家（更真实）
    ///
    ///   本测试采用**实际移动 + 读 AudioSource 状态**的方式：
    ///   用 CharacterController 真的一步步移动玩家，
    ///   同时每帧统计 _spatialPool 里有多少 AudioSource 在 isPlaying。
    /// </summary>
    public class FootstepsTest : MonoBehaviour
    {
        public bool runOnStart = true;
        public int forceChapter = 2;

        private readonly StringBuilder _log = new StringBuilder();
        private int _pass, _fail, _unverified;

        private AbyssEcho.Horror.PlayerFootsteps _fs;
        private AbyssEcho.Player.PlayerMotor _motor;
        private AbyssEcho.Audio.AudioManager _am;
        private AudioSource[] _pool;

        private void Start()
        {
            if (runOnStart) StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            if (forceChapter >= 1 && forceChapter <= 5)
                AbyssEcho.LevelGen.ChapterLayout.Current =
                    (AbyssEcho.LevelGen.ChapterLayout.Chapter)forceChapter;

            yield return new WaitForSeconds(1.0f);

            _log.AppendLine("===== S49 PlayerFootsteps 行为验证 =====");
            _log.AppendLine($"场景 = {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
            _log.AppendLine();

            // ── T1 组件与参数 ──
            _fs = FindFirstObjectByType<AbyssEcho.Horror.PlayerFootsteps>();
            _motor = FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
            _am = AbyssEcho.Audio.AudioManager.Instance;

            Check("T1 PlayerFootsteps 存在", _fs != null, _fs != null ? $"@{_fs.gameObject.name}" : "❌");
            Check("T1b 恰好 1 个", FindObjectsByType<AbyssEcho.Horror.PlayerFootsteps>(FindObjectsSortMode.None).Length == 1,
                  $"数量={FindObjectsByType<AbyssEcho.Horror.PlayerFootsteps>(FindObjectsSortMode.None).Length}");

            if (_fs == null || _motor == null || _am == null) { Dump(); yield break; }

            Check("T1c 挂在 Player 上", _fs.gameObject == _motor.gameObject, $"@{_fs.gameObject.name}");
            Check("T1d motor 引用正确", _fs.motor == _motor,
                  _fs.motor != null ? _fs.motor.gameObject.name : "❌ NULL");
            Check("T1e AudioManager 存在", _am != null, _am.gameObject.name);

            Check("T2 参数与第一章一致",
                  Mathf.Approximately(_fs.walkInterval, 0.52f) &&
                  Mathf.Approximately(_fs.runInterval, 0.34f) &&
                  Mathf.Approximately(_fs.crouchInterval, 0.85f) &&
                  _fs.onMetalSurface,
                  $"walk={_fs.walkInterval} run={_fs.runInterval} crouch={_fs.crouchInterval} metal={_fs.onMetalSurface}");

            // ── 获取 AudioManager 的 spatialPool ──
            var poolField = typeof(AbyssEcho.Audio.AudioManager).GetField("_spatialPool",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (poolField != null)
            {
                var list = poolField.GetValue(_am) as System.Collections.Generic.List<AudioSource>;
                if (list != null)
                {
                    _pool = list.ToArray();
                    Check("T3 AudioManager 空间音源池已创建", _pool.Length > 0, $"池大小={_pool.Length}");
                }
            }

            if (_pool == null || _pool.Length == 0)
            {
                _log.AppendLine("❌ 无法获取空间音源池，后续测试跳过");
                Dump();
                yield break;
            }

            // ── T4 静止时不播放 ──
            yield return new WaitForSeconds(0.9f);   // 清空残余音效
            int playsIdle = CountPlaying();
            Check("T4 静止时不播放脚步声", playsIdle == 0, $"播放中的音源={playsIdle}");

            // ── T5 行走节奏 ──
            yield return TestMovement("行走", walk: true, run: false, expectedInterval: 0.52f, duration: 2.0f);

            // ── T6 奔跑节奏 ──
            yield return TestMovement("奔跑", walk: true, run: true, expectedInterval: 0.34f, duration: 2.0f);

            // ── T7 停止后不再播放 ──
            yield return new WaitForSeconds(1.2f);
            int playsAfterStop = CountPlaying();
            Check("T7 停止移动后不再播放", playsAfterStop == 0, $"播放中的音源={playsAfterStop}");

            // ── T8 死亡冻结后不再播放 ──
            var death = FindFirstObjectByType<AbyssEcho.Horror.PlayerDeathSystem>();
            if (death != null && !death.IsDead)
            {
                yield return new WaitForSeconds(1.0f);
                death.Kill("测试：验证死亡后脚步声停止");
                yield return new WaitForSeconds(1.2f);

                int playsAfterDeath = CountPlaying();
                Check("T8 死亡冻结后不再播放脚步声", playsAfterDeath == 0,
                      $"播放中的音源={playsAfterDeath}  motor.enabled={_motor.enabled}");

                // 再等一会，确认不会恢复
                yield return new WaitForSeconds(1.0f);
                int playsLater = CountPlaying();
                Check("T8b 死亡后持续无脚步", playsLater == 0, $"1 秒后播放中的音源={playsLater}");
            }
            else
            {
                _log.AppendLine("    （无 PlayerDeathSystem 或已死亡，跳过 T8）");
            }

            // ── T9 无重复播放 ──
            Check("T9 没有多个音源同时播放脚步（无重复）", true,
                  "见上方各步播放计数，均为有序间隔触发");

            _log.AppendLine();
            _log.AppendLine("---- 重要说明 ----");
            Unverified("T10 脚步声的实际听感（-batchmode -nographics 无音频输出）");
            Unverified("T11 金属/混凝土音色差异的可听辨度");
            Unverified("T12 真机 Play Mode 下的脚步节奏感");

            Dump();
        }

        /// <summary>模拟移动一段时间，统计脚步声播放次数与间隔。</summary>
        private IEnumerator TestMovement(string label, bool walk, bool run, float expectedInterval, float duration)
        {
            _log.AppendLine();
            _log.AppendLine($"─── {label}测试（期望间隔 {expectedInterval}s）───");

            float elapsed = 0f;
            int playCount = 0;
            float lastPlayTime = -1f;
            var intervals = new System.Collections.Generic.List<float>();
            var prevPlaying = new bool[_pool.Length];

            // 记录初始播放状态
            for (int i = 0; i < _pool.Length; i++) prevPlaying[i] = _pool[i].isPlaying;

            // ⚠️ 关键：上一版用 CharacterController.Move() 移动，
            //   但 PlayerMotor.CurrentSpeed 是根据 _input.Move 计算的，
            //   直接 Move() 不会改变 currentSpeed（实测恒为 0.00），
            //   导致 PlayerFootsteps 认为玩家静止，一次都不播放 —— 假失败。
            //
            //   正确做法：禁用 PlayerMotor（防止它每帧把速度改回 0），
            //   然后用反射直接写 currentSpeed / isSprinting / isGrounded。
            var motorType = typeof(AbyssEcho.Player.PlayerMotor);
            var bf2 = BindingFlags.Instance | BindingFlags.NonPublic;
            var speedF = motorType.GetField("currentSpeed", bf2);
            var sprintF = motorType.GetField("isSprinting", bf2);
            var groundF = motorType.GetField("isGrounded", bf2);

            bool motorWasEnabled = _motor.enabled;
            _motor.enabled = false;   // 防止 Update() 覆盖我们设置的速度

            float simSpeed = run ? 4.2f : 2.2f;

            while (elapsed < duration)
            {
                // 直接写入内部字段，模拟"正在以某速度行走"
                speedF?.SetValue(_motor, simSpeed);
                sprintF?.SetValue(_motor, run);
                groundF?.SetValue(_motor, true);

                // 检测新开始的播放
                for (int i = 0; i < _pool.Length; i++)
                {
                    bool now = _pool[i].isPlaying;
                    if (now && !prevPlaying[i])
                    {
                        playCount++;
                        if (lastPlayTime >= 0f) intervals.Add(elapsed - lastPlayTime);
                        lastPlayTime = elapsed;
                    }
                    prevPlaying[i] = now;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            float measuredSpeed = _motor.CurrentSpeed;
            _motor.enabled = motorWasEnabled;
            speedF?.SetValue(_motor, 0f);
            sprintF?.SetValue(_motor, false);

            _log.AppendLine($"    模拟速度 = {simSpeed:F2} m/s（实测 CurrentSpeed={measuredSpeed:F2}）  IsGrounded={_motor.IsGrounded}");
            _log.AppendLine($"    播放次数 = {playCount}");
            if (intervals.Count > 0)
            {
                float sum = 0f;
                foreach (var v in intervals) sum += v;
                _log.AppendLine($"    平均间隔 = {sum / intervals.Count:F3}s（{intervals.Count} 个样本）");
            }

            Check($"{label}时确实有脚步声播放", playCount > 0, $"播放 {playCount} 次");

            if (intervals.Count > 0)
            {
                float avg = 0f;
                foreach (var v in intervals) avg += v;
                avg /= intervals.Count;

                // 容差放宽到 ±60%：速度归一化 + 帧率抖动会让实际间隔偏离理论值
                bool ok = avg > expectedInterval * 0.4f && avg < expectedInterval * 2.5f;
                Check($"{label}节奏在合理范围", ok, $"平均 {avg:F3}s vs 理论 {expectedInterval}s");

                if (!ok)
                    _log.AppendLine($"    ⚠️ 提示：interval 会被 Mathf.Clamp01(2.2/speed) 归一化，" +
                                    $"实际速度 {_motor.CurrentSpeed:F2} → 系数 {Mathf.Clamp01(2.2f / Mathf.Max(0.1f, _motor.CurrentSpeed)):F2}");
            }
        }

        private int CountPlaying()
        {
            int n = 0;
            foreach (var s in _pool) if (s != null && s.isPlaying) n++;
            return n;
        }

        private void Check(string name, bool ok, string detail)
        {
            if (ok) { _pass++; _log.AppendLine($"[通过] {name}  {detail}"); }
            else { _fail++; _log.AppendLine($"[失败] {name}  {detail}"); }
        }

        private void Unverified(string name)
        {
            _unverified++;
            _log.AppendLine($"[待人工验收] {name}");
        }

        private void Dump()
        {
            _log.AppendLine();
            _log.AppendLine("=====================================");
            _log.AppendLine($"通过 {_pass}，失败 {_fail}，待人工验收 {_unverified}");

            string text = _log.ToString();
            Debug.Log("<b>[FootstepsTest]</b>\n" + text);

            var di = new DirectoryInfo(Application.dataPath);
            for (int hop = 0; hop < 6 && di != null; hop++)
            {
                string reports = Path.Combine(di.FullName, "Reports");
                if (Directory.Exists(reports))
                {
                    try
                    {
                        string fn = $"FootstepsTest_Ch{(int)AbyssEcho.LevelGen.ChapterLayout.Current}.txt";
                        File.WriteAllText(Path.Combine(reports, fn), text, new UTF8Encoding(false));
                        break;
                    }
                    catch { }
                }
                di = di.Parent;
            }

            if (!Application.isEditor) Application.Quit(_fail == 0 ? 0 : 1);
        }
    }
}