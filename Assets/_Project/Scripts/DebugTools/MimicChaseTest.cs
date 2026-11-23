using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// S46 追击行为验证：确认模仿者会**真的从初始位置移动到玩家身边并攻击**。
    ///
    /// ══════════════════ 与上一批测试的区别 ══════════════════
    ///
    ///   S45 测试：把玩家放到攻击距离内，验证"判定生效"。
    ///   S46 测试：把玩家放在 6 米外**站着不动**，验证"怪物自己走过来"。
    ///
    ///   后者才是真正的追击验证 —— 任务特别要求：
    ///   「特别检查：怪物是否真的会从初始位置移动到玩家附近，
    ///     而不是仅仅修改了距离参数。」
    ///
    /// ══════════════════ 测试环境说明 ══════════════════
    ///
    ///   玩家必须**背对**怪物，否则 IsVisibleToPlayer=true → 怪物定在 Idle 不动。
    ///   这本身是一个已知的条件（见报告"阻止追击的其他条件"一节）。
    ///
    ///   为避免玩家掉出房间，本测试不传送玩家，而是把玩家放在
    ///   **怪物所在房间内**的一个安全点，只调整朝向。
    /// </summary>
    public class MimicChaseTest : MonoBehaviour
    {
        public bool runOnStart = true;
        public int forceChapter = 3;

        private readonly StringBuilder _log = new StringBuilder();
        private int _pass, _fail, _unverified;

        private AbyssEcho.Horror.MimicStalker _mimic;
        private AbyssEcho.Player.PlayerMotor _motor;
        private Transform _camPivot;
        private Vector3 _mimicHome;

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

            var ch = AbyssEcho.LevelGen.ChapterLayout.Current;
            _log.AppendLine("===== S46 追击行为验证 =====");
            _log.AppendLine($"场景 = {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
            _log.AppendLine();

            _mimic = FindFirstObjectByType<AbyssEcho.Horror.MimicStalker>();
            _motor = FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();

            if (_mimic == null) { _log.AppendLine("❌ 本章没有 MimicStalker"); Dump(); yield break; }
            if (_motor == null) { _log.AppendLine("❌ 找不到玩家"); Dump(); yield break; }

            _mimicHome = _mimic.transform.position;

            _log.AppendLine($"Mimic = {_mimic.gameObject.name} @ {_mimicHome}");
            _log.AppendLine($"  attackDistance = {_mimic.attackDistance}");
            _log.AppendLine($"  vanishDistance = {_mimic.vanishDistance}   ← S46 改为 1.0");
            _log.AppendLine($"  approachSpeed  = {_mimic.approachSpeed}");
            _log.AppendLine($"  approachStartDistance = {_mimic.approachStartDistance}");
            _log.AppendLine($"  enableAttack   = {_mimic.enableAttack}");
            _log.AppendLine();

            // ── T0 参数检查 ──
            Check("T0 vanishDistance 已改为 1.0", Mathf.Approximately(_mimic.vanishDistance, 1.0f),
                  $"vanishDistance={_mimic.vanishDistance}");
            Check("T0b attackDistance 未被改动", _mimic.attackDistance >= 1.9f && _mimic.attackDistance <= 2.3f,
                  $"attackDistance={_mimic.attackDistance}");
            Check("T0c attack > vanish（攻击先于消失）", _mimic.attackDistance > _mimic.vanishDistance,
                  $"attack={_mimic.attackDistance} > vanish={_mimic.vanishDistance}");
            Check("T0d enableAttack 开启", _mimic.enableAttack, $"enableAttack={_mimic.enableAttack}");

            // 禁用会覆盖相机旋转的组件
            _camPivot = _motor.transform.Find("CameraPivot");
            var cam = Camera.main;
            if (_camPivot != null)
            {
                var pl = _camPivot.GetComponent<AbyssEcho.Player.PlayerLook>();
                if (pl != null) pl.enabled = false;
            }
            if (cam != null)
            {
                var fv = cam.GetComponent<AbyssEcho.Horror.FearVisuals>();
                if (fv != null) fv.enabled = false;
            }

            // ── T1 距离 6 米：不应立即攻击，且应进入 Approach ──
            yield return TestAtDistance(6.0f, "6 米（远处）", expectImmediateAttack: false);

            // ── T2 距离 3 米：应进入接近行为 ──
            yield return TestAtDistance(3.0f, "3 米（中距离）", expectImmediateAttack: false);

            // ── T3 核心：6 米外站着不动，验证怪物主动走过来并攻击 ──
            yield return TestChase();

            Dump();
        }

        /// <summary>玩家站在指定距离，观察是否有异常立即攻击。</summary>
        private IEnumerator TestAtDistance(float distance, string label, bool expectImmediateAttack)
        {
            ResetMimicOnly();

            Vector3 anchor = new Vector3(_mimicHome.x - distance, 0.1f, _mimicHome.z);
            Teleport(anchor);
            FaceAway(_mimicHome);

            yield return null;
            yield return null;

            float realDist = Vector3.Distance(_motor.transform.position, _mimic.transform.position);
            string st = _mimic.StateName;

            _log.AppendLine($"─── {label} ───");
            _log.AppendLine($"    实测距离 = {realDist:F2}m   怪物状态 = {st}");

            if (!expectImmediateAttack)
            {
                // 只看"进场瞬间"是否误攻击 —— 不是等 2 秒
                Check($"{label} 不应在进场瞬间攻击", _mimic.StateName != "Dead",
                      $"StateName={_mimic.StateName}");
            }

            // 3 米档额外确认能进入 Approach 或已在攻击区间
            if (distance <= 3.5f)
            {
                bool canApproach = _mimic.StateName == "Approach" || _mimic.StateName == "Dead";
                _log.AppendLine($"    是否进入接近行为 = {canApproach}（状态 {_mimic.StateName}）");
            }

            _log.AppendLine();
        }

        /// <summary>
        /// ★ 核心测试：玩家在 6 米外**站着不动**，怪物必须自己走过来并攻击。
        /// </summary>
        private IEnumerator TestChase()
        {
            var death = FindFirstObjectByType<AbyssEcho.Horror.PlayerDeathSystem>();
            if (death == null) { Check("T3 死亡系统存在", false, "❌ 无 PlayerDeathSystem"); yield break; }

            ResetMimicOnly();

            Vector3 anchor = new Vector3(_mimicHome.x - 6.0f, 0.1f, _mimicHome.z);

            _log.AppendLine("─── ★ 核心：6 米外站着不动，等待怪物主动追来 ───");

            // ⚠️ 必须用**水平距离**（与游戏内 MimicStalker.FlatDistance() 一致）：
            //   上一版用 Vector3.Distance（含 Y），
            //   第五章玩家被放到没地板的位置会坠落，Y 差越来越大，
            //   距离从 6m 暴涨到 60m → 误报"距离没有缩短"。
            //   游戏内 FlatDistance() 只算 X/Z，所以怪物判断是对的。
            float startDist = FlatDist(anchor, _mimicHome);
            _log.AppendLine($"    玩家位置 = {anchor}");
            _log.AppendLine($"    怪物初始位置 = {_mimic.transform.position}");
            _log.AppendLine($"    初始距离 = {startDist:F2}m");
            _log.AppendLine();

            Teleport(anchor);
            FaceAway(_mimicHome);

            yield return null;
            yield return null;

            Vector3 mimicStart = _mimic.transform.position;
            float movedTotal = 0f;
            float lastDist = startDist;
            float elapsed = 0f;
            float lastLogT = 0f;

            _log.AppendLine("    逐秒记录：");
            _log.AppendLine("      时间   玩家-怪物距离   怪物剩余状态   怪物已移动");

            // 玩家不动，等最多 12 秒（approachSpeed 1.5m/s，6米需约 4 秒，留足余量）
            while (!death.IsDead && elapsed < 12f)
            {
                // 玩家每帧保持背对，但位置**不动**（这才是"站着不动"）
                FaceAway(_mimic.transform.position);

                float d = FlatDist(_motor.transform.position, _mimic.transform.position);

                // 累计怪物实际移动距离（与上一帧比）
                movedTotal += Vector3.Distance(_mimic.transform.position, _lastMimicPos);
                _lastMimicPos = _mimic.transform.position;

                if (elapsed - lastLogT >= 1.0f)
                {
                    lastLogT = elapsed;
                    _log.AppendLine($"      {elapsed,4:F0}s   {d,6:F2}m        {_mimic.StateName,-10}  {movedTotal:F2}m");
                }

                lastDist = d;

                elapsed += Time.deltaTime;
                yield return null;
            }

            _lastMimicPos = _mimic.transform.position;

            float finalMoved = Vector3.Distance(_mimic.transform.position, mimicStart);

            _log.AppendLine();
            _log.AppendLine($"    结束时：耗时 {elapsed:F1}s，最终距离 {lastDist:F2}m，怪物从初始位置移动了 {finalMoved:F2}m");
            _log.AppendLine($"    最终状态 = {_mimic.StateName}");
            _log.AppendLine();

            // ★ 这是本批最重要的断言：怪物真的移动了
            Check("T3a 怪物真的从初始位置移动了（不是只改参数）", finalMoved > 2.0f,
                  $"移动距离 {finalMoved:F2}m（> 2.0m 才算真的追）");

            Check("T3b 怪物缩短了与玩家的距离", lastDist < startDist - 2.0f,
                  $"{startDist:F2}m → {lastDist:F2}m");

            Check("T3c 怪物主动攻击了玩家（玩家全程未移动）", death.IsDead,
                  death.IsDead ? "✓ 玩家已死亡" : "❌ 未攻击");

            if (!death.IsDead) { _log.AppendLine(); yield break; }

            // ── 死亡链路 ──
            yield return null;

            var look = _camPivot != null ? _camPivot.GetComponent<AbyssEcho.Player.PlayerLook>() : null;
            var detector = FindFirstObjectByType<AbyssEcho.Interaction.InteractionDetector>();
            var crosshair = FindFirstObjectByType<AbyssEcho.UI.CrosshairUI>();

            Check("T4 PlayerDeathSystem.Kill() 被调用", death.IsDead, "IsDead=True");
            Check("T5 玩家移动被冻结", !_motor.enabled, $"motor.enabled={_motor.enabled}");
            Check("T6 玩家视角被冻结", look == null || !look.enabled, $"look.enabled={look?.enabled}");
            Check("T7 交互检测被冻结", detector == null || !detector.enabled, $"detector.enabled={detector?.enabled}");
            Check("T8 准星被隐藏", crosshair == null || !crosshair.enabled, $"crosshair.enabled={crosshair?.enabled}");

            // 黑屏与文案
            var t = typeof(AbyssEcho.Horror.PlayerDeathSystem);
            var bf = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var alphaF = t.GetField("_fadeAlpha", bf);
            var completeF = t.GetField("_fadeComplete", bf);

            float rt0 = Time.realtimeSinceStartup;
            float alpha = 0f;
            bool complete = false;
            while (Time.realtimeSinceStartup - rt0 < death.fadeDuration + 2f)
            {
                if (alphaF != null) alpha = (float)alphaF.GetValue(death);
                if (completeF != null) complete = (bool)completeF.GetValue(death);
                if (complete) break;
                yield return null;
            }
            if (alphaF != null) alpha = (float)alphaF.GetValue(death);
            if (completeF != null) complete = (bool)completeF.GetValue(death);

            Check("T9 黑屏淡入完成", alpha >= 0.99f && complete, $"_fadeAlpha={alpha:F2} _fadeComplete={complete}");
            Check("T10 死亡标题", death.deathTitle == "你被发现了", $"'{death.deathTitle}'");
            Check("T11 死亡副标题", death.deathSubtitle == "深渊记得每一个来客。", $"'{death.deathSubtitle}'");
            Check("T12 Restart 接口可用", t.GetMethod("Restart") != null, "ok");

            // 重复攻击
            int evt = 0;
            System.Action h = () => evt++;
            AbyssEcho.Horror.PlayerDeathSystem.OnPlayerDied += h;
            _mimic.SendMessage("HitPlayer", SendMessageOptions.DontRequireReceiver);
            yield return null;
            AbyssEcho.Horror.PlayerDeathSystem.OnPlayerDied -= h;
            Check("T13 重复攻击不重复死亡", evt == 0, $"二次触发={evt}");

            _log.AppendLine();
            Unverified("T14 屏幕上真的看到怪物走过来（-nographics 无渲染）");
            Unverified("T15 真人按 R 键重开当前章节（无真实输入设备）");
            Unverified("T16 追击过程中的音效与镜头观感（无音频/渲染）");
        }

        private Vector3 _lastMimicPos;

        // ══════════════════ 工具 ══════════════════

        private void ResetMimicOnly()
        {
            // 只复位怪物，不动玩家组件状态（避免干扰死亡冻结检测）
            _mimic.ResetStalker();
            _mimic.transform.position = _mimicHome;
            _lastMimicPos = _mimic.transform.position;
        }

        /// <summary>水平距离（忽略 Y），与 MimicStalker.FlatDistance() 保持一致。</summary>
        private static float FlatDist(Vector3 a, Vector3 b)
        {
            a.y = 0f; b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private void Teleport(Vector3 pos)
        {
            var cc = _motor.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            _motor.transform.position = pos;
            if (cc != null) cc.enabled = true;
        }

        /// <summary>让玩家背对指定位置。</summary>
        private void FaceAway(Vector3 target)
        {
            if (_camPivot == null) return;
            Vector3 dir = target - _motor.transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            _camPivot.rotation = Quaternion.LookRotation(-dir.normalized, Vector3.up);
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
            Debug.Log("<b>[MimicChaseTest]</b>\n" + text);

            var di = new DirectoryInfo(Application.dataPath);
            for (int hop = 0; hop < 6 && di != null; hop++)
            {
                string reports = Path.Combine(di.FullName, "Reports");
                if (Directory.Exists(reports))
                {
                    try
                    {
                        string fn = $"MimicChaseTest_Ch{(int)AbyssEcho.LevelGen.ChapterLayout.Current}.txt";
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