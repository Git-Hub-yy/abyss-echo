using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S73c：**UV 密度的唯一可靠判定方式** —— 用三角形面积计算。
    ///
    /// ══════════════════════ 之前四次全错的原因 ══════════════════════
    ///
    ///   我一直在用"顶点局部坐标范围 × 世界尺寸"推断"面的大小"。
    ///   这对薄板（0.3 米厚）完全不成立：
    ///
    ///     East_F10_Full (0.3 × 2.8 × 4.0)
    ///     X 面组的 4 个顶点，局部 X 都是 0.5，
    ///     局部 Y 范围 1.0、局部 Z 范围 1.0
    ///     → 我算出"面尺寸 4.0 × 2.8"
    ///     → 但 X 面是厚 0.3、宽 4.0、高 2.8 的**薄片侧面**，
    ///       它真实的可视面积只有 4.0 × 2.8（正面）
    ///       而 UV 在该面上只有 U 方向 0.15（因为 UV 是按"最大两轴"整体展开的）
    ///
    ///   所以"面尺寸"这个中间量，我一直算错。
    ///
    /// ══════════════════════ 正确做法：直接算面积比 ══════════════════════
    ///
    ///   对每个三角形：
    ///     · 世界空间面积 A_world（用顶点位置的叉积）
    ///     · UV 空间面积 A_uv（用 UV 的叉积）
    ///
    ///   纹理密度 = A_uv / A_world  （每平方米对应多少 UV 面积）
    ///
    ///   若 UV 正确展开（每 2 米一个循环），则：
    ///     A_uv = (边长1/2) × (边长2/2) = A_world / 4
    ///   所以 **期望值 A_uv / A_world = 0.25**
    ///
    ///   实际密度 × Tiling² 才是最终纹理密度。
    ///
    ///   这个方法的优点：
    ///     · 不依赖"面组"假设
    ///     · 不依赖顶点顺序
    ///     · 对薄板同样成立
    ///     · 面积是标量，没有方向混淆
    ///
    /// ══════════════════════ 判定 ══════════════════════
    ///
    ///   计算每个网格所有三角形的密度中位数。
    ///   中位数偏离期望 0.25 超过 4 倍（或低于 1/4）→ 判定异常。
    ///
    ///   用中位数而非平均，避免个别退化三角形干扰。
    ///
    /// 只读。
    /// </summary>
    public static class UvDensityByArea
    {
        private static readonly (int ch, string path)[] Scenes =
        {
            (1, "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity"),
            (2, "Assets/_Project/Scenes/Level_Chapter2_Graybox.unity"),
            (3, "Assets/_Project/Scenes/Level_Chapter3_Graybox.unity"),
            (4, "Assets/_Project/Scenes/Level_Chapter4_Graybox.unity"),
            (5, "Assets/_Project/Scenes/Level_Chapter5_Graybox.unity"),
        };

        /// <summary>期望 A_uv/A_world（UV 每 2 米一个循环时）</summary>
        private const float ExpectedRatio = 0.25f;

        /// <summary>容忍倍数：中位数偏离超过这个倍数才判异常</summary>
        private const float Tolerance = 4.0f;

        [MenuItem("ABYSS ECHO/审计 v10.0/UV 密度（面积法）")]
        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            sb.AppendLine("  UV 密度审计 —— 面积法（唯一可靠方法）");
            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            sb.AppendLine($"  期望 UV面积/世界面积 = {ExpectedRatio}");
            sb.AppendLine($"  判定：中位数偏离 > {Tolerance} 倍 或 < 1/{Tolerance}");
            sb.AppendLine();

            int grand = 0;

            foreach (var (ch, path) in Scenes)
            {
                if (!File.Exists(path)) continue;
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                Physics.SyncTransforms();

                sb.AppendLine();
                sb.AppendLine($"────────── 第{ch}章 ──────────");

                var seen = new HashSet<Mesh>();
                var rows = new List<(string name, string meshPath, string matName, float tiling,
                                    Vector3 size, float ratio, float ratioWithTiling)>();

                var sorted = new List<MeshRenderer>();
                foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                    if (r != null) sorted.Add(r);
                sorted.Sort((a, b) => string.Compare(a.gameObject.name, b.gameObject.name, System.StringComparison.Ordinal));

                foreach (var r in sorted)
                {
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) continue;
                    var mesh = mf.sharedMesh;
                    if (!mesh.name.StartsWith("MESH_")) continue;
                    if (!seen.Add(mesh)) continue;

                    var uv = mesh.uv;
                    var verts = mesh.vertices;
                    var tris = mesh.triangles;
                    if (uv == null || verts == null || tris == null || tris.Length < 3) continue;

                    // 逐个三角形算面积比
                    var ratios = new List<float>();

                    for (int t = 0; t + 2 < tris.Length; t += 3)
                    {
                        int a = tris[t], b = tris[t + 1], c = tris[t + 2];

                        // 世界面积
                        Vector3 e1 = verts[b] - verts[a];
                        Vector3 e2 = verts[c] - verts[a];
                        float aWorld = Vector3.Cross(e1, e2).magnitude * 0.5f;

                        // UV 面积
                        Vector2 d1 = uv[b] - uv[a];
                        Vector2 d2 = uv[c] - uv[a];
                        float aUv = Mathf.Abs(d1.x * d2.y - d1.y * d2.x) * 0.5f;

                        if (aWorld < 1e-6f) continue;    // 退化三角形

                        ratios.Add(aUv / aWorld);
                    }

                    if (ratios.Count == 0) continue;

                    ratios.Sort();
                    float median = ratios[ratios.Count / 2];

                    float tiling = r.sharedMaterial != null
                        ? r.sharedMaterial.GetTextureScale("_BaseMap").x : 1f;

                    rows.Add((r.gameObject.name,
                              AssetDatabase.GetAssetPath(mesh),
                              r.sharedMaterial != null ? r.sharedMaterial.name : "NULL",
                              tiling,
                              r.bounds.size,
                              median,
                              median * tiling * tiling));
                }

                // 统计
                sb.AppendLine($"  检查网格 {rows.Count} 个");
                sb.AppendLine();

                // 分档统计
                int normal = 0, tooDense = 0, tooSparse = 0;
                var issues = new List<string>();

                foreach (var row in rows)
                {
                    float ratio = row.ratio;
                    float factor = ratio / ExpectedRatio;

                    bool bad = factor > Tolerance || factor < 1f / Tolerance;
                    if (!bad) { normal++; continue; }

                    if (factor > 1) tooDense++; else tooSparse++;

                    string symptom = factor > 1
                        ? $"纹理过密 ×{factor:F1}（每平方米 ~{row.ratioWithTiling:F2} UV面积）"
                        : $"纹理过疏/拉伸 ×{factor:F2}";

                    string sev = factor > 20 || factor < 0.05f ? "P1" : "P2";

                    issues.Add($"    {row.name,-28} 密度×{factor,7:F2}  {symptom}\n" +
                               $"        材质={row.matName} Tiling={row.tiling:F2}  尺寸={row.size.x:F1}×{row.size.y:F1}×{row.size.z:F1}  [{sev}]\n" +
                               $"        mesh={row.meshPath}");
                }

                sb.AppendLine($"  正常 {normal} 个，过密 {tooDense} 个，过疏 {tooSparse} 个");
                sb.AppendLine();

                if (issues.Count > 0)
                {
                    sb.AppendLine("  异常清单：");
                    foreach (var i in issues) sb.AppendLine(i);
                }

                grand += issues.Count;

                // 密度分布摘要（正常的部分也给个数，便于对比）
                sb.AppendLine();
                sb.AppendLine("  密度分布（前 10 个最密集）：");
                rows.Sort((a, b) => b.ratio.CompareTo(a.ratio));
                for (int i = 0; i < Mathf.Min(10, rows.Count); i++)
                {
                    var r0 = rows[i];
                    sb.AppendLine($"    {r0.name,-28} 密度×{(r0.ratio / ExpectedRatio):F2}  " +
                                  $"材质={r0.matName} Tiling={r0.tiling:F2}");
                }
            }

            sb.AppendLine();
            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            sb.AppendLine($"  全程异常合计 {grand} 处");
            sb.AppendLine("═══════════════════════════════════════════════════════════════");

            string text = sb.ToString();
            Debug.Log(text);
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\UvDensityByArea.txt",
                              text, new UTF8Encoding(false));
        }
    }
}