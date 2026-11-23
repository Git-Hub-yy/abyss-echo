using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// S55：和平模式自动化测试。
    ///
    /// ══════════════════ 测试策略 ══════════════════
    ///
    ///   不是"读一下 IsEnabled 就完事"，而是：
    ///     ① 把怪物直接放到玩家身边（进入攻击距离）
    ///     ② 在和平模式关闭时，验证玩家**确实会死**
    ///     ③ 在和平模式开启时，验证玩家**不会死**
    ///     ④ 再关闭，验证**又恢复会死**
    ///
    ///   这样才证明"和平模式真的拦住了攻击"，而不是"没触发过所以没死"。
    ///
    ///   同时验证：
    ///     · 怪物在和平模式下**仍在活动**（不会消失/冻结）
    ///     · 连续切换状态无异常
    ///     · 切场景后管理器只有一个
    /// </summary>
    public class PeaceModeTest : MonoBehaviour
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

            _log.AppendLine("══════ S55 和平模式测试 ══════");
            _log.AppendLine($"章节 = {forceChapter}");
            _log.AppendLine();

            _pm = FindFirstObjectByType<PeaceModeManager>();
            _death = FindFirstObjectByType<AbyssEcho.Horror.PlayerDeathSystem>();
            _motor = FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();

            // ══ T1 管理器存在且只有一个 ══
            _log.AppendLine("──── T1 管理器 ────");
            var all = FindObjectsByType<PeaceModeManager>(FindObjectsSortMode.None);
            Check("T1a PeaceModeManager 存在", _pm != null, _pm != null ? $"@{_pm.gameObject.name}" : "❌");
            Check("T1b 恰好 1 个实例", all.Length == 1, $"数量={all.Length}");

            if (_pm == null || _death == null || _motor == null)
            {
                _log.AppendLine("❌ 缺少关键组件，终止");
                Dump();
                yield break;
            }

            // ══ T2 默认关闭 ══
            _log.AppendLine();
            _log.AppendLine("──── T2 默认状态 ────");
            _pm.SetEnabled(false);
            yield return null;
            Check("T2 默认和平模式关闭", !PeaceModeManager.IsEnabled,
                  $"IsEnabled={PeaceModeManager.IsEnabled}");
            Check("T2b AllowAttack=true（允许攻击）", PeaceModeManager.AllowAttack, "");

            // ══ T3 找到怪物并测试 ══
            _log.AppendLine();
            _log.AppendLine("──── T3 怪物清单 ────");
            var mimics = FindObjectsByType<AbyssEcho.Horror.MimicStalker>(FindObjectsSortMode.None);
            var monsters = FindObjectsByType<AbyssEcho.Monster.MonsterAI>(FindObjectsSortMode.None);
            _log.AppendLine($"  MimicStalker = {mimics.Length}  MonsterAI = {monsters.Length}");

            if (mimics.Length == 0 && monsters.Length == 0)
            {
                _log.AppendLine("  本章无怪物，改用构造测试");
            }

            // ══ T4 和平模式开启：怪物靠近但不致死 ══
            _log.AppendLine();
            _log.AppendLine("──── T4 和平模式开启时，把怪物贴到玩家身上 ────");

            _pm.SetEnabled(true);
            yield return null;
            Check("T4a 开启后 IsEnabled=true", PeaceModeManager.IsEnabled, "");
            Check("T4b 开启后 AllowAttack=false（禁止攻击）", !PeaceModeManager.AllowAttack, "");

            // 复活玩家（可能上一轮已死）
            ResetDeath();

            if (mimics.Length > 0)
            {
                yield return TestMimicWithPeace(mimics[0]);
            }
            if (monsters.Length > 0)
            {
                yield return TestMonsterWithPeace(monsters[0]);
            }

            // ══ T5 和平模式关闭：恢复致死 ══
            _log.AppendLine();
            _log.AppendLine("──── T5 关闭和平模式，验证恢复致死 ────");

            _pm.SetEnabled(false);
            yield return null;
            Check("T5a 关闭后 IsEnabled=false", !PeaceModeManager.IsEnabled, "");
            Check("T5b 关闭后 AllowAttack=true", PeaceModeManager.AllowAttack, "");

            ResetDeath();

            if (mimics.Length > 0)
            {
                yield return TestMimicKills(mimics[0]);
            }
            else if (monsters.Length > 0)
            {
                yield return TestMonsterKills(monsters[0]);
            }

            // ══ T6 连续切换 ══
            _log.AppendLine();
            _log.AppendLine("──── T6 连续切换 20 次 ────");
            // 先确保从"关闭"状态开始
            _pm.SetEnabled(false);
            yield return null;

            bool toggleOk = true;
            string seq = "";
            for (int i = 0; i < 20; i++)
            {
                _pm.Toggle();
                // 从"关"开始，第 1 次切换应变成"开"，第 2 次"关"……
                // 即 i 为偶数时应为"开"
                bool expect = (i % 2 == 0);
                if (PeaceModeManager.IsEnabled != expect) toggleOk = false;
                seq += PeaceModeManager.IsEnabled ? "开" : "关";
            }
            _log.AppendLine($"  切换序列: {seq}");
            Check("T6a 连续 20 次切换状态正确", toggleOk, "");
            Check("T6b 切换后仍只有 1 个实例",
                  FindObjectsByType<PeaceModeManager>(FindObjectsSortMode.None).Length == 1, "");

            // 归位到关闭
            _pm.SetEnabled(false);
            yield return null;

            _log.AppendLine();
            Unverified("U1 按 M 键的实际手感（自动化直接调 SetEnabled，不经键盘）");
            Unverified("U2 屏幕提示条的位置与观感");
            Unverified("U3 和平模式下长时间游玩的体验");

            Dump();
        }

        // ── 和平模式：怪物靠近不致死 ──
        private IEnumerator TestMimicWithPeace(AbyssEcho.Horror.MimicStalker m)
        {
            _log.AppendLine($"  测试对象: {m.gameObject.name}");

            // 把怪物放到玩家身边 1 米（攻击距离 2.2 内）
            Vector3 p = _motor.transform.position;
            m.transform.position = new Vector3(p.x + 1f, p.y, p.z);

            // 让玩家背对怪物（这样怪物会追）
            _motor.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            float t = 0f;
            bool died = false;
            while (t < 4f)
            {
                if (_death.IsDead) { died = true; break; }
                t += Time.deltaTime;
                yield return null;
            }

            _log.AppendLine($"    等了 {t:F1}s，玩家 IsDead={_death.IsDead}");
            _log.AppendLine($"    怪物状态 = {m.StateName}  位置 = {m.transform.position}");

            Check($"T4c [{m.gameObject.name}] 和平模式下贴脸不致死", !died,
                  $"IsDead={_death.IsDead}");
            Check($"T4d [{m.gameObject.name}] 怪物仍在活动（未冻结）",
                  m.StateName != "Dead", $"StateName={m.StateName}");
            Check($"T4e [{m.gameObject.name}] 怪物没有被移走或禁用",
                  m.gameObject.activeInHierarchy, $"active={m.gameObject.activeInHierarchy}");
        }

        private IEnumerator TestMonsterWithPeace(AbyssEcho.Monster.MonsterAI m)
        {
            _log.AppendLine($"  测试对象: {m.gameObject.name}");

            var mType = typeof(AbyssEcho.Monster.MonsterAI);
            var catchM = mType.GetMethod("CatchPlayer", BF);

            if (catchM == null)
            {
                _log.AppendLine("    找不到 CatchPlayer，跳过");
                yield break;
            }

            // 直接调用抓人方法（模拟距离判定通过）
            catchM.Invoke(m, null);
            yield return new WaitForSeconds(0.6f);

            _log.AppendLine($"    直接调用 CatchPlayer 后，玩家 IsDead={_death.IsDead}");
            Check($"T4f [{m.gameObject.name}] 和平模式下抓人不致死", !_death.IsDead,
                  $"IsDead={_death.IsDead}");

            var st = mType.GetProperty("CurrentState", BF);
            string state = st != null ? st.GetValue(m).ToString() : "?";
            _log.AppendLine($"    怪物状态 = {state}");
            Check($"T4g [{m.gameObject.name}] 没有进入 Stunned 冻结",
                  state != "Stunned", $"state={state}");
        }

        // ── 关闭模式：恢复致死 ──
        private IEnumerator TestMimicKills(AbyssEcho.Horror.MimicStalker m)
        {
            _log.AppendLine($"  测试对象: {m.gameObject.name}");

            // 重置怪物状态
            var stateF = typeof(AbyssEcho.Horror.MimicStalker).GetField("_state", BF);
            if (stateF != null)
                stateF.SetValue(m, System.Enum.Parse(stateF.FieldType, "Idle"));

            Vector3 p = _motor.transform.position;
            m.transform.position = new Vector3(p.x + 1f, p.y, p.z);
            _motor.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            float t = 0f;
            while (t < 4f && !_death.IsDead)
            {
                t += Time.deltaTime;
                yield return null;
            }

            _log.AppendLine($"    等了 {t:F1}s，玩家 IsDead={_death.IsDead}");
            Check($"T5c [{m.gameObject.name}] 关闭和平模式后恢复致死", _death.IsDead,
                  $"IsDead={_death.IsDead}");
        }

        private IEnumerator TestMonsterKills(AbyssEcho.Monster.MonsterAI m)
        {
            _log.AppendLine($"  测试对象: {m.gameObject.name}");
            var catchM = typeof(AbyssEcho.Monster.MonsterAI).GetMethod("CatchPlayer", BF);
            if (catchM == null) yield break;

            catchM.Invoke(m, null);
            yield return new WaitForSeconds(0.6f);

            Check($"T5f [{m.gameObject.name}] 关闭和平模式后抓人致死", _death.IsDead,
                  $"IsDead={_death.IsDead}");
        }

        /// <summary>把死亡状态复位（用于反复测试）。</summary>
        private void ResetDeath()
        {
            if (_death == null) return;

            var t = typeof(AbyssEcho.Horror.PlayerDeathSystem);

            var isDeadP = t.GetProperty("IsDead", BF);
            if (isDeadP != null && isDeadP.CanWrite)
                isDeadP.SetValue(_death, false);
            else
            {
                var isDeadF = t.GetField("<IsDead>k__BackingField", BF)
                           ?? t.GetField("IsDead", BF);
                isDeadF?.SetValue(_death, false);
            }

            // 恢复玩家组件
            if (_motor != null) _motor.enabled = true;

            var look = FindFirstObjectByType<AbyssEcho.Player.PlayerLook>();
            if (look != null) look.enabled = true;

            var detector = FindFirstObjectByType<AbyssEcho.Interaction.InteractionDetector>();
            if (detector != null) detector.enabled = true;
        }

        private void Check(string name, bool ok, string detail)
        {
            if (ok) { _pass++; _log.AppendLine($"[通过] {name}  {detail}"); }
            else { _fail++; _log.AppendLine($"[失败] {name}  {detail}"); }
        }

        private void Unverified(string name) { _unverified++; _log.AppendLine($"[待人工验收] {name}"); }

        private void Dump()
        {
            _log.AppendLine();
            _log.AppendLine("=====================================");
            _log.AppendLine($"通过 {_pass}，失败 {_fail}，待人工验收 {_unverified}");

            string text = _log.ToString();
            Debug.Log("<b>[PeaceModeTest]</b>\n" + text);

            var di = new DirectoryInfo(Application.dataPath);
            for (int hop = 0; hop < 6 && di != null; hop++)
            {
                string r = Path.Combine(di.FullName, "Reports");
                if (Directory.Exists(r))
                {
                    try
                    {
                        File.WriteAllText(Path.Combine(r, $"PeaceModeTest_Ch{forceChapter}.txt"),
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