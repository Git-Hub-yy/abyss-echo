using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace AbyssEcho.Core.EditorTools {
    public static class G5DoorCheck {
        public static void Run() {
            var sb = new StringBuilder();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_Chapter4_Graybox.unity", OpenSceneMode.Single);
            Physics.SyncTransforms();

            sb.AppendLine("══════ G5 北墙相关对象 ══════");
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)) {
                string n = r.gameObject.name;
                if (!n.Contains("North_G5") && !n.Contains("Transit") && !n.Contains("G5Door")) continue;
                var b = r.bounds;
                sb.AppendLine($"  {n,-34} X[{b.min.x,7:F2},{b.max.x,7:F2}] Y[{b.min.y,5:F2},{b.max.y,5:F2}] Z[{b.min.z,7:F2},{b.max.z,7:F2}]");
            }

            sb.AppendLine();
            sb.AppendLine("══════ 在 z=5~9 之间，沿 x 扫描地面 ══════");
            sb.AppendLine($"{"x",-8} 地面范围 Z");
            for (float x = -26f; x <= -8f; x += 1f) {
                var found = new System.Collections.Generic.List<string>();
                foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)) {
                    string n = r.gameObject.name;
                    if (!n.StartsWith("Floor")) continue;
                    var b = r.bounds;
                    if (x < b.min.x || x > b.max.x) continue;
                    if (b.max.z < 4f || b.min.z > 10f) continue;
                    found.Add($"{n} Z[{b.min.z:F1},{b.max.z:F1}]");
                }
                sb.AppendLine($"  {x,-8:F0} {(found.Count > 0 ? string.Join(" | ", found) : "❌ 无")}");
            }

            Debug.Log(sb.ToString());
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\G5DoorCheck.txt", sb.ToString(), new UTF8Encoding(false));
        }
    }
}