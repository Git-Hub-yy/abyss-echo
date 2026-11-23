using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace AbyssEcho.Core.EditorTools {
    public static class MeshAssetCheck {
        public static void Run() {
            var sb = new StringBuilder();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_Chapter1_Graybox.unity", OpenSceneMode.Single);

            var seen = new System.Collections.Generic.HashSet<Mesh>();
            int inScene = 0, inAsset = 0, other = 0;
            foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None)) {
                var m = mf.sharedMesh;
                if (m == null || !seen.Add(m)) continue;
                if (!m.name.StartsWith("MESH_")) continue;
                string p = AssetDatabase.GetAssetPath(m);
                if (string.IsNullOrEmpty(p)) inScene++;
                else if (p.EndsWith(".asset")) inAsset++;
                else other++;
                if (seen.Count <= 8) sb.AppendLine($"  {m.name,-32} path=\"{p}\"");
            }
            sb.AppendLine();
            sb.AppendLine($"MESH_ 网格：场景内嵌={inScene}  .asset资源={inAsset}  其他={other}");

            // 检查网格是否可读
            var first = Object.FindFirstObjectByType<MeshFilter>()?.sharedMesh;
            if (first != null) sb.AppendLine($"  isReadable（第一个网格）= {first.isReadable}");

            Debug.Log(sb.ToString());
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\MeshAssetCheck.txt", sb.ToString(), new UTF8Encoding(false));
        }
    }
}