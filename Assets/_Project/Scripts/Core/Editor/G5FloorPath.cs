using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace AbyssEcho.Core.EditorTools {
    public static class G5FloorPath {
        public static void Run() {
            var sb = new StringBuilder();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_Chapter4_Graybox.unity", OpenSceneMode.Single);
            Physics.SyncTransforms();
            sb.AppendLine("══════ 真实路径地板连续性 ══════");
            sb.AppendLine("路线: G5(-19,0) → 北门洞(-19,5) → 连廊(-12,7) → (-11,8) → G7(-9,8)");

            // 沿真实路径采样
            var path = new[] {
                new Vector3(-19, 0, 0),
                new Vector3(-19, 0, 2),
                new Vector3(-19, 0, 4),
                new Vector3(-19, 0, 5),
                new Vector3(-19, 0, 5.5f),
                new Vector3(-18, 0, 6),
                new Vector3(-16, 0, 6.5f),
                new Vector3(-14, 0, 7),
                new Vector3(-12.5f, 0, 7.5f),
                new Vector3(-11.5f, 0, 8),
                new Vector3(-10.5f, 0, 8),
                new Vector3(-9.5f, 0, 8),
                new Vector3(-8, 0, 8.5f),
                new Vector3(-5, 0, 10),
                new Vector3(0, 0, 13),
                new Vector3(0, 0, 16),
            };

            sb.AppendLine();
            int gaps = 0;
            foreach (var p in path) {
                bool f = Physics.Raycast(p + Vector3.up * 2f, Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore);
                string nm = "";
                float topY = 0;
                if (f) {
                    Physics.Raycast(p + Vector3.up * 2f, Vector3.down, out var h, 4f, ~0, QueryTriggerInteraction.Ignore);
                    nm = h.collider.gameObject.name;
                    topY = h.point.y;
                } else gaps++;
                sb.AppendLine($"  ({p.x,6:F1},{p.z,5:F1})  {(f ? $"✅ {nm,-24} 顶面Y={topY:F2}" : "❌ 无地板")}");
            }
            sb.AppendLine();
            sb.AppendLine($"地板缺口数 = {gaps}");

            Debug.Log(sb.ToString());
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\G5FloorPath.txt", sb.ToString(), new UTF8Encoding(false));
        }
    }
}