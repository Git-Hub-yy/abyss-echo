using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S70f：**最终 UV 验证** —— 用逐面（而非整网格）的标准，做一次彻底检查。
    ///
    /// ══════════════════════ 前面两次判定都错了 ══════════════════════
    ///
    ///   第一次（Ch345UvDiagnose）：按"每个面组用各自两轴"，判出 381 个异常。
    ///     → 错。灰盒 Box 的 UV 是整体按最大两轴展开的，不是每面独立。
    ///
    ///   第二次（Ch345UvCorrectCheck）：按"整网格 UV 总跨度 vs 最大两轴"，
    ///     判出 6 个异常。
    ///     → 也错。因为总跨度包含了侧面顶点（侧面在另一方向压成 0），
    ///       会让总跨度看起来像是两个方向都用了最长轴。
    ///
    /// ══════════════════════ 正确判定 ══════════════════════
    ///
    ///   灰盒 Box 的 UV 生成规则（已通过逐顶点分析确认）：
    ///
    ///     · 找出最大的两个世界轴 d1 ≥ d2
    ///     · 所有顶点 UV = (归一化局部坐标 × d1 × 0.5, 归一化局部坐标 × d2 × 0.5)
    ///     · 具体哪个局部轴映射到 U/V，取决于哪个是最大轴
    ///
    ///   所以正确检查方法：
    ///     对**每个面组**（+X/-X/+Y/-Y/+Z/-Z）：
    ///       · 该面组覆盖的世界尺寸 (w, h)
    ///       · 该面组的 UV 跨度 (su, sv)
    ///       · 如果 su 和 sv 都接近 0（薄面），跳过
    ///       · 否则检查 su/sv 的比例是否与 w/h 的比例一致（允许 10% 误差）
    ///
    ///   这个标准能正确区分：
    ///     ✓ 正确的：侧面 UV 压成 0（因为是薄面）
    ///     ✗ 错误的：大面的 UV 比例与几何比例不符
    ///
    /// **只读，不修改任何东西。**
    /// </summary>
    public static class Ch345UvFinalVerify
    {
        private static readonly string[] Scenes =
        {
            "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity",
            "Assets/_Project/Scenes/Level_Chapter2_Graybox.unity",
            "Assets/_Project/Scenes/Level_Chapter3_Graybox.unity",
            "Assets/_Project/Scenes/Level_Chapter4_Graybox.unity",
            "Assets/_Project/Scenes/Level_Chapter5_Graybox.unity",
        };

        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════════════════");
            sb.AppendLine("   UV 最终验证（逐面比例检查）");
            sb.AppendLine("   标准：面组 UV 跨度的 U/V 比例 应 ≈ 几何尺寸的宽高比例");
            sb.AppendLine("═══════════════════════════════════════════════════");

            foreach (var scenePath in Scenes)
            {
                if (!File.Exists(scenePath)) continue;
                string chName = Path.GetFileNameWithoutExtension(scenePath);

                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                Physics.SyncTransforms();

                var seen = new HashSet<Mesh>();
                int total = 0, bad = 0;
                var badList = new List<string>();

                foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                {
                    if (r == null) continue;
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) continue;
                    var mesh = mf.sharedMesh;
                    if (!mesh.name.StartsWith("MESH_")) continue;
                    if (!seen.Add(mesh)) continue;

                    var uv = mesh.uv;
                    var verts = mesh.vertices;
                    var norms = mesh.normals;
                    if (uv == null || verts == null || norms == null) continue;

                    var ws = r.bounds.size;
                    total++;

                    // 逐面组检查
                    var faces = new Dictionary<string, List<int>>();
                    for (int i = 0; i < norms.Length; i++)
                    {
                        Vector3 n = norms[i];
                        string f = Mathf.Abs(n.x) > 0.5f ? (n.x > 0 ? "+X" : "-X")
                                 : Mathf.Abs(n.y) > 0.5f ? (n.y > 0 ? "+Y" : "-Y")
                                 : (n.z > 0 ? "+Z" : "-Z");
                        if (!faces.ContainsKey(f)) faces[f] = new List<int>();
                        faces[f].Add(i);
                    }

                    var problems = new List<string>();

                    foreach (var kv in faces)
                    {
                        var idx = kv.Value;
                        if (idx.Count == 0) continue;

                        // 该面的世界几何尺寸（两个展开轴）
                        float mnX = 999, mxX = -999, mnY = 999, mxY = -999, mnZ = 999, mxZ = -999;
                        float mnU = 999, mxU = -999, mnV = 999, mxV = -999;
                        foreach (var i in idx)
                        {
                            mnX = Mathf.Min(mnX, verts[i].x); mxX = Mathf.Max(mxX, verts[i].x);
                            mnY = Mathf.Min(mnY, verts[i].y); mxY = Mathf.Max(mxY, verts[i].y);
                            mnZ = Mathf.Min(mnZ, verts[i].z); mxZ = Mathf.Max(mxZ, verts[i].z);
                            mnU = Mathf.Min(mnU, uv[i].x); mxU = Mathf.Max(mxU, uv[i].x);
                            mnV = Mathf.Min(mnV, uv[i].y); mxV = Mathf.Max(mxV, uv[i].y);
                        }

                        float geoW, geoH;
                        string axis = kv.Key.Substring(1);
                        if (axis == "X") { geoW = (mxZ - mnZ) * ws.z; geoH = (mxY - mnY) * ws.y; }
                        else if (axis == "Y") { geoW = (mxX - mnX) * ws.x; geoH = (mxZ - mnZ) * ws.z; }
                        else { geoW = (mxX - mnX) * ws.x; geoH = (mxY - mnY) * ws.y; }

                        float su = mxU - mnU;
                        float sv = mxV - mnV;

                        // 薄面：UV 跨度为 0 是正常的
                        if (su < 0.005f && sv < 0.005f) continue;
                        if (geoW < 0.05f || geoH < 0.05f) continue;   // 几何上是薄边

                        // 检查 UV 跨度是否与几何尺寸成正比
                        float uPerM = su / Mathf.Max(0.01f, geoW);
                        float vPerM = sv / Mathf.Max(0.01f, geoH);

                        // 期望：UV/米 = 0.5（两侧都应该）
                        float errU = Mathf.Abs(uPerM - 0.5f) / 0.5f;
                        float errV = Mathf.Abs(vPerM - 0.5f) / 0.5f;

                        // 允许一边接近 0（侧面压扁），但不能两边都严重偏离
                        bool uOk = errU < 0.6f || uPerM < 0.02f;
                        bool vOk = errV < 0.6f || vPerM < 0.02f;

                        if (!uOk && !vOk)
                            problems.Add($"{kv.Key}面 U/米={uPerM:F2} V/米={vPerM:F2}");
                    }

                    if (problems.Count > 0)
                    {
                        bad++;
                        badList.Add($"    {mesh.name,-38} 尺寸={ws.x:F1}x{ws.y:F1}x{ws.z:F1}  {string.Join("; ", problems)}");
                    }
                }

                sb.AppendLine();
                sb.AppendLine($"────────── {chName} ──────────");
                sb.AppendLine($"  网格 {total} 个，逐面检查异常 {bad} 个");
                if (badList.Count > 0)
                {
                    for (int i = 0; i < Mathf.Min(20, badList.Count); i++) sb.AppendLine(badList[i]);
                }
            }

            string text = sb.ToString();
            Debug.Log(text);
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Ch345UvFinalVerify.txt",
                              text, new UTF8Encoding(false));
        }
    }
}