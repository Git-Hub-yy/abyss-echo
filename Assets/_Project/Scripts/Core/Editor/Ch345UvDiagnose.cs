using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S70a：三章 UV 异常的**精确诊断**（只读，不改任何东西）。
    ///
    /// 任务要求先检查：
    ///   · 每个网格的顶点、三角形、法线、UV
    ///   · 实际尺寸和 U/V 方向
    ///   · 使用的材质及 Tiling
    ///   · 共享 Mesh 是否被其他章节引用
    ///
    /// ══════════════════════ 判定逻辑 ══════════════════════
    ///
    /// 灰盒网格是 24 顶点的长方体（6 面 × 4 顶点）。
    /// 正确做法：按法线方向分组，每个面组应该用自己的两个"展开轴"：
    ///     ±X 面（左右侧面）→ U 用 Z 长度，V 用 Y 高度
    ///     ±Z 面（前后大面）→ U 用 X 长度，V 用 Y 高度
    ///     ±Y 面（顶/底面）  → U 用 X 长度，V 用 Z 长度
    ///
    /// 判定"错误"的标准：
    ///     该面组的 UV 跨度 与 期望跨度 偏离超过 2 倍
    ///
    /// 同时检查：
    ///   · 网格被哪些场景的哪些对象引用（防止跨章连带修改）
    ///   · 材质的 Tiling（因为最终纹理密度 = UV × Tiling）
    /// </summary>
    public static class Ch345UvDiagnose
    {
        private static readonly string[] Scenes =
        {
            "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity",
            "Assets/_Project/Scenes/Level_Chapter2_Graybox.unity",
            "Assets/_Project/Scenes/Level_Chapter3_Graybox.unity",
            "Assets/_Project/Scenes/Level_Chapter4_Graybox.unity",
            "Assets/_Project/Scenes/Level_Chapter5_Graybox.unity",
        };

        /// <summary>灰盒约定：UV = 世界尺寸 × 0.5</summary>
        private const float UvPerMeter = 0.5f;

        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════════════════════");
            sb.AppendLine("  第三/四/五章 UV 异常精确诊断（只读）");
            sb.AppendLine("═══════════════════════════════════════════════════════");

            // ══ 第一步：建立 mesh → 引用对象 的全局索引（跨全部5章）══
            var meshRefs = new Dictionary<Mesh, List<string>>();

            foreach (var scenePath in Scenes)
            {
                if (!File.Exists(scenePath)) continue;
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                string sceneName = Path.GetFileNameWithoutExtension(scenePath);

                foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
                {
                    if (mf == null || mf.sharedMesh == null) continue;
                    var mesh = mf.sharedMesh;
                    if (!meshRefs.ContainsKey(mesh)) meshRefs[mesh] = new List<string>();
                    meshRefs[mesh].Add($"{sceneName}/{mf.gameObject.name}");
                }
            }

            sb.AppendLine($"跨 5 章共索引 {meshRefs.Count} 个独立网格");

            // ══ 第二步：逐章诊断 ══
            int[] chapters = { 3, 4, 5 };
            var totalToFix = new List<Mesh>();
            var totalSkip = new List<string>();

            foreach (int ch in chapters)
            {
                string scenePath = $"Assets/_Project/Scenes/Level_Chapter{ch}_Graybox.unity";
                if (!File.Exists(scenePath)) continue;

                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                Physics.SyncTransforms();

                sb.AppendLine();
                sb.AppendLine($"══════════ 第{ch}章 ══════════");

                var seen = new HashSet<Mesh>();
                int okCount = 0, badCount = 0, skipCount = 0;

                var ordered = new List<MeshRenderer>();
                foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                    if (r != null) ordered.Add(r);
                ordered.Sort((a, b) => string.Compare(a.gameObject.name, b.gameObject.name,
                                                      System.StringComparison.Ordinal));

                foreach (var r in ordered)
                {
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) continue;
                    var mesh = mf.sharedMesh;
                    if (!mesh.name.StartsWith("MESH_")) continue;
                    if (!seen.Add(mesh)) continue;

                    var uv = mesh.uv;
                    var norms = mesh.normals;
                    var verts = mesh.vertices;
                    var tris = mesh.triangles;

                    var ws = r.bounds.size;
                    var mat = r.sharedMaterial;
                    float tiling = mat != null ? mat.GetTextureScale("_BaseMap").x : 1f;

                    if (uv == null || uv.Length == 0 || norms == null)
                    {
                        sb.AppendLine($"  [SKIP] {mesh.name}: 无 UV 或法线");
                        totalSkip.Add($"{ch}/{mesh.name}: 无UV或法线");
                        skipCount++;
                        continue;
                    }

                    // 按法线方向分组
                    var groups = new Dictionary<string, List<int>>
                    {
                        { "X", new List<int>() }, { "Y", new List<int>() }, { "Z", new List<int>() }
                    };
                    for (int i = 0; i < norms.Length; i++)
                    {
                        Vector3 n = norms[i];
                        string k = Mathf.Abs(n.x) > 0.5f ? "X" : (Mathf.Abs(n.y) > 0.5f ? "Y" : "Z");
                        groups[k].Add(i);
                    }

                    // 期望 UV 跨度
                    float expUX = ws.z * UvPerMeter, expVX = ws.y * UvPerMeter;
                    float expUZ = ws.x * UvPerMeter, expVZ = ws.y * UvPerMeter;
                    float expUY = ws.x * UvPerMeter, expVY = ws.z * UvPerMeter;

                    bool needFix = false;
                    var faceInfo = new StringBuilder();

                    foreach (var g in groups)
                    {
                        var idx = g.Value;
                        if (idx.Count == 0) { faceInfo.Append($"      {g.Key}面: 无顶点\n"); continue; }

                        float mnU = 999, mxU = -999, mnV = 999, mxV = -999;
                        foreach (var i in idx)
                        {
                            mnU = Mathf.Min(mnU, uv[i].x); mxU = Mathf.Max(mxU, uv[i].x);
                            mnV = Mathf.Min(mnV, uv[i].y); mxV = Mathf.Max(mxV, uv[i].y);
                        }
                        float curU = mxU - mnU, curV = mxV - mnV;

                        float eU, eV;
                        switch (g.Key)
                        {
                            case "X": eU = expUX; eV = expVX; break;
                            case "Z": eU = expUZ; eV = expVZ; break;
                            default: eU = expUY; eV = expVY; break;
                        }
                        if (eU < 0.01f) eU = 0.01f;
                        if (eV < 0.01f) eV = 0.01f;

                        float ratioU = curU / eU;
                        float ratioV = curV / eV;

                        bool badU = ratioU < 0.5f || ratioU > 2.0f;
                        bool badV = ratioV < 0.5f || ratioV > 2.0f;
                        if (badU || badV) needFix = true;

                        faceInfo.Append($"      {g.Key}面: 现UV {curU:F3}x{curV:F3}  期望 {eU:F3}x{eV:F3}  " +
                                        $"比例 {ratioU:F2}/{ratioV:F2}  {(badU || badV ? "❌" : "✓")}\n");
                    }

                    if (needFix)
                    {
                        badCount++;
                        var refs = meshRefs.ContainsKey(mesh) ? meshRefs[mesh] : new List<string>();
                        var otherScenes = new HashSet<string>();
                        foreach (var rf in refs)
                        {
                            int slash = rf.IndexOf('/');
                            if (slash > 0) otherScenes.Add(rf.Substring(0, slash));
                        }

                        sb.AppendLine($"  [FIX] {mesh.name}");
                        sb.AppendLine($"        尺寸 {ws.x:F2} × {ws.y:F2} × {ws.z:F2}   顶点={verts.Length} 三角形={tris.Length / 3}");
                        sb.AppendLine($"        材质 {mat?.name ?? "NULL"}  Tiling={tiling:F2}");
                        sb.AppendLine($"        引用 {refs.Count} 处，涉及场景: {string.Join(", ", otherScenes)}");
                        sb.Append(faceInfo.ToString());

                        if (otherScenes.Count > 1)
                            sb.AppendLine($"        ⚠️ 跨章节共享（{otherScenes.Count} 个场景）");

                        totalToFix.Add(mesh);
                    }
                    else okCount++;
                }

                sb.AppendLine($"  小计: 正常 {okCount}，需修复 {badCount}，跳过 {skipCount}");
            }

            sb.AppendLine();
            sb.AppendLine("═══════════════════════════════════════════════════════");
            sb.AppendLine($"  合计需修复 {totalToFix.Count} 个网格");
            sb.AppendLine($"  合计跳过 {totalSkip.Count} 个");
            sb.AppendLine("═══════════════════════════════════════════════════════");

            string text = sb.ToString();
            Debug.Log(text);
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Ch345UvDiagnose.txt",
                              text, new UTF8Encoding(false));
        }
    }
}