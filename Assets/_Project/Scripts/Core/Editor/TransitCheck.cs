using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace AbyssEcho.Core.EditorTools {
    public static class TransitCheck {
        public static void Run() {
            var sb = new StringBuilder();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_Chapter4_Graybox.unity", OpenSceneMode.Single);
            Physics.SyncTransforms();
            sb.AppendLine("══════ Transit_Floor 是否还在 ══════");
            var tf = GameObject.Find("Transit_Floor");
            if (tf == null) {
                sb.AppendLine("❌ Transit_Floor 不存在！");
                sb.AppendLine("搜索所有含 Transit 的对象：");
                foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                    if (t.name.Contains("Transit")) sb.AppendLine($"  {t.name} @ {t.position}");
            } else {
                var bc = tf.GetComponent<BoxCollider>();
                var b = bc.bounds;
                sb.AppendLine($"✅ Transit_Floor 存在");
                sb.AppendLine($"   位置 = {tf.transform.position}");
                sb.AppendLine($"   包围盒 x[{b.min.x:F2},{b.max.x:F2}] y[{b.min.y:F2},{b.max.y:F2}] z[{b.min.z:F2},{b.max.z:F2}]");
                sb.AppendLine($"   active = {tf.activeInHierarchy}");
            }
            sb.AppendLine();
            sb.AppendLine("══════ z=6 处逐点地板检测（x 从 -22 到 -8）══════");
            for (float x = -22f; x <= -8f; x += 1f) {
                bool f = Physics.Raycast(new Vector3(x, 2f, 6f), Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore);
                string nm = "";
                if (f) { Physics.Raycast(new Vector3(x,2f,6f), Vector3.down, out var h, 4f, ~0, QueryTriggerInteraction.Ignore); nm = h.collider.gameObject.name; }
                sb.AppendLine($"  x={x,6:F1}  {(f ? "✅ " + nm : "❌ 无地板")}");
            }
            sb.AppendLine();
            sb.AppendLine("══════ z=5.5 处逐点地板检测 ══════");
            for (float x = -22f; x <= -8f; x += 1f) {
                bool f = Physics.Raycast(new Vector3(x, 2f, 5.5f), Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore);
                string nm = "";
                if (f) { Physics.Raycast(new Vector3(x,2f,5.5f), Vector3.down, out var h, 4f, ~0, QueryTriggerInteraction.Ignore); nm = h.collider.gameObject.name; }
                sb.AppendLine($"  x={x,6:F1}  {(f ? "✅ " + nm : "❌ 无地板")}");
            }
            Debug.Log(sb.ToString());
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\TransitCheck.txt", sb.ToString(), new UTF8Encoding(false));
        }
    }
}