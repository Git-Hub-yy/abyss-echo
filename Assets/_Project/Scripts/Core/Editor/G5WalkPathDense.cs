using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S59d：用**密集采样**（每 0.15 米一个点）验证 G5 → G7 整条路是否真能走。
    ///
    /// ══════════════════ 为什么密集采样比 CapsuleCast 可靠 ══════════════════
    ///
    ///   CapsuleCast 扫一整段直线，跨两块地板时胶囊下沿会擦到
    ///   地板块的边缘，产生"明明能走却报被挡"的假阳性。
    ///
    ///   密集采样是：每隔 0.15 米放一个"玩家胶囊"，检查这个位置
    ///   有没有被非地板的物体占据。这才是玩家能站的地方。
    /// </summary>
    public static class G5WalkPathDense
    {
        public static void Run()
        {
            var sb = new StringBuilder();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_Chapter4_Graybox.unity",
                                         OpenSceneMode.Single);
            Physics.SyncTransforms();

            sb.AppendLine("══════ G5 → G7 密集采样通行验证 ══════");
            sb.AppendLine("玩家胶囊 r=0.35 h=1.8，每 0.15 米采样");
            sb.AppendLine();

            // 完整路线：G5 → 北门洞 → 过渡区东行 → 连廊 → G7 心瓣
            var waypoints = new[]
            {
                new Vector3(-19f, 0, 0f),      // G5 中心
                new Vector3(-19f, 0, 3f),      // G5 北部
                new Vector3(-19f, 0, 5f),      // 门洞中心（北墙）
                new Vector3(-18.5f, 0, 5.7f),  // 出门
                new Vector3(-16f, 0, 6.0f),    // 过渡区东行
                new Vector3(-13.5f, 0, 6.2f),  // 过渡区东端
                new Vector3(-12f, 0, 6.5f),    // 靠近连廊
                new Vector3(-11.8f, 0, 7.2f),  // 进连廊
                new Vector3(-11f, 0, 8.0f),    // 连廊中线
                new Vector3(-9.5f, 0, 8.0f),   // 穿连廊东门进 G7
                new Vector3(-7f, 0, 10f),      // G7 内
                new Vector3(-3f, 0, 12f),      // G7 内
                new Vector3(0f, 0, 13.5f),     // 心瓣前
            };

            int totalSamples = 0, badSamples = 0;
            var badList = new StringBuilder();

            for (int seg = 0; seg < waypoints.Length - 1; seg++)
            {
                Vector3 a = waypoints[seg];
                Vector3 b = waypoints[seg + 1];
                Vector3 flatA = new Vector3(a.x, 0, a.z);
                Vector3 flatB = new Vector3(b.x, 0, b.z);
                float dist = Vector3.Distance(flatA, flatB);
                int steps = Mathf.Max(1, Mathf.CeilToInt(dist / 0.15f));

                sb.AppendLine($"── 段 {seg + 1}: ({a.x:F1},{a.z:F1}) → ({b.x:F1},{b.z:F1})  长 {dist:F2}m  采样 {steps} 点 ──");

                int segBad = 0;
                float lastGoodT = 0f;
                for (int i = 0; i <= steps; i++)
                {
                    float t = (float)i / steps;
                    Vector3 p = Vector3.Lerp(flatA, flatB, t);
                    totalSamples++;

                    // 1. 脚下有地板
                    bool hasFloor = Physics.Raycast(new Vector3(p.x, p.y + 2f, p.z), Vector3.down,
                                                    4f, ~0, QueryTriggerInteraction.Ignore);

                    // 2. 胶囊位置无阻挡（忽略地板本身）
                    bool blocked = false;
                    string blocker = "";
                    if (hasFloor)
                    {
                        var hits = Physics.OverlapCapsule(
                            new Vector3(p.x, 0.35f, p.z),
                            new Vector3(p.x, 1.45f, p.z),
                            0.35f, ~0, QueryTriggerInteraction.Ignore);
                        foreach (var h in hits)
                        {
                            if (h == null || h.isTrigger) continue;
                            // 忽略所有地板（名字含 Floor 或顶部在 y<0.1）
                            string n = h.gameObject.name;
                            if (n.Contains("Floor")) continue;
                            var hb = h.bounds;
                            if (hb.max.y < 0.2f) continue;   // 贴地的矮块，能跨过去
                            blocked = true; blocker = n;
                            break;
                        }
                    }

                    if (!hasFloor || blocked)
                    {
                        segBad++; badSamples++;
                        if (badList.Length < 800)
                            badList.AppendLine($"    ❌ ({p.x:F2},{p.z:F2})  {(hasFloor ? "被 " + blocker + " 挡住" : "无地板")}");
                    }
                    else lastGoodT = t;
                }

                if (segBad == 0)
                    sb.AppendLine($"   ✅ 全段 {steps + 1} 点通过");
                else
                    sb.AppendLine($"   ❌ {segBad}/{steps + 1} 点不通");
            }

            sb.AppendLine();
            sb.AppendLine("══════ 结果 ══════");
            sb.AppendLine($"总采样点 {totalSamples}，不通 {badSamples}");
            if (badList.Length > 0)
            {
                sb.AppendLine("不通的点：");
                sb.Append(badList);
            }
            sb.AppendLine();
            sb.AppendLine(badSamples == 0
                ? "✅ G5 → G7 全线可通行"
                : $"❌ 有 {badSamples} 个点不通，需要继续修");

            string text = sb.ToString();
            Debug.Log(text);
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\G5WalkPathDense.txt",
                              text, new UTF8Encoding(false));
        }
    }
}