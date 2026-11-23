using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S54：验证正确的 G5 → G7 路线。
    ///
    /// 审计发现 G5 北墙是实心的，出口在东墙 z=8。
    /// 本脚本沿正确路线逐段检测，确认玩家真的能走过去。
    /// </summary>
    public static class Ch4RouteVerify
    {
        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("══════ 第四章正确路线验证 ══════");

            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_Chapter4_Graybox.unity",
                                         OpenSceneMode.Single);

            // ══ 列出所有 G5 附近的墙体，找门洞 ══
            sb.AppendLine();
            sb.AppendLine("──── G5 周围墙体（找门洞）────");
            foreach (var c in Object.FindObjectsByType<BoxCollider>(FindObjectsSortMode.None))
            {
                if (c.isTrigger) continue;
                string n = c.gameObject.name;
                if (!n.Contains("G5") && !n.Contains("G7")) continue;

                Vector3 size = Vector3.Scale(c.size, c.transform.lossyScale);
                Vector3 p = c.transform.position;
                sb.AppendLine($"  {n,-28} 中心=({p.x,7:F1},{p.y,5:F1},{p.z,6:F1}) " +
                              $"尺寸=({size.x:F1},{size.y:F1},{size.z:F1})");
            }

            // ══ 沿正确路线逐段检测 ══
            //
            //   G5 中心 (-19, 0) → 东门 (-13, 8) → 核心西门 (-11.5, 8) → G7 (0, 16)
            sb.AppendLine();
            sb.AppendLine("──── 正确路线：G5 中心 → 东侧门洞(z=8) → 核心西门 → G7 ────");

            var path = new[]
            {
                new Vector3(-19f, 0.9f, 0f),      // G5 中心
                new Vector3(-19f, 0.9f, 4f),      // 往东北走
                new Vector3(-17f, 0.9f, 7f),
                new Vector3(-14f, 0.9f, 8f),      // 接近 G5 东墙门洞
                new Vector3(-11.5f, 0.9f, 8f),    // 核心西门（G5_G7_Link 内）
                new Vector3(-9.5f, 0.9f, 8f),     // 进入 G7 西边界
                new Vector3(-6f, 0.9f, 10f),
                new Vector3(0f, 0.9f, 14f),       // 接近心瓣
                new Vector3(0f, 0.9f, 16f),       // 心瓣
            };

            bool allClear = true;
            for (int i = 0; i < path.Length - 1; i++)
            {
                Vector3 a = path[i], b = path[i + 1];
                Vector3 dir = (b - a).normalized;
                float dist = Vector3.Distance(a, b);

                bool blocked = Physics.CapsuleCast(
                    a + Vector3.up * 0.9f, a + Vector3.up * 0.1f, 0.35f,
                    dir, out var hit, dist, ~0, QueryTriggerInteraction.Ignore);

                if (blocked) allClear = false;

                sb.AppendLine($"  [{i}] {a} → {b}   ({dist:F1}m)  " +
                              (blocked ? $"❌ 被 '{hit.collider.gameObject.name}' 挡住 @ {hit.point}"
                                       : "✅ 通畅"));
            }

            sb.AppendLine();
            sb.AppendLine(allClear
                ? "  ★ 结论：这条路线**完全通畅**"
                : "  ⚠️ 结论：仍有阻挡，需要处理");

            // ══ 检查 G5 东墙是否有门洞 ══
            sb.AppendLine();
            sb.AppendLine("──── G5 东墙 (x=-13) 逐点扫描 ────");
            sb.AppendLine("  沿 z 轴从 -5 扫到 +5，看哪个高度有洞：");
            for (float z = -4f; z <= 4f; z += 1f)
            {
                Vector3 probe = new Vector3(-13f, 1.2f, z);
                bool hit = Physics.CheckSphere(probe, 0.3f, ~0, QueryTriggerInteraction.Ignore);
                sb.AppendLine($"    z={z,5:F1}  {(hit ? "■ 有墙" : "□ 空（可通行）")}");
            }

            // ══ 核心西门附近 ══
            sb.AppendLine();
            sb.AppendLine("──── 核心西门 x=-10 处扫描 ────");
            for (float z = 6f; z <= 11f; z += 1f)
            {
                Vector3 probe = new Vector3(-10f, 1.2f, z);
                bool hit = Physics.CheckSphere(probe, 0.3f, ~0, QueryTriggerInteraction.Ignore);
                sb.AppendLine($"    z={z,5:F1}  {(hit ? "■ 有墙" : "□ 空（可通行）")}");
            }

            // ══ 心瓣周围的遮挡 ══
            sb.AppendLine();
            sb.AppendLine("──── 心瓣 (0, 0.12, 16) 周围 ────");
            var around = Physics.OverlapSphere(new Vector3(0f, 1.2f, 16f), 3f,
                                               ~0, QueryTriggerInteraction.Ignore);
            int cnt = 0;
            foreach (var o in around)
            {
                if (o == null || o.isTrigger) continue;
                if (o.gameObject.name == "HeartValve") continue;
                sb.AppendLine($"    附近物体: {o.gameObject.name} @ {o.transform.position}");
                cnt++;
            }
            if (cnt == 0) sb.AppendLine("    ✅ 心瓣周围 3 米内无遮挡物");

            string text = sb.ToString();
            Debug.Log(text);
            File.WriteAllText(Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(Application.dataPath), "../Reports/Ch4RouteVerify.txt")),
                text, new UTF8Encoding(false));
        }
    }
}