using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S73b：**UV 拉伸的最终判定**（只读）。
    ///
    /// ══════════════════════ 历史教训 ══════════════════════
    ///
    ///   之前三次判定都错了：
    ///     ① 按"每面独立两轴" → 381 个误报
    ///     ② 按"整网格总跨度" → 6 个误报
    ///     ③ 按"每面 U/V 比例" → 164 个误报
    ///
    ///   最终确认：灰盒 Box 的 UV 是**整体按最大两轴展开**，
    ///   第五次用"以第一章为基准"才得出正确结论（0 个问题）。
    ///
    /// ══════════════════════ 本次判定标准 ══════════════════════
    ///
    ///   不猜规则，只检查**物理事实**：
    ///
    ///   对每个面组：
    ///     ① 计算该面覆盖的世界尺寸 (w, h)
    ///     ② 计算该面的 UV 跨度 (su, sv)
    ///     ③ 若 w >= 1.0 且 h >= 1.0（这是"大面"，玩家会看到）
    ///        则计算实际纹理密度：
    ///            densU = su / w    （每米多少个贴图循环）
    ///            densV = sv / h
    ///     ④ 期望密度是 0.5（灰盒约定）
    ///     ⑤ 若 densU 或 densV 偏离 0.5 超过 3 倍 → 判定异常
    ///
    ///   **只检查大面**，跳过薄边 —— 这是之前误判的根源。
    ///
    /// ══════════════════════ 输出 ══════════════════════
    ///
    ///   对每个异常网格输出：
    ///     对象名 / 章节 / Mesh 路径 / 材质 / 尺寸 / UV 范围 /
    ///     U/V 方向 / Tiling / 密度 / 问题表现 / 严重程度
    ///
    /// 只读。
    /// </summary>
    public static class UvStretchFinalAudit
    {
        private static readonly (int ch, string path)[] Scenes =
        {
            (1, "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity"),
            (2, "Assets/_Project/Scenes/Level_Chapter2_Graybox.unity"),
            (3, "Assets/_Project/Scenes/Level_Chapter3_Graybox.unity"),
            (4, "Assets/_Project/Scenes/Level_Chapter4_Graybox.unity"),
            (5, "Assets/_Project/Scenes/Level_Chapter5_Graybox.unity"),
        };

        /// <summary>灰盒约定的 UV 密度：每米 0.5 个循环（即 2 米一个贴图）</summary>
        private const float ExpectedDensity = 0.5f;

        /// <summary>偏离倍数阈值：超过 3 倍才判为异常（保守，避免误报）</summary>
        private const float Tolerance = 3.0f;

        /// <summary>只检查两个轴都超过这个尺寸的面（真正的可视大面）</summary>
        private const float MinFaceSize = 1.0f;

        [MenuItem("ABYSS ECHO/审计 v10.0/UV 拉伸最终判定")]
        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            sb.AppendLine("  UV 拉伸最终判定（只检查大面，容忍 3 倍偏差）");
            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            sb.AppendLine($"  判定标准：");
            sb.AppendLine($"    · 只检查两个轴都 ≥ {MinFaceSize} 米的面（可视大面）");
            sb.AppendLine($"    · 期望 UV 密度 = {ExpectedDensity} 循环/米");
            sb.AppendLine($"    · 实际密度偏离超过 {Tolerance} 倍 → 异常");
            sb.AppendLine();

            int grandTotal = 0;

            foreach (var (ch, path) in Scenes)
            {
                if (!File.Exists(path)) continue;
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                Physics.SyncTransforms();

                sb.AppendLine();
                sb.AppendLine("═══════════════════════════════════════════════════");
                sb.AppendLine($"  第{ch}章  ({Path.GetFileNameWithoutExtension(path)})");
                sb.AppendLine("═══════════════════════════════════════════════════");

                var seen = new HashSet<Mesh>();
                var issues = new List<string>();
                var sorted = new List<MeshRenderer>();
                foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                    if (r != null) sorted.Add(r);
                sorted.Sort((a, b) => string.Compare(a.gameObject.name, b.gameObject.name, System.StringComparison.Ordinal));

                int checkedCount = 0;

                foreach (var r in sorted)
                {
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
                    var mat = r.sharedMaterial;
                    float tiling = mat != null ? mat.GetTextureScale("_BaseMap").x : 1f;
                    checkedCount++;

                    // 按法线分组
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

                        // 该面的世界尺寸
                        float w, h;
                        string axisDesc;
                        switch (kv.Key)
                        {
                            case "X":
                                w = (mxZ - mnZ) * ws.z;
                                h = (mxY - mnY) * ws.y;
                                axisDesc = $"Y-Z面(Z长{w:F2} × Y高{h:F2})";
                                break;
                            case "Y":
                                w = (mxX - mnX) * ws.x;
                                h = (mxZ - mnZ) * ws.z;
                                axisDesc = $"X-Z面(X宽{w:F2} × Z长{h:F2})";
                                break;
                            default:
                                w = (mxX - mnX) * ws.x;
                                h = (mxY - mnY) * ws.y;
                                axisDesc = $"X-Y面(X宽{w:F2} × Y高{h:F2})";
                                break;
                        }

                        // 只检查大面
                        if (w < MinFaceSize || h < MinFaceSize) continue;

                        float su = mxU - mnU;
                        float sv = mxV - mnV;

                        // 实际纹理密度（考虑 Tiling）
                        float densU = (su * tiling) / w;
                        float densV = (sv * tiling) / h;

                        float ratioU = densU / ExpectedDensity;
                        float ratioV = densV / ExpectedDensity;

                        bool badU = ratioU > Tolerance || ratioU < 1f / Tolerance;
                        bool badV = ratioV > Tolerance || ratioV < 1f / Tolerance;

                        if (!badU && !badV) continue;

                        // 记录问题
                        var issue = new StringBuilder();
                        issue.AppendLine($"  ❌ {r.gameObject.name}");
                        issue.AppendLine($"      章节: 第{ch}章");
                        issue.AppendLine($"      Mesh: {AssetDatabase.GetAssetPath(mesh)}");
                        issue.AppendLine($"      材质: {(mat != null ? mat.name : "NULL")}  (Tiling={tiling:F2})");
                        issue.AppendLine($"      模型尺寸: {ws.x:F2} × {ws.y:F2} × {ws.z:F2}");
                        issue.AppendLine($"      面组: {kv.Key}  ({axisDesc})");
                        issue.AppendLine($"      UV 范围: U[{mnU:F3}, {mxU:F3}] V[{mnV:F3}, {mxV:F3}]");
                        issue.AppendLine($"      UV 跨度: {su:F3} × {sv:F3}");
                        issue.AppendLine($"      实际密度: U={densU:F3}/米  V={densV:F3}/米  (期望 {ExpectedDensity})");
                        issue.AppendLine($"      偏离倍数: U×{ratioU:F1}  V×{ratioV:F1}");

                        string symptom;
                        if (badU && badV) symptom = "U、V 双向拉伸";
                        else if (badU) symptom = ratioU > 1 ? "U 方向纹理过密（重复太多）" : "U 方向纹理拉伸（被拉长）";
                        else symptom = ratioV > 1 ? "V 方向纹理过密（重复太多）" : "V 方向纹理拉伸（被拉长）";

                        float maxRatio = Mathf.Max(ratioU, ratioV);
                        string severity = maxRatio > 10 ? "P1-严重"
                                        : maxRatio > 5 ? "P1"
                                        : "P2";

                        issue.AppendLine($"      问题表现: {symptom}");
                        issue.AppendLine($"      严重程度: {severity}");

                        issues.Add(issue.ToString());
                    }
                }

                sb.AppendLine($"  检查网格 {checkedCount} 个，发现异常 {issues.Count} 处");
                sb.AppendLine();
                foreach (var i in issues) sb.AppendLine(i);
                grandTotal += issues.Count;
            }

            sb.AppendLine();
            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            sb.AppendLine($"  全部五章合计异常 {grandTotal} 处");
            sb.AppendLine("═══════════════════════════════════════════════════════════════");

            string text = sb.ToString();
            Debug.Log(text);
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\UvStretchFinalAudit.txt",
                              text, new UTF8Encoding(false));
        }
    }
}