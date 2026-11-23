using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace AbyssEcho.Core.EditorTools {
    public static class FindLinkObj {
        public static void Run() {
            var sb = new StringBuilder();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_Chapter4_Graybox.unity", OpenSceneMode.Single);
            sb.AppendLine("=== 所有含 Link/G5 的对象 ===");
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)) {
                if (t.name.Contains("Link") || t.name.Contains("G5")) {
                    var bc = t.GetComponent<BoxCollider>();
                    string box = "";
                    if (bc != null) { var b = bc.bounds; box = $" x[{b.min.x:F1},{b.max.x:F1}] y[{b.min.y:F1},{b.max.y:F1}] z[{b.min.z:F1},{b.max.z:F1}]"; }
                    string parent = t.parent != null ? t.parent.name : "无";
                    sb.AppendLine($"  {t.name,-34} parent={parent,-22}{box}");
                }
            }
            sb.AppendLine();
            sb.AppendLine("=== North_G5 当前分段 ===");
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)) {
                if (t.name.StartsWith("North_G5")) {
                    var b = t.GetComponent<BoxCollider>().bounds;
                    sb.AppendLine($"  {t.name,-24} x[{b.min.x:F2},{b.max.x:F2}] z[{b.min.z:F2},{b.max.z:F2}]");
                }
            }
            Debug.Log(sb.ToString());
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\FindLinkObj.txt", sb.ToString(), new UTF8Encoding(false));
        }
    }
}