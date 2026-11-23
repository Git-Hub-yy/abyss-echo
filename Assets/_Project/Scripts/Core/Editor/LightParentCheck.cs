using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace AbyssEcho.Core.EditorTools {
    public static class LightParentCheck {
        public static void Run() {
            var sb = new StringBuilder();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_Chapter4_Graybox.unity", OpenSceneMode.Single);
            var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            sb.AppendLine($"总灯数 = {lights.Length}");
            sb.AppendLine();
            sb.AppendLine("══════ 按父节点分组 ══════");
            var groups = new System.Collections.Generic.Dictionary<string,int>();
            foreach (var l in lights) {
                string p = l.transform.parent != null ? l.transform.parent.name : "(无父)";
                if (!groups.ContainsKey(p)) groups[p] = 0;
                groups[p]++;
            }
            foreach (var kv in groups) sb.AppendLine($"  {kv.Key,-40} {kv.Value} 盏");
            sb.AppendLine();

            sb.AppendLine("══════ 前 20 盏灯的完整路径 + 局部/世界坐标 ══════");
            int n = 0;
            foreach (var l in lights) {
                if (n++ >= 20) break;
                string path = l.gameObject.name;
                var t = l.transform;
                int g = 0;
                while (t.parent != null && g++ < 10) { t = t.parent; path = t.name + "/" + path; }
                sb.AppendLine($"  {path}");
                sb.AppendLine($"    世界={l.transform.position}  本地={l.transform.localPosition}");
            }
            sb.AppendLine();

            sb.AppendLine("══════ 房间根节点 ══════");
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)) {
                if (t.name.StartsWith("Room_")) {
                    sb.AppendLine($"  {t.name,-40} 世界={t.position}");
                }
            }
            Debug.Log(sb.ToString());
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\LightParentCheck.txt", sb.ToString(), new UTF8Encoding(false));
        }
    }
}