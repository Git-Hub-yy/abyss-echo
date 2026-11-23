using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// S55b：和平模式测试（修正版）。
    ///
    /// ══════════════════ 上一版测试为什么没发现 bug ══════════════════
    ///
    ///   上一版把怪物**直接传到玩家身边 1 米**，然后等 PlayerDeathSystem 的反应。
    ///
    ///   但 MimicStalker.UpdateApproach() 有**两处**攻击判定：
    ///     ① 方法开头：进入时距离已 < attackDistance → 判定
    ///     ② MoveTowardPlayer() 之后：再判一次
    ///
    ///   把怪物贴到身上时，① 立刻命中并穿过和平模式检查（被拦住了，正确），
    ///   然后 return，**永远走不到 ②**。
    ///   而我只给 ① 加了检查，② 没加。
    ///
    ///   所以测试全绿，玩家实际游玩时怪物从远处走过来，
    ///   在 ② 的补判里杀了人 —— 就是"没看到怪物就死了"。
    ///
    /// ══════════════════ 本版测试怎么改 ══════════════════
    ///
    ///   ① 把怪物放到**远处**（approachStartDistance 外），让它真的走过来
    ///   ② 一直等到玩家死亡 或 超时
    ///   ③ 这样会自然经过 ①→移动→② 的完整路径
    ///
    ///   同时保留"贴脸"测试作为补充，两条路径都覆盖。
    /// </summary>
    public class PeaceModeTest2 : MonoBehaviour
    {
        public bool runOnStart = true;
        public int forceChapter = 3;

        private readonly StringBuilder _log = new StringBuilder();
        private int _pass, _fail, _unverified;
        private static readonly BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private PeaceModeManager _pm;
        private AbyssEcho.Horror.PlayerDeathSystem _death;
        private AbyssEcho.Player.PlayerMotor _motor;

        private void Start()
        {
            if (runOnStart) StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            AbyssEcho.LevelGen.ChapterLayout.Current =
                (AbyssEcho.LevelGen.ChapterLayout.Chapter)forceChapter;
            yield return new WaitForSeconds(1.2f);

            _log.AppendLine("══════ S55b 和平模式测试（覆盖完整攻击路径）══════");
            _log.AppendLine($"章节 = {forceChapter}");
            _log.AppendLine();

            _pm = FindFirstObjectByType<PeaceModeManager>();
            _death = FindFirstObjectByType<AbyssEcho.Horror.PlayerDeathSystem>();
            _motor = FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();

            if (_pm == null || _death == null || _motor == null)
            {
                _log.AppendLine("❌ 缺组件");
                Dump();
                yield break;
            }

            var mimics = FindObjectsByType<AbyssEcho.Horror.MimicStalker>(FindObjectsSortMode.None);
            var monsters = FindObjectsByType<AbyssEcho.Monster.MonsterAI>(FindObjectsSortMode.None);
            _log.AppendLine($"MimicStalker={mimics.Length}  MonsterAI={monsters.Length}");
            _log.AppendLine();

            // ══ A. 和平模式开启：怪物从远处走来，不应致死 ══
            _log.AppendLine("──── A. 和平模式开启：怪物从远处走来 ────");
            _pm.SetEnabled(true);
            yield return null;
            ResetDeath();

            if (mimics.Length > 0)
                yield return TestMimicFromFar(mimics[0], expectDeath: false);

            // ══ B. 和平模式关闭：同样布置，应该会死 ══
            _log.AppendLine();
            _log.AppendLine("──── B. 和平模式关闭：同样布置，应该会死 ────");
            _pm.SetEnabled(false);
            yield return null;
            ResetDeath();

            if (mimics.Length > 0)
                yield return TestMimicFromFar(mimics[0], expectDeath: true);

            // ══ C. 贴脸测试（覆盖第一处判定）══
            _log.AppendLine();
            _log.AppendLine("──── C. 和平模式开启：怪物直接贴脸（覆盖第一处判定）────");
            _pm.SetEnabled(true);
            yield return null;
            ResetDeath();

            if (mimics.Length > 0)
                yield return TestMimicAdjacent(mimics[0]);

            // ══ D. MonsterAI ══
            if (monsters.Length > 0)
            {
                _log.AppendLine();
                _log.AppendLine("──── D. MonsterAI ────");
                _pm.SetEnabled(true);
                yield return null;
                ResetDeath();
                yield return TestMonster(monsters[0]);

                _pm.SetEnabled(false);
                yield return null;
                ResetDeath();
                yield return TestMonsterKills(monsters[0]);
            }

            _pm.SetEnabled(false);
            yield return null;

            _log.AppendLine();
            Unverified("U1 真人按 M 键的完整试玩");

            Dump();
        }

        // ── 怪物从远处走来（覆盖 ①移动② 的完整路径）──
        private IEnumerator TestMimicFromFar(AbyssEcho.Horror.MimicStalker m, bool expectDeath)
        {
            var t = typeof(AbyssEcho.Horror.MimicStalker);

            // 重置怪物状态为 Idle，位置退到 player + 12 米（在 approachStartDistance 内、attackDistance 外）
            var stateF = t.GetField("_state", BF);
            if (stateF != null) stateF.SetValue(m, System.Enum.Parse(stateF.FieldType, "Idle"));

            var visibleTimerF = t.GetField("_visibleTimer", BF);
            visibleTimerF?.SetValue(m, 0f);

            Vector3 p = _motor.transform.position;
            Vector3 far = new Vector3(p.x, p.y, p.z - 12f);   // 玩家南侧 12 米
            m.transform.position = far;

            // 玩家朝北（背对南边的怪物），这样怪物会追过来
            _motor.transform.rotation = Quaternion.Euler(0f, 0f, 0f);

            yield return new WaitForSeconds(0.2f);

            float startDist = Vector3.Distance(m.transform.position, _motor.transform.position);
            _log.AppendLine($"  {m.gameObject.name} 起始距离 = {startDist:F1}m  预期死亡={expectDeath}");

            float elapsed = 0f;
            float minDist = startDist;

            while (elapsed < 15f && !_death.IsDead)
            {
                elapsed += Time.deltaTime;
                float d = Vector3.Distance(m.transform.position, _motor.transform.position);
                if (d < minDist) minDist = d;

                // 每 2 秒记录一次
                if (Mathf.Repeat(elapsed, 2f) < Time.deltaTime)
                    _log.AppendLine($"    {elapsed:F0}s  距离={d:F1}m  状态={m.StateName}");

                yield return null;
            }

            _log.AppendLine($"  结束: 用时={elapsed:F1}s  最近距离={minDist:F1}m  玩家IsDead={_death.IsDead}");

            if (expectDeath)
                Check($"[{m.gameObject.name}] 和平模式关闭时从远处走来会致死", _death.IsDead,
                      $"IsDead={_death.IsDead}, 最近={minDist:F1}m");
            else
                Check($"[{m.gameObject.name}] 和平模式开启时从远处走来不致死", !_death.IsDead,
                      $"IsDead={_death.IsDead}, 最近={minDist:F1}m");
        }

        // ── 贴脸（覆盖第一处判定）──
        private IEnumerator TestMimicAdjacent(AbyssEcho.Horror.MimicStalker m)
        {
            var t = typeof(AbyssEcho.Horror.MimicStalker);
            var stateF = t.GetField("_state", BF);
            if (stateF != null) stateF.SetValue(m, System.Enum.Parse(stateF.FieldType, "Idle"));

            Vector3 p = _motor.transform.position;
            m.transform.position = new Vector3(p.x + 1f, p.y, p.z);
            _motor.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            yield return new WaitForSeconds(3f);

            _log.AppendLine($"  {m.gameObject.name} 贴脸 3 秒后: IsDead={_death.IsDead} 状态={m.StateName}");
            Check($"[{m.gameObject.name}] 和平模式下贴脸不致死", !_death.IsDead, $"IsDead={_death.IsDead}");
            Check($"[{m.gameObject.name}] 贴脸后怪物仍在活动", m.StateName != "Dead", $"State={m.StateName}");
        }

        private IEnumerator TestMonster(AbyssEcho.Monster.MonsterAI m)
        {
            var catchM = typeof(AbyssEcho.Monster.MonsterAI).GetMethod("CatchPlayer", BF);
            catchM?.Invoke(m, null);
            yield return new WaitForSeconds(0.6f);
            _log.AppendLine($"  {m.gameObject.name}: IsDead={_death.IsDead}");
            Check($"[{m.gameObject.name}] 和平模式下抓人不致死", !_death.IsDead, $"IsDead={_death.IsDead}");
        }

        private IEnumerator TestMonsterKills(AbyssEcho.Monster.MonsterAI m)
        {
            var catchM = typeof(AbyssEcho.Monster.MonsterAI).GetMethod("CatchPlayer", BF);
            catchM?.Invoke(m, null);
            yield return new WaitForSeconds(0.6f);
            Check($"[{m.gameObject.name}] 关闭后抓人致死", _death.IsDead, $"IsDead={_death.IsDead}");
        }

        private void ResetDeath()
        {
            if (_death == null) return;
            var t = typeof(AbyssEcho.Horror.PlayerDeathSystem);
            var p = t.GetProperty("IsDead", BF);
            if (p != null && p.CanWrite) p.SetValue(_death, false);
            else
            {
                var f = t.GetField("<IsDead>k__BackingField", BF) ?? t.GetField("IsDead", BF);
                f?.SetValue(_death, false);
            }
            if (_motor != null) _motor.enabled = true;
            var look = FindFirstObjectByType<AbyssEcho.Player.PlayerLook>();
            if (look != null) look.enabled = true;
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
            Debug.Log("<b>[PeaceModeTest2]</b>\n" + text);

            var di = new DirectoryInfo(Application.dataPath);
            for (int hop = 0; hop < 6 && di != null; hop++)
            {
                string r = Path.Combine(di.FullName, "Reports");
                if (Directory.Exists(r))
                {
                    try
                    {
                        File.WriteAllText(Path.Combine(r, $"PeaceModeTest2_Ch{forceChapter}.txt"),
                                          text, new UTF8Encoding(false));
                    }
                    catch { }
                    break;
                }
                di = di.Parent;
            }

            if (!Application.isEditor) Application.Quit(_fail == 0 ? 0 : 1);
        }
    }
}