using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace AbyssEcho.Core.EditorTools {
    public static class G5BlockerList {
        public static void Run() {
            var sb = new StringBuilder();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_Chapter4_Graybox.unity", OpenSceneMode.Single);
            Physics.SyncTransforms();
            sb.AppendLine("══════ 拐角区障碍物清单 ══════");
            sb.AppendLine("查询区域: x[-16,-10] z[5,9]");
            sb.AppendLine();
            foreach (var c in Object.FindObjectsByType<BoxCollider>(FindObjectsSortMode.None)) {
                var b = c.bounds;
                // 只要与查询区有交集
                if (b.max.x < -16 || b.min.x > -10) continue;
                if (b.max.z < 5 || b.min.z > 9) continue;
                if (b.max.y < 0.1f) continue;   // 忽略地板
                sb.AppendLine($"  {c.gameObject.name,-28} x[{b.min.x:F2},{b.max.x:F2}] y[{b.min.y:F2},{b.max.y:F2}] z[{b.min.z:F2},{b.max.z:F2}]");
            }
            sb.AppendLine();
            sb.AppendLine("══════ 沿 z=6.8 扫描，x 从 -20 到 -10 ══════");
            for (float x = -20f; x <= -10f; x += 0.4f) {
                var hits = Physics.OverlapCapsule(
                    new Vector3(x, 0.35f, 6.8f), new Vector3(x, 1.45f, 6.8f),
                    0.35f, ~0, QueryTriggerInteraction.Ignore);
                var names = new System.Collections.Generic.List<string>();
                foreach (var h in hits) if (h != null && !h.isTrigger) names.Add(h.gameObject.name);
                sb.AppendLine($"  x={x,6:F1}  {(names.Count == 0 ? "□ 通" : "■ " + string.Join(",", names))}");
            }

            Debug.Log(sb.ToString());
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\G5BlockerList.txt", sb.ToString(), new UTF8Encoding(false));
        }
    }
}