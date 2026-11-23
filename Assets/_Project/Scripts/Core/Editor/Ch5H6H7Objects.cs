using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace AbyssEcho.Core.EditorTools {
    public static class Ch5H6H7Objects {
        public static void Run() {
            var sb = new StringBuilder();
            sb.AppendLine("=== H6 / 通道 / H7 之间的所有对象（找可疑触发器）===");
            sb.AppendLine();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_Chapter5_Graybox.unity", OpenSceneMode.Single);
            Physics.SyncTransforms();

            sb.AppendLine($"  {"对象名",-34} {"位置",-30} {"组件",-50} 层");
            sb.AppendLine("  " + new string('-', 150));

            var list = new List<(string n, Vector3 p, string comps, string layer)>();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)) {
                var p = t.position;
                if (p.z < 34f || p.z > 62f) continue;

                var comps = new List<string>();
                foreach (var c in t.gameObject.GetComponents<Component>()) {
                    if (c == null) { comps.Add("MISSING!"); continue; }
                    if (c is Transform || c is MeshFilter || c is MeshRenderer) continue;
                    comps.Add(c.GetType().Name);
                }
                if (comps.Count == 0) continue;

                list.Add((t.name, p, string.Join(",", comps), LayerMask.LayerToName(t.gameObject.layer)));
            }
            list.Sort((a, b) => a.p.z.CompareTo(b.p.z));
            foreach (var e in list)
                sb.AppendLine($"  {e.n,-34} {e.p.ToString(),-30} {e.comps,-50} {e.layer}");

            sb.AppendLine();
            sb.AppendLine("── 只有 Trigger 类组件的对象 ──");
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)) {
                var p = t.position;
                if (p.z < 30f || p.z > 62f) continue;
                bool hasTrigger = false;
                var names = new List<string>();
                foreach (var c in t.gameObject.GetComponents<Component>()) {
                    if (c == null) continue;
                    string tn = c.GetType().Name;
                    if (tn.Contains("Trigger") || tn.Contains("Ending") || tn.Contains("Zone")) {
                        hasTrigger = true; names.Add(tn);
                    }
                    var bc = c as BoxCollider;
                    if (bc != null && bc.isTrigger) { hasTrigger = true; names.Add($"BoxCollider(trigger, size={bc.size})"); }
                }
                if (hasTrigger)
                    sb.AppendLine($"  {t.gameObject.name,-34} @ {p}   {string.Join(",", names)}");
            }

            Debug.Log(sb.ToString());
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Ch5H6H7Objects.txt", sb.ToString(), new UTF8Encoding(false));
        }
    }
}