using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// S57：第四章终点门 + 门后通道的**真实通关验证**。
    ///
    /// ══════════════════ 为什么必须运行时测 ══════════════════
    ///
    ///   编辑器的静态检测看不到：
    ///     · 门滑动的**运行时过程**（DoorInteractable.Update 每帧插值）
    ///     · 门滑到位后门洞是否真的空出来
    ///     · 玩家 CharacterController 能否真的走过去
    ///
    ///   本测试用真实时间等待门滑到位，再用玩家胶囊逐段检测。
    ///
    /// ══════════════════ 测试序列 ══════════════════
    ///
    ///   T1 门关闭时：玩家被挡（预期挡住）
    ///   T2 解谜 → 门解锁
    ///   T3 开门 → 等门滑到位
    ///   T4 门滑开后：门体位置正确
    ///   T5 门滑开后：门洞完全空出（玩家胶囊能放进去）
    ///   T6 玩家从 G7 走进门洞（CapsuleCast 全程通畅）
    ///   T7 沿通道走到结局触发区（逐段通畅）
    ///   T8 触发区覆盖通道，玩家走进去能触发
    /// </summary>
    public class Ch4ExitFlowTest : MonoBehaviour
    {
        public bool runOnStart = true;

        private readonly StringBuilder _log = new StringBuilder();
        private int _pass, _fail, _unverified;
        private static readonly BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private AbyssEcho.Player.PlayerMotor _motor;
        private CharacterController _cc;
        private AbyssEcho.Horror.PlayerDeathSystem _death;

        private void Start()
        {
            if (runOnStart) StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            // ⚠️ 必须先设章节，再让 ChapterEndingSystem 重新应用配置
            //   （Awake 时 PlayerPrefs 可能是上一次测试留下的别的章节）
            AbyssEcho.LevelGen.ChapterLayout.Current =
                AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter4;
            var ending = FindFirstObjectByType<AbyssEcho.Narrative.ChapterEndingSystem>();
            ending?.GetType().GetMethod("ApplyChapterConfig", BF)?.Invoke(ending, null);
            _log.AppendLine($"[时序修正] 强制章节 = Chapter4");

            yield return new WaitForSeconds(1.2f);

            _log.AppendLine("══════ S57 第四章终点门真实通关验证 ══════");
            _motor = FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
            _cc = _motor != null ? _motor.GetComponent<CharacterController>() : null;
            _death = FindFirstObjectByType<AbyssEcho.Horror.PlayerDeathSystem>();

            if (_motor == null) { _log.AppendLine("❌ 无 PlayerMotor"); Dump(); yield break; }

            var gate = GameObject.Find("Door_HeartGate");
            if (gate == null) { _log.AppendLine("❌ 无 Door_HeartGate"); Dump(); yield break; }

            var door = gate.GetComponent<AbyssEcho.Interaction.DoorInteractable>();
            var valve = GameObject.Find("HeartValve");
            var pulse = valve != null ? valve.GetComponent<AbyssEcho.Puzzle.PulseSyncPuzzle>() : null;

            // 关掉怪物和恐惧视觉，避免干扰
            foreach (var m in FindObjectsByType<AbyssEcho.Horror.MimicStalker>(FindObjectsSortMode.None))
                m.enabled = false;
            var fv = FindFirstObjectByType<AbyssEcho.Horror.FearVisuals>();
            if (fv != null) fv.enabled = false;

            // ══ T1 门关闭时挡住 ══
            _log.AppendLine();
            _log.AppendLine("──── T1 门关闭时 ────");
            _log.AppendLine($"  门位置 = {gate.transform.position}  locked={door.locked}  IsOpen={door.IsOpen}");
            bool blockedClosed = CanPlayerPass(new Vector3(0, 0, 23.5f), new Vector3(0, 0, 26.5f), out string hit1);
            _log.AppendLine($"  玩家从 z=23.5 往北走到 26.5: {(blockedClosed ? $"❌ 被 '{hit1}' 挡住" : "✅ 通畅")}");
            Check("T1 门关闭时阻挡玩家", blockedClosed, blockedClosed ? $"被 {hit1} 挡住（正确）" : "未挡住（错误）");

            // ══ T2 解谜 ══
            _log.AppendLine();
            _log.AppendLine("──── T2 解谜 ────");
            if (pulse != null)
            {
                pulse.GetType().GetMethod("Solve", BF)?.Invoke(pulse, null);
                yield return new WaitForSeconds(0.3f);
                _log.AppendLine($"  心瓣 IsSolved = {pulse.IsSolved}");
                _log.AppendLine($"  门 locked = {door.locked}");
                Check("T2a 解谜后 IsSolved=true", pulse.IsSolved, "");
                Check("T2b 门已解锁 locked=false", !door.locked, $"locked={door.locked}");
            }

            // ══ T3 开门并等待滑到位 ══
            _log.AppendLine();
            _log.AppendLine("──── T3 开门 ────");
            door.OnInteract(gameObject);

            float t = 0f;
            Vector3 closedPos = new Vector3(0f, 1.40f, 24.70f);
            while (t < 5f)
            {
                t += Time.deltaTime;
                if (Vector3.Distance(gate.transform.position, closedPos) > 2.5f) break;
                yield return null;
            }
            yield return new WaitForSeconds(0.5f);

            _log.AppendLine($"  IsOpen = {door.IsOpen}");
            _log.AppendLine($"  门滑到位用时 = {t:F2}s");
            _log.AppendLine($"  门最终位置 = {gate.transform.position}");
            Check("T3a IsOpen=true", door.IsOpen, $"IsOpen={door.IsOpen}");
            Check("T3b 门确实滑开了（位置变化 > 2.5m）",
                  Vector3.Distance(gate.transform.position, closedPos) > 2.5f,
                  $"位移 = {Vector3.Distance(gate.transform.position, closedPos):F2}m");

            // ══ T4/T5 门洞净空 ══
            _log.AppendLine();
            _log.AppendLine("──── T4/T5 门洞净空（门已打开）────");
            Physics.SyncTransforms();

            var gc = gate.GetComponent<Collider>();
            _log.AppendLine($"  门新包围盒 x[{gc.bounds.min.x:F2},{gc.bounds.max.x:F2}] " +
                            $"z[{gc.bounds.min.z:F2},{gc.bounds.max.z:F2}]");
            _log.AppendLine($"  原门洞位置 x[-1.5,1.5] 现在是否空出：");
            for (float x = -1.0f; x <= 1.01f; x += 0.5f)
            {
                bool blocked = Physics.CheckCapsule(
                    new Vector3(x, 0.35f, 24.70f),
                    new Vector3(x, 1.45f, 24.70f),
                    0.35f, ~0, QueryTriggerInteraction.Ignore);
                _log.AppendLine($"    x={x,5:F2} @ z=24.70: {(blocked ? "■ 被挡" : "□ 空")}");
            }

            // 逐点检测门洞平面是否有障碍（用较小半径，只看门体）
            bool holeClear = true;
            for (float x = -1.0f; x <= 1.01f; x += 0.5f)
            {
                var hits = Physics.OverlapCapsule(
                    new Vector3(x, 0.35f, 24.70f), new Vector3(x, 1.45f, 24.70f),
                    0.35f, ~0, QueryTriggerInteraction.Ignore);
                foreach (var h in hits)
                {
                    if (h == null || h.isTrigger) continue;
                    if (h.gameObject.name == "Door_HeartGate") { holeClear = false; }
                }
            }
            Check("T5 门滑开后门体不再占据门洞", holeClear, holeClear ? "门体已移开" : "门体仍在门洞内");

            // ══ T6 玩家走进门洞 ══
            _log.AppendLine();
            _log.AppendLine("──── T6 玩家从 G7 走进门洞 ────");
            bool pass1 = !CanPlayerPass(new Vector3(0, 0, 23.0f), new Vector3(0, 0, 26.5f), out string hit2);
            _log.AppendLine($"  z 23.0 → 26.5: {(pass1 ? "✅ 通畅" : $"❌ 被 '{hit2}' 挡住")}");
            Check("T6 门打开后玩家能穿过门洞", pass1, pass1 ? "" : $"撞到 {hit2}");

            // ══ T7 通道全程 ══
            _log.AppendLine();
            _log.AppendLine("──── T7 沿通道走到触发区 ────");
            var pts = new[]
            {
                new Vector3(0, 0, 26.5f),
                new Vector3(0, 0, 28f),
                new Vector3(0, 0, 30f),
                new Vector3(0, 0, 32f),
                new Vector3(0, 0, 34f),
                new Vector3(0, 0, 36f),
                new Vector3(0, 0, 38f),
                new Vector3(0, 0, 40f),
            };
            bool allClear = true;
            for (int i = 0; i < pts.Length - 1; i++)
            {
                bool blocked = CanPlayerPass(pts[i], pts[i + 1], out string hn);
                _log.AppendLine($"  z {pts[i].z:F0} → {pts[i + 1].z:F0}: {(blocked ? $"❌ '{hn}'" : "✅")}");
                if (blocked) allClear = false;
            }
            Check("T7 通道全程可通行", allClear, allClear ? "" : "有阻挡");

            // ══ T8 触发区 + 真实触发 ══
            _log.AppendLine();
            _log.AppendLine("──── T8 结局触发区 ────");

            // ⚠️ 重新查找实例（之前的 ending 引用可能不是场景里这个）
            var live = FindFirstObjectByType<AbyssEcho.Narrative.ChapterEndingSystem>();
            if (live != null)
            {
                var tcF = typeof(AbyssEcho.Narrative.ChapterEndingSystem).GetField("triggerCenter", BF);
                var tsF = typeof(AbyssEcho.Narrative.ChapterEndingSystem).GetField("triggerSize", BF);
                Vector3 tc = (Vector3)tcF.GetValue(live);
                Vector3 ts = (Vector3)tsF.GetValue(live);
                _log.AppendLine($"  实例 = {live.gameObject.name}");
                _log.AppendLine($"  触发中心={tc} 尺寸={ts}");
                _log.AppendLine($"  Z 范围=[{tc.z - ts.z * 0.5f:F1},{tc.z + ts.z * 0.5f:F1}]");

                bool inside = Mathf.Abs(39f - tc.z) <= ts.z * 0.5f;
                Check("T8a 通道内 z=39 在触发区范围内", inside, $"triggerZ={tc.z:F1}");

                // 真实把玩家挪到触发区中心，等它自己触发
                if (_cc != null) _cc.enabled = false;
                _motor.transform.position = new Vector3(tc.x, 0.05f, tc.z);
                if (_cc != null) _cc.enabled = true;

                float tw = 0f;
                var finP = typeof(AbyssEcho.Narrative.ChapterEndingSystem).GetProperty("IsFinished", BF);
                while (tw < 3f)
                {
                    if (finP != null && (bool)finP.GetValue(live)) break;
                    tw += Time.deltaTime;
                    yield return null;
                }
                bool finished = finP != null && (bool)finP.GetValue(live);
                _log.AppendLine($"  站到触发点 {tw:F2}s 后 IsFinished = {finished}");
                Check("T8b 玩家走到触发点后结局界面真的触发", finished, $"IsFinished={finished}");
            }
            else _log.AppendLine("  ❌ 未找到 ChapterEndingSystem");

            _log.AppendLine();
            Unverified("U1 玩家实际按 E 交互与走路的操作手感");
            Unverified("U2 通道视觉氛围（暗红灯光是否合适）");

            Dump();
        }

        /// <summary>用玩家胶囊检测 A→B 是否被挡。返回 true 表示被挡。</summary>
        private bool CanPlayerPass(Vector3 a, Vector3 b, out string hitName)
        {
            hitName = "";
            const float R = 0.35f, OFF = 1.1f;
            Vector3 dir = (b - a).normalized;
            dir.y = 0;
            float dist = Vector3.Distance(new Vector3(a.x, 0, a.z), new Vector3(b.x, 0, b.z));

            bool blocked = Physics.CapsuleCast(
                new Vector3(a.x, R, a.z), new Vector3(a.x, R + OFF, a.z),
                R, dir, out var hit, dist, ~0, QueryTriggerInteraction.Ignore);
            if (blocked && hit.collider != null) hitName = hit.collider.gameObject.name;
            return blocked;
        }

        private void Check(string name, bool ok, string detail)
        {
            if (ok) { _pass++; _log.AppendLine($"[通过] {name}  {detail}"); }
            else { _fail++; _log.AppendLine($"[失败] {name}  {detail}"); }
        }

        private void Unverified(string n) { _unverified++; _log.AppendLine($"[待人工验收] {n}"); }

        private void Dump()
        {
            _log.AppendLine();
            _log.AppendLine("=====================================");
            _log.AppendLine($"通过 {_pass}，失败 {_fail}，待人工验收 {_unverified}");

            string text = _log.ToString();
            Debug.Log("<b>[Ch4ExitFlowTest]</b>\n" + text);

            // 构建后路径解析不稳定，直接写已知路径 + 多路径尝试
            string[] candidates = {
                @"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Ch4ExitFlowTest.txt",
            };
            bool written = false;
            foreach (var p in candidates)
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(p));
                    File.WriteAllText(p, text, new UTF8Encoding(false));
                    written = true;
                    break;
                }
                catch { }
            }
            if (!written)
            {
                var di = new DirectoryInfo(Application.dataPath);
                for (int hop = 0; hop < 8 && di != null; hop++)
                {
                    string r = Path.Combine(di.FullName, "Reports");
                    if (Directory.Exists(r))
                    {
                        try { File.WriteAllText(Path.Combine(r, "Ch4ExitFlowTest.txt"), text, new UTF8Encoding(false)); written = true; }
                        catch { }
                        if (written) break;
                    }
                    di = di.Parent;
                }
            }

            if (!Application.isEditor) Application.Quit(_fail == 0 ? 0 : 1);
        }
    }
}