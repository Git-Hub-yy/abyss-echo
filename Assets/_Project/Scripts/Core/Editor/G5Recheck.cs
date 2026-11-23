using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace AbyssEcho.Core.EditorTools {
    public static class G5Recheck {
        public static void Run() {
            var sb = new StringBuilder();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_Chapter4_Graybox.unity", OpenSceneMode.Single);
            Physics.SyncTransforms();
            sb.AppendLine("══════ G5 → G7 修复后复查 ══════");

            sb.AppendLine();
            sb.AppendLine("【连廊对象】");
            var link = GameObject.Find("Room_G5_G7_Link_核心西门");
            if (link != null) {
                foreach (Transform c in link.transform) {
                    var bc = c.GetComponent<BoxCollider>();
                    if (bc != null) {
                        var b = bc.bounds;
                        sb.AppendLine($"  {c.name,-32} x[{b.min.x:F1},{b.max.x:F1}] y[{b.min.y:F1},{b.max.y:F1}] z[{b.min.z:F1},{b.max.z:F1}]");
                    }
                }
            }

            sb.AppendLine();
            sb.AppendLine("【地板连续性 x=-19】");
            for (float z = 3f; z <= 11f; z += 1f) {
                bool f = Physics.Raycast(new Vector3(-19, 2f, z), Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore);
                string nm = "";
                if (f) { Physics.Raycast(new Vector3(-19, 2f, z), Vector3.down, out var h, 4f, ~0, QueryTriggerInteraction.Ignore); nm = h.collider.gameObject.name; }
                sb.AppendLine($"  z={z,4:F0}  {(f ? "✅ " + nm : "❌ 无地板")}");
            }

            sb.AppendLine();
            sb.AppendLine("【玩家胶囊通行逐步测试】");
            var pts = new[] {
                new Vector3(-19, 0, 0),
                new Vector3(-19, 0, 4),
                new Vector3(-19, 0, 5.5f),
                new Vector3(-19, 0, 7),
                new Vector3(-18, 0, 8),
                new Vector3(-15, 0, 8),
                new Vector3(-12, 0, 8),
                new Vector3(-10.5f, 0, 8),
            };
            for (int i = 0; i < pts.Length - 1; i++) {
                Vector3 a = pts[i], b = pts[i+1];
                Vector3 dir = (b - a).normalized;
                float d = Vector3.Distance(a, b);
                bool blk = Physics.CapsuleCast(
                    new Vector3(a.x, 0.35f, a.z), new Vector3(a.x, 1.45f, a.z),
                    0.35f, dir, out var hit, d, ~0, QueryTriggerInteraction.Ignore);
                sb.AppendLine($"  ({a.x,6:F1},{a.z,5:F1}) → ({b.x,6:F1},{b.z,5:F1})  {d,4:F1}m  {(blk ? "❌ " + hit.collider.gameObject.name : "✅ 通")}");
            }

            sb.AppendLine();
            sb.AppendLine("【门洞净宽（北墙 z=5）】");
            for (float x = -20.5f; x <= -17.5f; x += 0.25f) {
                bool w = Physics.CheckCapsule(new Vector3(x,0.35f,5f), new Vector3(x,1.45f,5f), 0.35f, ~0, QueryTriggerInteraction.Ignore);
                sb.AppendLine($"  x={x,6:F2}  {(w ? "■ 墙" : "□ 通")}");
            }

            Debug.Log(sb.ToString());
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\G5Recheck.txt", sb.ToString(), new UTF8Encoding(false));
        }
    }
}