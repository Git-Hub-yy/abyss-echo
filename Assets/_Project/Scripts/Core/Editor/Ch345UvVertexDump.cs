using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S70c：逐顶点分析一个典型异常网格的 UV 结构。
    ///
    /// 目的：确认「按法线分组」这个前提是否成立。
    /// 灰盒 Box 是 24 顶点，但**顶点可能被多个面共享**，
    /// 或者生成器用了另一种分组方式。
    ///
    /// 本工具输出：
    ///   · 每个顶点的 位置 / UV / 法线
    ///   · 每组法线下的 UV 值分布
    ///   · 与"标准 Box UV"的对比
    ///
    /// 只读。
    /// </summary>
    public static class Ch345UvVertexDump
    {
        private static readonly string[] TargetMeshes =
        {
            "MESH_East_F10_Full",     // 第三章，0.3 × 2.8 × 4.0
            "MESH_South_A2_SegEnd",   // 第一章（已修复），1.8 × 3.2 × 0.3
        };

        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("═════════════════════════════════════════════");
            sb.AppendLine("  逐顶点 UV 结构分析");
            sb.AppendLine("═════════════════════════════════════════════");

            // 第三章
            DumpFromScene(sb, "Assets/_Project/Scenes/Level_Chapter3_Graybox.unity",
                          "MESH_East_F10_Full", "第三章 · 异常样本");

            // 第一章
            DumpFromScene(sb, "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity",
                          "MESH_South_A2_SegEnd", "第一章 · 已修复样本");

            string text = sb.ToString();
            Debug.Log(text);
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\UvVertexDump.txt",
                              text, new UTF8Encoding(false));
        }

        private static void DumpFromScene(StringBuilder sb, string scenePath,
                                           string meshName, string title)
        {
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            Physics.SyncTransforms();

            MeshRenderer found = null;
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (r == null) continue;
                var mf = r.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null && mf.sharedMesh.name == meshName)
                {
                    found = r;
                    break;
                }
            }

            sb.AppendLine();
            sb.AppendLine($"══════ {title} ══════");
            if (found == null) { sb.AppendLine($"  ❌ 找不到 {meshName}"); return; }

            var mesh = found.GetComponent<MeshFilter>().sharedMesh;
            var ws = found.bounds.size;
            sb.AppendLine($"  网格 {mesh.name}");
            sb.AppendLine($"  对象 {found.gameObject.name}");
            sb.AppendLine($"  尺寸 {ws.x:F3} × {ws.y:F3} × {ws.z:F3}");
            sb.AppendLine($"  顶点 {mesh.vertexCount}  三角形 {mesh.triangles.Length / 3}  子网格 {mesh.subMeshCount}");

            var verts = mesh.vertices;
            var uv = mesh.uv;
            var norms = mesh.normals;

            sb.AppendLine();
            sb.AppendLine($"  {"idx",-4} {"位置(x,y,z)",-26} {"UV(u,v)",-20} {"法线",-16} 所属面");
            sb.AppendLine("  " + new string('-', 90));

            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 n = norms[i];
                string face;
                if (Mathf.Abs(n.x) > 0.5f) face = n.x > 0 ? "+X" : "-X";
                else if (Mathf.Abs(n.y) > 0.5f) face = n.y > 0 ? "+Y" : "-Y";
                else face = n.z > 0 ? "+Z" : "-Z";

                sb.AppendLine($"  {i,-4} ({verts[i].x,6:F2},{verts[i].y,6:F2},{verts[i].z,6:F2})" +
                              $"   ({uv[i].x,7:F3},{uv[i].y,7:F3})" +
                              $"     ({n.x:F0},{n.y:F0},{n.z:F0})      {face}");
            }

            // 按面分组统计
            sb.AppendLine();
            sb.AppendLine("  按面分组统计：");
            var faces = new Dictionary<string, List<int>>();
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 n = norms[i];
                string f = Mathf.Abs(n.x) > 0.5f ? (n.x > 0 ? "+X" : "-X")
                         : Mathf.Abs(n.y) > 0.5f ? (n.y > 0 ? "+Y" : "-Y")
                         : (n.z > 0 ? "+Z" : "-Z");
                if (!faces.ContainsKey(f)) faces[f] = new List<int>();
                faces[f].Add(i);
            }

            foreach (var kv in faces)
            {
                float mnU = 999, mxU = -999, mnV = 999, mxV = -999;
                float mnX = 999, mxX = -999, mnY = 999, mxY = -999, mnZ = 999, mxZ = -999;
                foreach (var i in kv.Value)
                {
                    mnU = Mathf.Min(mnU, uv[i].x); mxU = Mathf.Max(mxU, uv[i].x);
                    mnV = Mathf.Min(mnV, uv[i].y); mxV = Mathf.Max(mxV, uv[i].y);
                    mnX = Mathf.Min(mnX, verts[i].x); mxX = Mathf.Max(mxX, verts[i].x);
                    mnY = Mathf.Min(mnY, verts[i].y); mxY = Mathf.Max(mxY, verts[i].y);
                    mnZ = Mathf.Min(mnZ, verts[i].z); mxZ = Mathf.Max(mxZ, verts[i].z);
                }
                sb.AppendLine($"    {kv.Key}: {kv.Value.Count} 顶点");
                sb.AppendLine($"      UV 范围 U[{mnU:F3},{mxU:F3}] V[{mnV:F3},{mxV:F3}]  跨度 {mxU - mnU:F3} × {mxV - mnV:F3}");
                sb.AppendLine($"      局部范围 X[{mnX:F2},{mxX:F2}] Y[{mnY:F2},{mxY:F2}] Z[{mnZ:F2},{mxZ:F2}]");
                sb.AppendLine($"      → 该面覆盖的世界尺寸 {Mathf.Abs(mxX - mnX) * ws.x:F2} × {Mathf.Abs(mxY - mnY) * ws.y:F2} × {Mathf.Abs(mxZ - mnZ) * ws.z:F2}");
            }
        }
    }
}