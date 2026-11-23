using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace AbyssEcho.Core.EditorTools {
    public static class WestB3UvCheck {
        public static void Run() {
            var sb = new StringBuilder();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_Chapter1_Graybox.unity", OpenSceneMode.Single);
            Physics.SyncTransforms();

            foreach (var name in new[] { "West_B3_Full", "East_B3_Full", "North_B3_Full", "South_B3_Seg0" }) {
                var go = GameObject.Find(name);
                if (go == null) { sb.AppendLine($"{name} 不存在"); continue; }
                var mf = go.GetComponent<MeshFilter>();
                var m = mf != null ? mf.sharedMesh : null;
                if (m == null) { sb.AppendLine($"{name} 无网格"); continue; }

                var ws = go.GetComponent<Renderer>().bounds.size;
                sb.AppendLine($"══════ {name} ══════");
                sb.AppendLine($"  世界尺寸 = {ws.x:F2} x {ws.y:F2} x {ws.z:F2}");
                sb.AppendLine($"  网格 = {m.name}  顶点={m.vertexCount}");
                sb.AppendLine($"  材质 = {go.GetComponent<MeshRenderer>().sharedMaterial.name}");
                sb.AppendLine($"  材质Tiling = {go.GetComponent<MeshRenderer>().sharedMaterial.GetTextureScale("_BaseMap")}");

                var uv = m.uv;
                var norms = m.normals;
                // 按法线分组看 UV 跨度
                var mx = new System.Collections.Generic.Dictionary<string, Vector2>();
                var mn = new System.Collections.Generic.Dictionary<string, Vector2>();
                for (int i = 0; i < uv.Length; i++) {
                    string k = $"{norms[i].x:F0},{norms[i].y:F0},{norms[i].z:F0}";
                    if (!mx.ContainsKey(k)) { mx[k] = new Vector2(-999,-999); mn[k] = new Vector2(999,999); }
                    mx[k] = new Vector2(Mathf.Max(mx[k].x, uv[i].x), Mathf.Max(mx[k].y, uv[i].y));
                    mn[k] = new Vector2(Mathf.Min(mn[k].x, uv[i].x), Mathf.Min(mn[k].y, uv[i].y));
                }
                foreach (var k in mx.Keys) {
                    var sp = mx[k] - mn[k];
                    string desc = "";
                    if (k == "1,0,0" || k == "-1,0,0") desc = "（左右侧面）";
                    else if (k == "0,1,0") desc = "（顶面）";
                    else if (k == "0,-1,0") desc = "（底面）";
                    else desc = "（前后大面）";
                    sb.AppendLine($"    法线({k}){desc} UV跨度 = {sp.x:F2} x {sp.y:F2}");
                }
                sb.AppendLine();
            }

            Debug.Log(sb.ToString());
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\WestB3UvCheck.txt", sb.ToString(), new UTF8Encoding(false));
        }
    }
}