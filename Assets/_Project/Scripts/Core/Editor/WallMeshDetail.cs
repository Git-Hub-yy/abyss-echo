using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace AbyssEcho.Core.EditorTools {
    public static class WallMeshDetail {
        public static void Run() {
            var sb = new StringBuilder();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_Chapter1_Graybox.unity", OpenSceneMode.Single);
            Physics.SyncTransforms();

            var go = GameObject.Find("East_C3_Seg0");
            if (go == null) { sb.AppendLine("找不到"); Debug.Log(sb.ToString()); return; }
            var mf = go.GetComponent<MeshFilter>();
            var mesh = mf.sharedMesh;
            sb.AppendLine($"══════ East_C3_Seg0 网格细节 ══════");
            sb.AppendLine($"  世界尺寸 = {go.GetComponent<Renderer>().bounds.size}");
            sb.AppendLine($"  顶点数 = {mesh.vertexCount}  子网格 = {mesh.subMeshCount}");
            var verts = mesh.vertices;
            var uv = mesh.uv;
            var norms = mesh.normals;

            sb.AppendLine();
            sb.AppendLine("  前 8 个顶点 (局部坐标 / UV / 法线)：");
            for (int i = 0; i < Mathf.Min(8, verts.Length); i++)
                sb.AppendLine($"    [{i}] v=({verts[i].x:F2},{verts[i].y:F2},{verts[i].z:F2}) uv=({uv[i].x:F2},{uv[i].y:F2}) n=({norms[i].x:F0},{norms[i].y:F0},{norms[i].z:F0})");

            // 按法线分组统计 UV
            sb.AppendLine();
            sb.AppendLine("  按面朝向统计 UV 跨度：");
            var groups = new System.Collections.Generic.Dictionary<string, Vector2>();
            var groupsMin = new System.Collections.Generic.Dictionary<string, Vector2>();
            for (int i = 0; i < verts.Length; i++) {
                string key = $"{norms[i].x:F0},{norms[i].y:F0},{norms[i].z:F0}";
                if (!groups.ContainsKey(key)) { groups[key] = new Vector2(-999,-999); groupsMin[key] = new Vector2(999,999); }
                groups[key] = new Vector2(Mathf.Max(groups[key].x, uv[i].x), Mathf.Max(groups[key].y, uv[i].y));
                groupsMin[key] = new Vector2(Mathf.Min(groupsMin[key].x, uv[i].x), Mathf.Min(groupsMin[key].y, uv[i].y));
            }
            foreach (var k in groups.Keys) {
                var span = groups[k] - groupsMin[k];
                sb.AppendLine($"    法线({k}): U跨度={span.x:F2} V跨度={span.y:F2}");
            }

            sb.AppendLine();
            sb.AppendLine("══════ 对比几个墙的 UV 跨度 vs 世界尺寸 ══════");
            sb.AppendLine($"{"对象",-26} {"世界尺寸",-24} {"UV跨度",-16} 比例");
            sb.AppendLine(new string('-', 86));
            int n = 0;
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)) {
                if (r.sharedMaterial == null || r.sharedMaterial.name != "M_Arch_WallSteel") continue;
                if (n++ >= 10) break;
                var m = r.GetComponent<MeshFilter>()?.sharedMesh;
                if (m == null || m.uv == null || m.uv.Length == 0) continue;
                float mnU=999,mxU=-999,mnV=999,mxV=-999;
                foreach (var v in m.uv) { mnU=Mathf.Min(mnU,v.x); mxU=Mathf.Max(mxU,v.x); mnV=Mathf.Min(mnV,v.y); mxV=Mathf.Max(mxV,v.y); }
                var ws = r.bounds.size;
                // 墙一般是 x或z很薄，取另两个轴
                float w1, w2;
                if (ws.x < ws.z) { w1 = ws.z; w2 = ws.y; } else { w1 = ws.x; w2 = ws.y; }
                float ru = (mxU-mnU) / Mathf.Max(0.01f, w1);
                float rv = (mxV-mnV) / Mathf.Max(0.01f, w2);
                sb.AppendLine($"{r.gameObject.name,-26} {ws.x:F1}x{ws.y:F1}x{ws.z:F1}       {(mxU-mnU):F2}x{(mxV-mnV):F2}     U/米={ru:F3} V/米={rv:F3}");
            }

            Debug.Log(sb.ToString());
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\WallMeshDetail.txt", sb.ToString(), new UTF8Encoding(false));
        }
    }
}