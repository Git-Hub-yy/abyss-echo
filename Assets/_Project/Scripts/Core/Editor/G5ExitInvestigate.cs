using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    public static class G5ExitInvestigate
    {
        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("══════ G5 → G7 通路真相调查 ══════");
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_Chapter4_Graybox.unity", OpenSceneMode.Single);
            Physics.SyncTransforms();

            sb.AppendLine();
            sb.AppendLine("【G5 范围】x[-25,-13] z[-5,5]");
            sb.AppendLine("【G5 东墙 x=-13 的组成】");
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                string n = t.name;
                if (!(n.Contains("East_G5") || n.Contains("G5_G7") || n.Contains("North_G5") || n.Contains("South_G5")))
                    continue;
                var bc = t.GetComponent<BoxCollider>();
                if (bc == null) continue;
                var b = bc.bounds;
                sb.AppendLine($"  {n,-28} x[{b.min.x:F1},{b.max.x:F1}] y[{b.min.y:F1},{b.max.y:F1}] z[{b.min.z:F1},{b.max.z:F1}]");
            }

            sb.AppendLine();
            sb.AppendLine("【G5 该怎么走出去？】逐面扫描");
            sb.AppendLine("  --- 北墙 z=5 ---");
            sb.AppendLine("  (玩家胶囊 x 扫描，y=1.0，z=5.0)");
            for (float x = -25f; x <= -13f; x += 1f)
            {
                bool w = Physics.CheckCapsule(new Vector3(x, 0.35f, 5f), new Vector3(x, 1.45f, 5f), 0.35f, ~0, QueryTriggerInteraction.Ignore);
                if (!w) sb.AppendLine($"    x={x,6:F0}  □ 通！");
            }

            sb.AppendLine("  --- 东墙 x=-13 ---");
            for (float z = -5f; z <= 12f; z += 1f)
            {
                bool w = Physics.CheckCapsule(new Vector3(-13f, 0.35f, z), new Vector3(-13f, 1.45f, z), 0.35f, ~0, QueryTriggerInteraction.Ignore);
                sb.AppendLine($"    z={z,6:F0}  {(w ? "■ 墙" : "□ 通")}");
            }

            sb.AppendLine();
            sb.AppendLine("【G5_G7_Link 房间】x[-13,-10] z[7,9]");
            sb.AppendLine("  → 这个连廊在 z[7,9]，而 G5 只到 z=5");
            sb.AppendLine("  → 中间 z[5,7] 是 2 米的空隙！");
            sb.AppendLine();
            sb.AppendLine("【z[5,7] 区域扫描】");
            for (float z = 5f; z <= 7.5f; z += 0.5f)
            {
                for (float x = -20f; x <= -9f; x += 1f)
                {
                    bool w = Physics.CheckCapsule(new Vector3(x, 0.35f, z), new Vector3(x, 1.45f, z), 0.35f, ~0, QueryTriggerInteraction.Ignore);
                    if (!w) sb.Append($"    x={x:F0} z={z:F1} □通  ");
                }
            }
            sb.AppendLine();

            sb.AppendLine();
            sb.AppendLine("【地板检测：玩家能不能从 G5 走到 G5_G7_Link】");
            var pts = new[]
            {
                new Vector3(-19, 0, 0),    // G5 中心
                new Vector3(-19, 0, 4),    // G5 北部
                new Vector3(-19, 0, 5),    // G5 北边界
                new Vector3(-19, 0, 6),    // ⚠️ 空隙区
                new Vector3(-19, 0, 7),    // 空隙区
                new Vector3(-17, 0, 7.5f),
                new Vector3(-15, 0, 8),
                new Vector3(-13, 0, 8),
                new Vector3(-11.5f, 0, 8),
                new Vector3(-10, 0, 8),
            };
            foreach (var p in pts)
            {
                bool floor = Physics.Raycast(p + Vector3.up * 2f, Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore);
                string hitName = "";
                if (floor) { Physics.Raycast(p + Vector3.up * 2f, Vector3.down, out var h, 4f, ~0, QueryTriggerInteraction.Ignore); hitName = h.collider.gameObject.name; }
                sb.AppendLine($"  ({p.x:F1},{p.z:F1})  地板={(floor ? "✅ " + hitName : "❌ 无")}");
            }

            string text = sb.ToString();
            Debug.Log(text);
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\G5ExitInvestigate.txt", text, new UTF8Encoding(false));
        }
    }
}