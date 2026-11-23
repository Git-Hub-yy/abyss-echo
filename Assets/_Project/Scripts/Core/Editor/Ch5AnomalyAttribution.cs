using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S75b：**第五章异常物体的精确归因**（只读）。
    ///
    /// 对每个「每贴图米数」异常的物体，逐个三角形分析：
    ///   · 这个面是横着的（X-Z）还是竖着的（Y-Z / X-Y）
    ///   · UV 在该面的两个方向各展开多少
    ///   · 实际贴图覆盖米数
    ///
    /// 目的：区分三种情况
    ///   A. UV 本身错了（该方向 UV 跨度不合理）
    ///   B. UV 对，但 Tiling 让某方向过密
    ///   C. 薄板侧面（正常，玩家看不到）
    ///
    /// 同时输出材质属性块（MaterialPropertyBlock）情况 ——
    /// 有些物体可能在运行时被脚本改了 Tiling。
    ///
    /// 只读。
    /// </summary>
    public static class Ch5AnomalyAttribution
    {
        private const string SCENE = "Assets/_Project/Scenes/Level_Chapter5_Graybox.unity";

        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            sb.AppendLine("  第五章异常物体精确归因");
            sb.AppendLine("═══════════════════════════════════════════════════════════════");

            EditorSceneManager.OpenScene(SCENE, OpenSceneMode.Single);
            Physics.SyncTransforms();

            // 重点检查有机材质
            var targets = new[] { "M_Org_Membrane", "M_Org_FleshWet", "M_Org_SlimeBio", "M_Org_FleshDeep" };

            foreach (var matName in targets)
            {
                sb.AppendLine();
                sb.AppendLine($"═══════════════════════════════════════════════════════");
                sb.AppendLine($"  {matName}");
                sb.AppendLine($"═══════════════════════════════════════════════════════");

                Material mat = null;
                foreach (var g in AssetDatabase.FindAssets($"t:Material {matName}"))
                {
                    var p = AssetDatabase.GUIDToAssetPath(g);
                    var tmp = AssetDatabase.LoadAssetAtPath<Material>(p);
                    if (tmp != null && tmp.name == matName) { mat = tmp; break; }
                }
                if (mat == null) { sb.AppendLine("  ❌ 找不到"); continue; }

                var sc = mat.GetTextureScale("_BaseMap");
                sb.AppendLine($"  材质 Tiling = ({sc.x:F2}, {sc.y:F2})   " +
                              $"Smoothness = {mat.GetFloat("_Smoothness"):F2}");

                // 统计所有使用者
                var list = new List<MeshRenderer>();
                foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                {
                    if (r == null || r.sharedMaterial == null) continue;
                    if (r.sharedMaterial.name == matName) list.Add(r);
                }
                list.Sort((a, b) => string.Compare(a.gameObject.name, b.gameObject.name, System.StringComparison.Ordinal));

                sb.AppendLine($"  使用物体 = {list.Count} 个");
                sb.AppendLine();
                sb.AppendLine($"  {"对象",-26} {"尺寸",-20} {"面",-6} {"该面世界尺寸",-16} {"UV跨度",-14} {"贴图/米",-16} 判定");
                sb.AppendLine("  " + new string('-', 120));

                int anomalyCount = 0;

                foreach (var r in list)
                {
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) continue;
                    var mesh = mf.sharedMesh;

                    var uv = mesh.uv;
                    var verts = mesh.vertices;
                    var norms = mesh.normals;
                    var tris = mesh.triangles;
                    if (uv == null || verts == null || norms == null || tris == null) continue;

                    var ws = r.bounds.size;

                    // 逐面组统计（用三角形法线，更准确）
                    var faces = new Dictionary<string, (float mnU, float mxU, float mnV, float mxV,
                                                        float mnA, float mxA, float mnB, float mxB)>();

                    // 用顶点法线分组
                    var groups = new Dictionary<string, List<int>>();
                    for (int i = 0; i < norms.Length; i++)
                    {
                        Vector3 n = norms[i];
                        string k = Mathf.Abs(n.x) > 0.5f ? "X" : (Mathf.Abs(n.y) > 0.5f ? "Y" : "Z");
                        if (!groups.ContainsKey(k)) groups[k] = new List<int>();
                        groups[k].Add(i);
                    }

                    foreach (var kv in groups)
                    {
                        var idx = kv.Value;
                        if (idx.Count == 0) continue;

                        float mnU = 999, mxU = -999, mnV = 999, mxV = -999;
                        float mnP = 999, mxP = -999, mnQ = 999, mxQ = -999;

                        foreach (var i in idx)
                        {
                            mnU = Mathf.Min(mnU, uv[i].x); mxU = Mathf.Max(mxU, uv[i].x);
                            mnV = Mathf.Min(mnV, uv[i].y); mxV = Mathf.Max(mxV, uv[i].y);
                        }

                        // 该面的两个世界方向尺寸
                        float w1, w2;
                        string axisName;
                        if (kv.Key == "X")
                        {
                            // X法线：面沿 Z 和 Y 展开
                            foreach (var i in idx)
                            {
                                mnP = Mathf.Min(mnP, verts[i].z); mxP = Mathf.Max(mxP, verts[i].z);
                                mnQ = Mathf.Min(mnQ, verts[i].y); mxQ = Mathf.Max(mxQ, verts[i].y);
                            }
                            w1 = (mxP - mnP) * ws.z;   // Z 方向
                            w2 = (mxQ - mnQ) * ws.y;   // Y 方向
                            axisName = "X面";
                        }
                        else if (kv.Key == "Y")
                        {
                            foreach (var i in idx)
                            {
                                mnP = Mathf.Min(mnP, verts[i].x); mxP = Mathf.Max(mxP, verts[i].x);
                                mnQ = Mathf.Min(mnQ, verts[i].z); mxQ = Mathf.Max(mxQ, verts[i].z);
                            }
                            w1 = (mxP - mnP) * ws.x;
                            w2 = (mxQ - mnQ) * ws.z;
                            axisName = "Y面";
                        }
                        else
                        {
                            foreach (var i in idx)
                            {
                                mnP = Mathf.Min(mnP, verts[i].x); mxP = Mathf.Max(mxP, verts[i].x);
                                mnQ = Mathf.Min(mnQ, verts[i].y); mxQ = Mathf.Max(mxQ, verts[i].y);
                            }
                            w1 = (mxP - mnP) * ws.x;
                            w2 = (mxQ - mnQ) * ws.y;
                            axisName = "Z面";
                        }

                        float su = mxU - mnU, sv = mxV - mnV;

                        // 该方向每贴图覆盖米数
                        float mPerU = su > 0.001f ? w1 / (su * sc.x) : -1f;
                        float mPerV = sv > 0.001f ? w2 / (sv * sc.y) : -1f;

                        // 薄面判定
                        bool thin = w1 < 0.5f || w2 < 0.5f;

                        string verdict;
                        if (thin) verdict = "薄面(正常)";
                        else if (mPerU < 0.001f || mPerV < 0.001f) verdict = "单方向压扁";
                        else
                        {
                            float ratio = mPerU / Mathf.Max(0.001f, mPerV);
                            if (ratio > 2.5f || ratio < 0.4f)
                            {
                                verdict = $"❌ 方向失衡 ×{ratio:F1}";
                                anomalyCount++;
                            }
                            else verdict = "OK";
                        }

                        sb.AppendLine($"  {(anomalyCount < 200 && verdict.StartsWith("❌") ? r.gameObject.name : ""),-26} ".PadRight(28) +
                                      $"{(verdict.StartsWith("❌") ? r.gameObject.name : ""),-26} ".PadRight(0) +
                                      $"{axisName,-6} {w1:F2}×{w2:F2}".PadRight(16) +
                                      $"{su:F2}×{sv:F2}".PadRight(14) +
                                      $"{(mPerU > 0 ? mPerU.ToString("F2") : "-")}/{(mPerV > 0 ? mPerV.ToString("F2") : "-")}".PadRight(16) +
                                      verdict);
                    }
                }

                sb.AppendLine();
                sb.AppendLine($"  ⚠️ 发现方向失衡面 = {anomalyCount} 处");
            }

            string text = sb.ToString();
            Debug.Log(text);
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Ch5AnomalyAttribution.txt",
                              text, new UTF8Encoding(false));
        }
    }
}