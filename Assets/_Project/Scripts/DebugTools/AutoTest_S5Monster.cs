using System.Collections;
using System.IO;
using System.Text;
using AbyssEcho.Monster;
using UnityEngine;
using UnityEngine.AI;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// S5 第二部分自动测试：怪物 AI。
    ///
    /// 验证：
    ///   · 怪物实例存在且高度正确
    ///   · 导航网格可用（能在关卡里找到可行走点）
    ///   · 状态机能正确转换
    ///   · 感知参数合理
    ///   · 巡逻点已设置
    /// </summary>
    public class AutoTest_S5Monster : MonoBehaviour
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
            _log.AppendLine("ABYSS ECHO · S5 怪物 AI 自动测试");
            _log.AppendLine("=====================================");

            yield return new WaitForSeconds(0.5f);

            T38_MonsterExists();
            T39_MonsterHeight();
            T40_NavMeshAvailable();
            T41_NavAgentConfigured();
            T42_PatrolPoints();
            T43_StateMachine();
            T44_ActivationTrigger();
            T45_PerceptionConfig();
            T46_SilhouetteMaterial();

            _log.AppendLine("=====================================");
            _log.AppendLine($"结果：通过 {_pass}，失败 {_fail}");

            string text = _log.ToString();
            Debug.Log("<b>[AutoTest_S5Monster]</b>\n" + text);
            WriteReport(text);

            yield return new WaitForSeconds(0.2f);
            if (!Application.isEditor) Application.Quit(_fail == 0 ? 0 : 1);
        }

        private void T38_MonsterExists()
        {
            var m = FindFirstObjectByType<MonsterAI>();
            Check("T38 怪物实例存在", m != null);
        }

        private void T39_MonsterHeight()
        {
            var m = FindFirstObjectByType<MonsterAI>();
            if (m == null) { Fail("T39 怪物身高合理", "无怪物"); return; }

            var rends = m.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) { Fail("T39 怪物身高合理", "无渲染器"); return; }

            float h = rends[0].bounds.size.y;
            // 应在 1.5~2.5 米之间（比玩家 1.78 略高）
            bool ok = h > 1.5f && h < 2.5f;
            Check("T39 怪物身高合理", ok, $"高度={h:F2}m（期望 1.5~2.5m）");
        }

        private void T40_NavMeshAvailable()
        {
            // 在关卡范围内找一个可行走点
            bool found = NavMesh.SamplePosition(new Vector3(0f, 0f, 10.9f), out var hit, 10f, NavMesh.AllAreas);
            bool found2 = NavMesh.SamplePosition(new Vector3(0f, 0f, 3f), out var hit2, 10f, NavMesh.AllAreas);

            Check("T40 导航网格可用", found || found2,
                  found ? $"B2 走廊命中 ({hit.position.x:F1},{hit.position.z:F1})" :
                  found2 ? $"B1 餐厅命中 ({hit2.position.x:F1},{hit2.position.z:F1})" : "未找到可行走点");
        }

        private void T41_NavAgentConfigured()
        {
            var m = FindFirstObjectByType<MonsterAI>();
            if (m == null) { Fail("T41 导航代理已配置", "无怪物"); return; }

            var agent = m.GetComponent<NavMeshAgent>();
            if (agent == null) { Fail("T41 导航代理已配置", "无 NavMeshAgent"); return; }

            bool ok = agent.radius > 0.1f && agent.height > 1f && agent.speed > 0.5f;
            Check("T41 导航代理已配置", ok,
                  $"radius={agent.radius:F2} height={agent.height:F1} speed={agent.speed:F1}");
        }

        private void T42_PatrolPoints()
        {
            var m = FindFirstObjectByType<MonsterAI>();
            if (m == null) { Fail("T42 巡逻点已设置", "无怪物"); return; }

            int n = m.patrolPoints != null ? m.patrolPoints.Length : 0;
            Check("T42 巡逻点已设置", n >= 3, $"共 {n} 个巡逻点");
        }

        private void T43_StateMachine()
        {
            var m = FindFirstObjectByType<MonsterAI>();
            if (m == null) { Fail("T43 状态机可转换", "无怪物"); return; }

            var initial = m.CurrentState;

            // Dormant → Patrol（激活）
            m.Deactivate();
            bool dormant = m.CurrentState == MonsterAI.State.Dormant;

            m.Activate();
            bool patrol = m.CurrentState == MonsterAI.State.Patrol;

            Check("T43 状态机可转换", dormant && patrol,
                  $"初始={initial} 休眠={dormant} 激活后={m.CurrentState}");
        }

        private void T44_ActivationTrigger()
        {
            var triggers = FindObjectsByType<MonsterActivationTrigger>(FindObjectsSortMode.None);
            Check("T44 怪物激活触发器存在", triggers.Length >= 1, $"找到 {triggers.Length} 个");
        }

        private void T45_PerceptionConfig()
        {
            var m = FindFirstObjectByType<MonsterAI>();
            if (m == null) { Fail("T45 感知参数合理", "无怪物"); return; }

            bool ok = m.sightRange > 5f && m.sightRange < 40f
                   && m.sightAngle > 30f && m.sightAngle < 200f
                   && m.chaseSpeed > m.patrolSpeed;
            Check("T45 感知参数合理", ok,
                  $"视野={m.sightRange:F0}m/{m.sightAngle:F0}° 巡逻={m.patrolSpeed:F1} 追击={m.chaseSpeed:F1}");
        }

        private void T46_SilhouetteMaterial()
        {
            var m = FindFirstObjectByType<MonsterAI>();
            if (m == null) { Fail("T46 剪影材质已应用", "无怪物"); return; }

            var rends = m.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) { Fail("T46 剪影材质已应用", "无渲染器"); return; }

            var mat = rends[0].sharedMaterial;
            if (mat == null) { Fail("T46 剪影材质已应用", "无材质"); return; }

            Color c = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : mat.color;
            float lum = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
            bool dark = lum < 0.1f;

            Check("T46 剪影材质已应用", dark,
                  $"材质={mat.name} 亮度={lum:F3}（剪影应 <0.1）");
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
                File.WriteAllText(Path.Combine(dir, "S5Monster_AutoTestResult.txt"), text, new UTF8Encoding(false));
            }
            catch { }
        }
    }
}