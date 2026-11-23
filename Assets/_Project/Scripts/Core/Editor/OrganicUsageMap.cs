using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace AbyssEcho.Core.EditorTools {
    public static class OrganicUsageMap {
        public static void Run() {
            var sb = new StringBuilder();
            sb.AppendLine("=== 有机材质在各章使用数量 ===");
            var scenes = new (int ch, string p)[] {
                (3,"Assets/_Project/Scenes/Level_Chapter3_Graybox.unity"),
                (4,"Assets/_Project/Scenes/Level_Chapter4_Graybox.unity"),
                (5,"Assets/_Project/Scenes/Level_Chapter5_Graybox.unity"),
            };
            var map = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<int,int>>();
            foreach (var (ch,p) in scenes) {
                EditorSceneManager.OpenScene(p, OpenSceneMode.Single);
                foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)) {
                    if (r == null || r.sharedMaterial == null) continue;
                    string n = r.sharedMaterial.name;
                    if (!n.StartsWith("M_Org_")) continue;
                    if (!map.ContainsKey(n)) map[n] = new System.Collections.Generic.Dictionary<int,int>();
                    if (!map[n].ContainsKey(ch)) map[n][ch] = 0;
                    map[n][ch]++;
                }
            }
            foreach (var kv in map) {
                var parts = new System.Collections.Generic.List<string>();
                foreach (var c in kv.Value) parts.Add($"Ch{c.Key}={c.Value}");
                sb.AppendLine($"  {kv.Key,-24} {string.Join("  ", parts)}");
            }
            Debug.Log(sb.ToString());
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\OrganicUsageMap.txt", sb.ToString(), new UTF8Encoding(false));
        }
    }
}