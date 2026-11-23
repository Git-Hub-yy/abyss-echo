using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace AbyssEcho.Core.EditorTools {
    public static class Ch5H6H7PathCheck {
        public static void Run() {
            var sb = new StringBuilder();
            sb.AppendLine("=== 第五章 H6 → H7 通道检查 ===");
            sb.AppendLine();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_Chapter5_Graybox.unity", OpenSceneMode.Single);
            Physics.SyncTransforms();

            sb.AppendLine("──── A. 通道相关对象 ────");
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)) {
                if (r == null) continue;
                string n = r.gameObject.name;
                if (!n.Contains("H6_H7") && !n.Contains("Floor_H7") && !n.Contains("Floor_H6")) continue;
                var b = r.bounds;
                sb.AppendLine($"  {n,-34} X[{b.min.x,7:F2},{b.max.x,7:F2}] Y[{b.min.y,6:F2},{b.max.y,6:F2}] Z[{b.min.z,7:F2},{b.max.z,7:F2}]");
            }

            sb.AppendLine();
            sb.AppendLine("──── B. 沿 x=0 从 z=40 到 z=58 逐点探地面 ────");
            sb.AppendLine($"  {"z",-8} {"地面Y",-10} {"命中对象",-32} 状态");
            float lastHole = -999, holeStart = -999;
            int holes = 0;
            for (float z = 40f; z <= 58f; z += 0.5f) {
                float bestY = -999f; string bestName = "无";
                var hits = Physics.RaycastAll(new Vector3(0f, 6f, z), Vector3.down, 30f, ~0, QueryTriggerInteraction.Ignore);
                foreach (var h in hits) {
                    if (h.collider == null) continue;
                    var b = h.collider.bounds;
                    if (0f < b.min.x - 0.01f || 0f > b.max.x + 0.01f) continue;
                    if (z < b.min.z - 0.01f || z > b.max.z + 0.01f) continue;
                    if (b.max.y > 1.0f || b.max.y < -2.0f) continue;
                    if (h.point.y > bestY) { bestY = h.point.y; bestName = h.collider.gameObject.name; }
                }
                bool ok = bestY > -999f + 1f;
                if (!ok) { holes++; if (holeStart < -900f) holeStart = z; lastHole = z; }
                if (!ok || Mathf.Abs(z - Mathf.Round(z)) < 0.26f)
                    sb.AppendLine($"  {z,-8:F1} {(ok ? bestY.ToString("F2") : "无"),-10} {bestName,-32} {(ok ? "✅" : "❌ 虚空")}");
            }
            if (holes > 0) sb.AppendLine($"  ⚠️ 虚空点 {holes} 个，范围 z[{holeStart:F1}, {lastHole:F1}]");

            sb.AppendLine();
            sb.AppendLine("──── C. 沿 z=46 横向扫描（通道中部）────");
            sb.AppendLine("  x 从 -8 到 8：");
            var row = new StringBuilder("  ");
            for (float x = -8f; x <= 8f; x += 0.5f) {
                bool ok = false;
                var hits = Physics.RaycastAll(new Vector3(x, 6f, 46f), Vector3.down, 30f, ~0, QueryTriggerInteraction.Ignore);
                foreach (var h in hits) {
                    if (h.collider == null) continue;
                    var b = h.collider.bounds;
                    if (x < b.min.x - 0.01f || x > b.max.x + 0.01f) continue;
                    if (46f < b.min.z - 0.01f || 46f > b.max.z + 0.01f) continue;
                    if (b.max.y > 1.0f || b.max.y < -2.0f) continue;
                    ok = true; break;
                }
                row.Append(ok ? "█" : "·");
            }
            sb.AppendLine(row.ToString());
            sb.AppendLine("  █=有地面  ·=虚空");

            sb.AppendLine();
            sb.AppendLine("──── D. 通道内是否有墙阻挡（水平射线）────");
            var blocked = Physics.RaycastAll(new Vector3(0f, 1.62f, 44f), Vector3.forward, 6f, ~0, QueryTriggerInteraction.Ignore);
            int wallCount = 0;
            foreach (var h in blocked) {
                if (h.collider == null) continue;
                string n = h.collider.gameObject.name;
                if (n.StartsWith("Floor")) continue;
                sb.AppendLine($"  阻挡: {n,-30} @ z={h.point.z:F2}");
                wallCount++;
            }
            sb.AppendLine($"  共 {wallCount} 个阻挡物");
            if (wallCount == 0) sb.AppendLine("  ✅ 通道畅通");

            Debug.Log(sb.ToString());
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Ch5H6H7PathCheck.txt", sb.ToString(), new UTF8Encoding(false));
        }
    }
}