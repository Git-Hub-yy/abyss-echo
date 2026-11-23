using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S75c：修复第五章**墙体 X 面 / Y 面的 UV 方向错误**。
    ///
    /// ══════════════════════ 问题本质（已逐顶点验证）══════════════════════
    ///
    ///   灰盒生成器的 UV 规则：
    ///       U = 局部X × 世界X尺寸 × 0.5
    ///       V = 局部Y × 世界Y尺寸 × 0.5
    ///
    ///   这个规则对地板/天花板是对的（它们垂直于 Y，展开面就是 X-Z）。
    ///
    ///   但对**墙**是错的：
    ///       墙 East_H1_Full = 0.3(厚X) × 5.0(高Y) × 8.0(长Z)
    ///       ±X 面（玩家正对着看的墙）：
    ///           实际应该  U = Z长度 × 0.5 = 4.00,  V = Y高度 × 0.5 = 2.50
    ///           实际生成  U = X厚度 × 0.5 = 0.15,  V = Y高度 × 0.5 = 2.50
    ///       → U 方向被压扁 26.7 倍
    ///
    ///   实测数据（Reports/Ch5WallFaceUv.txt）：
    ///       ±X 面  U方向每米 0.019   （应为 0.5）  ❌
    ///       ±Y 面  U方向每米 13.333  （应为 0.5）  ❌
    ///       ±Z 面  U方向每米 0.000   （薄面，正常）
    ///
    ///   对比地板 Floor_H1：所有面 U/V 都是 0.500 ✅
    ///   对比天花板 Ceiling_H2：所有面 U/V 都是 0.500 ✅
    ///
    /// ══════════════════════ 为什么金属纹理看不出来 ══════════════════════
    ///
    ///   金属板贴图是细密规律的，压扁后看起来只是"纹理变密"，
    ///   而有机贴图（膜、血肉、黏液）有清晰结构，
    ///   压扁 26 倍后表现为**横向拉长的条纹** —— 正是玩家反馈的现象。
    ///
    /// ══════════════════════ 修复方式 ══════════════════════
    ///
    ///   对每个 MESH_ 网格，按**顶点法线**分组：
    ///     ±X 面：U = (局部Z归一化) × 世界Z尺寸 × 0.5
    ///            V = (局部Y归一化) × 世界Y尺寸 × 0.5
    ///     ±Y 面：U = (局部X归一化) × 世界X尺寸 × 0.5
    ///            V = (局部Z归一化) × 世界Z尺寸 × 0.5
    ///     ±Z 面：U = (局部X归一化) × 世界X尺寸 × 0.5
    ///            V = (局部Y归一化) × 世界Y尺寸 × 0.5
    ///
    ///   **只改 UV，不改顶点/法线/三角形/碰撞体。**
    ///   **只处理薄板墙（一个轴 < 1 米），地板天花板不动。**
    ///
    /// ══════════════════════ 范围限定 ══════════════════════
    ///
    ///   只改第五章场景使用的网格。
    ///   修改前检查该网格是否被其他章节引用 —— 如果被引用则跳过并报告。
    /// </summary>
    public static class Ch5WallUvFix
    {
        private const string CH5_SCENE = "Assets/_Project/Scenes/Level_Chapter5_Graybox.unity";

        private static readonly string[] OtherScenes =
        {
            "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity",
            "Assets/_Project/Scenes/Level_Chapter2_Graybox.unity",
            "Assets/_Project/Scenes/Level_Chapter3_Graybox.unity",
            "Assets/_Project/Scenes/Level_Chapter4_Graybox.unity",
        };

        private const float UV_PER_METER = 0.5f;

        [MenuItem("ABYSS ECHO/修复 v10.2/第五章墙体UV修复")]
        public static void Build() => Run(dryRun: false);

        [MenuItem("ABYSS ECHO/修复 v10.2/第五章墙体UV预演（只读）")]
        public static void DryRun() => Run(dryRun: true);

        public static void Run(bool dryRun)
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════════════════════════");
            sb.AppendLine(dryRun ? "  第五章墙体 UV 修复 —— 预演（只读）"
                                 : "  第五章墙体 UV 修复 —— 执行");
            sb.AppendLine("═══════════════════════════════════════════════════════════");

            // ══════════ 1. 建立跨章节 Mesh 引用索引 ══════════
            sb.AppendLine();
            sb.AppendLine("──── 建立跨章节 Mesh 引用索引 ────");

            var otherMeshRefs = new HashSet<Mesh>();
            foreach (var sp in OtherScenes)
            {
                if (!File.Exists(sp)) continue;
                EditorSceneManager.OpenScene(sp, OpenSceneMode.Single);
                foreach (var r in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
                {
                    if (r != null && r.sharedMesh != null) otherMeshRefs.Add(r.sharedMesh);
                }
            }
            sb.AppendLine($"  其他章节引用网格 = {otherMeshRefs.Count} 个");

            // ══════════ 2. 打开第五章，逐个处理 ══════════
            var scene = EditorSceneManager.OpenScene(CH5_SCENE, OpenSceneMode.Single);
            Physics.SyncTransforms();

            var seen = new HashSet<Mesh>();
            var sorted = new List<MeshRenderer>();
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                if (r != null) sorted.Add(r);
            sorted.Sort((a, b) => string.Compare(a.gameObject.name, b.gameObject.name, System.StringComparison.Ordinal));

            int fixedCount = 0, skippedShared = 0, skippedAlready = 0, skippedNotWall = 0, skippedNotOrganic = 0;
            var fixedList = new List<string>();
            var skippedList = new List<string>();

            sb.AppendLine();
            sb.AppendLine("──── 逐个网格检查 ────");

            foreach (var r in sorted)
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var mesh = mf.sharedMesh;
                if (!mesh.name.StartsWith("MESH_")) continue;
                if (!seen.Add(mesh)) continue;

                var ws = r.bounds.size;

                // 只处理薄板墙：恰好一个轴 < 1 米（其他两个 >= 1 米）
                int thinAxes = 0;
                if (ws.x < 1.0f) thinAxes++;
                if (ws.y < 1.0f) thinAxes++;
                if (ws.z < 1.0f) thinAxes++;

                if (thinAxes != 1) { skippedNotWall++; continue; }

                // ⚠️ 本次只修复**有机材质**墙面
                //
                //   金属墙面（M_Mach_WornMetal 等）的 UV 也有同样的技术缺陷，
                //   但金属贴图是细密规律纹理，压扁后在视觉上看不出问题。
                //
                //   任务要求「最小必要修改」「不要为了追求修改数量而批量重做 UV」，
                //   因此本批只处理有机材质 —— 那里才有玩家可见的条纹/拉伸。
                //
                //   金属墙是否修复，留待用户看完有机修复效果后再决定。
                string matName = r.sharedMaterial != null ? r.sharedMaterial.name : "";
                if (!matName.StartsWith("M_Org_"))
                {
                    skippedNotOrganic++;
                    continue;
                }

                // 跨章节共享 → 跳过
                if (otherMeshRefs.Contains(mesh))
                {
                    skippedShared++;
                    skippedList.Add($"  ⚠️ {mesh.name} 被其他章节引用，跳过");
                    continue;
                }

                var uv = mesh.uv;
                var verts = mesh.vertices;
                var norms = mesh.normals;
                if (uv == null || verts == null || norms == null) continue;

                // 按法线分组
                var groups = new Dictionary<string, List<int>>();
                for (int i = 0; i < norms.Length; i++)
                {
                    Vector3 n = norms[i];
                    string k = Mathf.Abs(n.x) > 0.5f ? "X" : (Mathf.Abs(n.y) > 0.5f ? "Y" : "Z");
                    if (!groups.ContainsKey(k)) groups[k] = new List<int>();
                    groups[k].Add(i);
                }

                // 检查是否需要修复（只看 ±X 面和 ±Y 面）
                bool needFix = false;
                foreach (var kv in groups)
                {
                    if (kv.Key != "X" && kv.Key != "Y") continue;

                    var idx = kv.Value;
                    float mnU = 999, mxU = -999, mnV = 999, mxV = -999;
                    foreach (var i in idx)
                    {
                        mnU = Mathf.Min(mnU, uv[i].x); mxU = Mathf.Max(mxU, uv[i].x);
                        mnV = Mathf.Min(mnV, uv[i].y); mxV = Mathf.Max(mxV, uv[i].y);
                    }
                    float su = mxU - mnU;
                    float sv = mxV - mnV;

                    // 该面覆盖的世界尺寸
                    float wU, wV;
                    if (kv.Key == "X")
                    {
                        // X 面沿 Z(长度) 和 Y(高度)
                        float mnZ = 999, mxZ = -999, mnY = 999, mxY = -999;
                        foreach (var i in idx)
                        {
                            mnZ = Mathf.Min(mnZ, verts[i].z); mxZ = Mathf.Max(mxZ, verts[i].z);
                            mnY = Mathf.Min(mnY, verts[i].y); mxY = Mathf.Max(mxY, verts[i].y);
                        }
                        wU = (mxZ - mnZ) * ws.z;
                        wV = (mxY - mnY) * ws.y;
                    }
                    else
                    {
                        // Y 面沿 X 和 Z
                        float mnX = 999, mxX = -999, mnZ = 999, mxZ = -999;
                        foreach (var i in idx)
                        {
                            mnX = Mathf.Min(mnX, verts[i].x); mxX = Mathf.Max(mxX, verts[i].x);
                            mnZ = Mathf.Min(mnZ, verts[i].z); mxZ = Mathf.Max(mxZ, verts[i].z);
                        }
                        wU = (mxX - mnX) * ws.x;
                        wV = (mxZ - mnZ) * ws.z;
                    }

                    if (wU < 0.3f || wV < 0.3f) continue;   // 薄面不处理

                    float densU = su / wU;
                    float densV = sv / wV;

                    // 偏离 0.5 超过 2 倍 → 需要修
                    if (densU < 0.25f || densU > 1.0f) { needFix = true; }
                    if (densV < 0.25f || densV > 1.0f) { needFix = true; }
                }

                if (!needFix) { skippedAlready++; continue; }

                // ══════ 执行修复 ══════
                if (!dryRun)
                {
                    var newUv = (Vector2[])uv.Clone();

                    foreach (var kv in groups)
                    {
                        var idx = kv.Value;
                        if (idx.Count == 0) continue;

                        // 算该面组的局部范围
                        float mnA = 999, mxA = -999, mnB = 999, mxB = -999;
                        foreach (var i in idx)
                        {
                            if (kv.Key == "X") { mnA = Mathf.Min(mnA, verts[i].z); mxA = Mathf.Max(mxA, verts[i].z); }
                            else if (kv.Key == "Y") { mnA = Mathf.Min(mnA, verts[i].x); mxA = Mathf.Max(mxA, verts[i].x); }
                            else { mnA = Mathf.Min(mnA, verts[i].x); mxA = Mathf.Max(mxA, verts[i].x); }

                            if (kv.Key == "X") { mnB = Mathf.Min(mnB, verts[i].y); mxB = Mathf.Max(mxB, verts[i].y); }
                            else if (kv.Key == "Y") { mnB = Mathf.Min(mnB, verts[i].z); mxB = Mathf.Max(mxB, verts[i].z); }
                            else { mnB = Mathf.Min(mnB, verts[i].y); mxB = Mathf.Max(mxB, verts[i].y); }
                        }
                        float spanA = Mathf.Max(0.001f, mxA - mnA);
                        float spanB = Mathf.Max(0.001f, mxB - mnB);

                        // 该面的世界尺寸
                        float worldU, worldV;
                        if (kv.Key == "X") { worldU = spanA * ws.z; worldV = spanB * ws.y; }
                        else if (kv.Key == "Y") { worldU = spanA * ws.x; worldV = spanB * ws.z; }
                        else { worldU = spanA * ws.x; worldV = spanB * ws.y; }

                        foreach (var i in idx)
                        {
                            float nA, nB;
                            if (kv.Key == "X") { nA = (verts[i].z - mnA) / spanA; nB = (verts[i].y - mnB) / spanB; }
                            else if (kv.Key == "Y") { nA = (verts[i].x - mnA) / spanA; nB = (verts[i].z - mnB) / spanB; }
                            else { nA = (verts[i].x - mnA) / spanA; nB = (verts[i].y - mnB) / spanB; }

                            newUv[i] = new Vector2(nA * worldU * UV_PER_METER,
                                                   nB * worldV * UV_PER_METER);
                        }
                    }

                    mesh.uv = newUv;
                    EditorUtility.SetDirty(mesh);
                    fixedCount++;
                    fixedList.Add($"  ✅ {mesh.name,-32} {ws.x:F1}×{ws.y:F1}×{ws.z:F1}  " +
                                  $"材质={r.sharedMaterial?.name ?? "?"}");
                }
                else
                {
                    fixedCount++;
                    fixedList.Add($"  [预演] {mesh.name,-32} {ws.x:F1}×{ws.y:F1}×{ws.z:F1}  " +
                                  $"材质={r.sharedMaterial?.name ?? "?"}");
                }
            }

            // ══════ 3. 输出 ══════
            sb.AppendLine();
            sb.AppendLine($"  待修复网格 = {fixedCount}");
            sb.AppendLine($"  已正确跳过 = {skippedAlready}");
            sb.AppendLine($"  跨章节共享跳过 = {skippedShared}");
            sb.AppendLine($"  非薄板跳过 = {skippedNotWall}");
            sb.AppendLine($"  非有机材质跳过 = {skippedNotOrganic}  （金属墙，视觉无明显问题，本次不处理）");

            if (fixedList.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("──── 修复清单 ────");
                foreach (var s in fixedList) sb.AppendLine(s);
            }
            if (skippedList.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("──── 跳过清单 ────");
                foreach (var s in skippedList) sb.AppendLine(s);
            }

            // ══════ 4. 保存 ══════
            if (!dryRun && fixedCount > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                sb.AppendLine();
                sb.AppendLine($"  ✅ 已保存 {fixedCount} 个网格");
            }
            else if (dryRun)
            {
                sb.AppendLine();
                sb.AppendLine("  [预演] 未保存任何文件");
            }

            string text = sb.ToString();
            Debug.Log(text);
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Ch5WallUvFix_Result.txt",
                              text, new UTF8Encoding(false));
        }
    }
}