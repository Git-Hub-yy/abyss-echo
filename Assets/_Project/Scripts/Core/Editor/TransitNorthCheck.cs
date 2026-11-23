using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace AbyssEcho.Core.EditorTools {
    public static class TransitNorthCheck {
        public static void Run() {
            var sb = new StringBuilder();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_Chapter4_Graybox.unity", OpenSceneMode.Single);
            Physics.SyncTransforms();
            sb.AppendLine("══════ 过渡区北边 (z>6.9) 有什么 ══════");
            sb.AppendLine("查询 x[-21,-12] z[7,12] 的所有碰撞体");
            foreach (var c in Object.FindObjectsByType<BoxCollider>(FindObjectsSortMode.None)) {
                var b = c.bounds;
                if (b.max.x < -21 || b.min.x > -12) continue;
                if (b.max.z < 7 || b.min.z > 12) continue;
                sb.AppendLine($"  {c.gameObject.name,-30} x[{b.min.x:F1},{b.max.x:F1}] y[{b.min.y:F1},{b.max.y:F1}] z[{b.min.z:F1},{b.max.z:F1}]");
            }
            sb.AppendLine();
            sb.AppendLine("══════ z=7.5 处是否有地板 ══════");
            for (float x = -20f; x <= -11f; x += 1f) {
                bool f = Physics.Raycast(new Vector3(x,2f,7.5f), Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore);
                sb.AppendLine($"  x={x,6:F1}  {(f ? "有地板" : "无地板")}");
            }
            sb.AppendLine();
            sb.AppendLine("══════ 如果把 Transit_Wall_North 开洞，玩家会看到什么 ══════");
            sb.AppendLine("  z=7.0~9.2 x[-13.0,-10.0] 是连廊区域（有地板）");
            sb.AppendLine("  z=7.0~9.2 x[-20.2,-13.0] 呢？");
            for (float x = -20f; x <= -13f; x += 1f) {
                bool f = Physics.Raycast(new Vector3(x,2f,8f), Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore);
                sb.AppendLine($"     x={x,6:F1} z=8  {(f ? "有地板" : "❌ 无地板(虚空)")}");
            }
            Debug.Log(sb.ToString());
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\TransitNorthCheck.txt", sb.ToString(), new UTF8Encoding(false));
        }
    }
}