using System.Collections;
using System.IO;
using System.Text;
using AbyssEcho.Horror;
using AbyssEcho.Monster;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// S13 自动测试：第二章恐怖系统。
    ///
    /// 验证：
    ///   · 深水区存在且配置正确
    ///   · 玩家进入水域后速度倍率生效
    ///   · 第二章怪物存在、有巡逻点
    ///   · 恐怖触发器 ≥ 4 个
    ///   · 怪物激活触发器存在
    /// </summary>
    public class AutoTest_S13 : MonoBehaviour
    {
        public bool runOnStart = true;

        private int _pass, _fail;
        private readonly StringBuilder _log = new StringBuilder();

        private void Start()
        {
            if (runOnStart) StartCoroutine(RunAll());
        }

        private IEnumerator RunAll()
        {
            _log.AppendLine("ABYSS ECHO · S13 第二章恐怖系统测试");
            _log.AppendLine("=====================================");

            yield return new WaitForSeconds(0.5f);

            T87_WaterZoneExists();
            T88_WaterZoneConfig();
            T89_MonsterExists();
            T90_MonsterPatrolPoints();
            T91_HorrorTriggers();
            T92_ActivationTrigger();
            T93_PlayerMotorSpeedMultiplier();
            yield return T94_WaterSlowsPlayer();

            _log.AppendLine("=====================================");
            _log.AppendLine($"结果：通过 {_pass}，失败 {_fail}");

            string text = _log.ToString();
            Debug.Log("<b>[AutoTest_S13]</b>\n" + text);
            WriteReport(text);

            yield return new WaitForSeconds(0.2f);
            if (!Application.isEditor) Application.Quit(_fail == 0 ? 0 : 1);
        }

        private void T87_WaterZoneExists()
        {
            var zones = FindObjectsByType<DeepWaterZone>(FindObjectsSortMode.None);
            Check("T87 深水区存在", zones.Length >= 1, $"共 {zones.Length} 个");
        }

        private void T88_WaterZoneConfig()
        {
            var z = FindFirstObjectByType<DeepWaterZone>();
            if (z == null) { Fail("T88 深水区参数合理", "无深水区"); return; }

            bool ok = z.speedMultiplier < 0.9f && z.noiseMultiplier > 1f && z.waterDepth > 0.1f;
            Check("T88 深水区参数合理", ok,
                  $"水深={z.waterDepth:F1}m 速度×{z.speedMultiplier:F2} 噪音×{z.noiseMultiplier:F1}");
        }

        private void T89_MonsterExists()
        {
            var monsters = FindObjectsByType<MonsterAI>(FindObjectsSortMode.None);
            bool hasCh2 = GameObject.Find("Monster_Ch2") != null;

            Check("T89 第二章怪物存在", hasCh2,
                  $"场景中共 {monsters.Length} 只怪物，Monster_Ch2={(hasCh2 ? "存在" : "缺失")}");
        }

        private void T90_MonsterPatrolPoints()
        {
            var m = GameObject.Find("Monster_Ch2");
            if (m == null) { Fail("T90 怪物有巡逻点", "无 Monster_Ch2"); return; }

            var ai = m.GetComponent<MonsterAI>();
            if (ai == null) { Fail("T90 怪物有巡逻点", "无 MonsterAI"); return; }

            int n = ai.patrolPoints != null ? ai.patrolPoints.Length : 0;
            Check("T90 怪物有巡逻点", n >= 3, $"{n} 个巡逻点，视野={ai.sightRange}m");
        }

        private void T91_HorrorTriggers()
        {
            var triggers = FindObjectsByType<HorrorTrigger>(FindObjectsSortMode.None);
            // 第一章有 4 个 + 第二章 4 个 = 8
            Check("T91 恐怖触发器数量", triggers.Length >= 8, $"共 {triggers.Length} 个（第一章 4 + 第二章 4）");
        }

        private void T92_ActivationTrigger()
        {
            var trig = GameObject.Find("Ch2_MonsterActivate");
            bool ok = trig != null && trig.GetComponent<MonsterActivationTrigger>() != null;
            Check("T92 第二章怪物激活触发器", ok);
        }

        private void T93_PlayerMotorSpeedMultiplier()
        {
            var motor = FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
            if (motor == null) { Fail("T93 玩家速度倍率接口", "无 PlayerMotor"); return; }

            var prop = typeof(AbyssEcho.Player.PlayerMotor).GetProperty("ExternalSpeedMultiplier");
            bool ok = prop != null && prop.CanWrite;

            float def = ok ? (float)prop.GetValue(motor) : -1f;
            Check("T93 玩家速度倍率接口", ok && Mathf.Abs(def - 1f) < 0.01f,
                  $"属性存在={ok} 默认值={def:F2}");
        }

        private IEnumerator T94_WaterSlowsPlayer()
        {
            var motor = FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
            var zone = FindFirstObjectByType<DeepWaterZone>();

            if (motor == null || zone == null)
            {
                Fail("T94 下水后速度降低", "缺少组件");
                yield break;
            }

            // 记录陆地倍率
            float onLand = motor.ExternalSpeedMultiplier;

            // 把玩家传送到水域中心
            var cc = motor.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            motor.transform.position = zone.transform.position + Vector3.up * 0.05f;
            if (cc != null) cc.enabled = true;

            yield return new WaitForSeconds(0.4f);

            float inWater = motor.ExternalSpeedMultiplier;
            bool ok = inWater < onLand - 0.01f;

            // 诊断信息
            var col = zone.GetComponent<BoxCollider>();
            string diag = $"玩家位置={motor.transform.position} 水域中心={zone.transform.position} " +
                          $"水域尺寸={(col != null ? col.size.ToString() : "?")} PlayerInside={zone.PlayerInside}";

            Check("T94 下水后速度降低", ok,
                  $"陆地×{onLand:F2} → 水中×{inWater:F2}　[{diag}]");

            // 恢复
            if (cc != null) cc.enabled = false;
            motor.transform.position = AbyssEcho.LevelGen.ChapterLayout.GetPlayerStart();
            if (cc != null) cc.enabled = true;
        }

        // ────────────────────────── 工具 ──────────────────────────

        private void Check(string name, bool ok, string detail = "")
        {
            if (ok) { _pass++; _log.AppendLine($"[通过] {name} {detail}"); }
            else { _fail++; _log.AppendLine($"[失败] {name} {detail}"); }
        }

        private void Fail(string name, string detail)
        {
            _fail++;
            _log.AppendLine($"[失败] {name} {detail}");
        }

        private static void WriteReport(string text)
        {
            try
            {
                string dir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports"));
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "S13_AutoTestResult.txt"), text, new UTF8Encoding(false));
            }
            catch { }
        }
    }
}