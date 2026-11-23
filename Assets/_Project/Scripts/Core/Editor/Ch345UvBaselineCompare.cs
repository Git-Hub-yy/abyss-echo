using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S70g：**最终定论** —— 用第一章（视觉已验证正常）作为基准，
    /// 判断三章的网格 UV 是否存在**同类型同比例**的差异。
    ///
    /// ══════════════════════ 为什么前三次都不可靠 ══════════════════════
    ///
    ///   一：按"每面独立两轴" → 381 个，错（Box 是整体两轴）
    ///   二：按"整网格总跨度"   → 6 个，错（总跨度含侧面）
    ///   三：按"每面 U/V 比例"  → 164 个，错（窄边本应压扁）
    ///
    ///   根本问题：这些都是**猜测的判定标准**，
    ///   而灰盒生成器的实际规则没人写下来。
    ///
    /// ══════════════════════ 正确做法：用已验收的章节做基准 ══════════════════════
    ///
    ///   第一章的视觉已通过多轮截图验证，纹理正常、无拉伸。
    ///   所以：
    ///     ① 从第一章采集所有 MESH_ 网格的「面组 UV 模式」
    ///     ② 从第三/四/五章采集同样的模式
    ///     ③ 对**同尺寸比例**的网格，比较两边的 UV 是否一致
    ///
    ///   如果三章的 UV 模式与第一章**一致**，说明没有 bug（之前报的是误报）。
    ///   如果不一致，说明确实有问题，并给出具体差异。
    ///
    /// ══════════════════════ 模式的定义 ══════════════════════
    ///
    ///   对每个网格，记录每个面组的：
    ///     U跨度 / 该面的世界宽度   (归一化)
    ///     V跨度 / 该面的世界高度   (归一化)
    ///
    ///   然后按 (面组, 舍入后的值) 统计分布。
    ///   若第一章和三章都集中在相同的值，则一致。
    ///
    /// **只读。**
    /// </summary>
    public static class Ch345UvBaselineCompare
    {
        private static readonly Dictionary<int, string> ChapterScenes = new Dictionary<int, string>
        {
            { 1, "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity" },
            { 2, "Assets/_Project/Scenes/Level_Chapter2_Graybox.unity" },
            { 3, "Assets/_Project/Scenes/Level_Chapter3_Graybox.unity" },
            { 4, "Assets/_Project/Scenes/Level_Chapter4_Graybox.unity" },
            { 5, "Assets/_Project/Scenes/Level_Chapter5_Graybox.unity" },
        };

        private const float UvPerMeter = 0.5f;

        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════════════════════");
            sb.AppendLine("  UV 模式对比（以第一章为基准）");
            sb.AppendLine("═══════════════════════════════════════════════════════");

            // 每章：面组 → (归一化U/0.5, 归一化V/0.5) 的频次
            var chapterModes = new Dictionary<int, Dictionary<string, Dictionary<string, int>>>();

            foreach (var kv in ChapterScenes)
            {
                int ch = kv.Key;
                if (!File.Exists(kv.Value)) continue;

                EditorSceneManager.OpenScene(kv.Value, OpenSceneMode.Single);
                Physics.SyncTransforms();

                var modes = new Dictionary<string, Dictionary<string, int>>();
                var seen = new HashSet<Mesh>();

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

                    var faces = new Dictionary<string, List<int>>();
                    for (int i = 0; i < norms.Length; i++)
                    {
                        Vector3 n = norms[i];
                        string f = Mathf.Abs(n.x) > 0.5f ? "X"
                                 : Mathf.Abs(n.y) > 0.5f ? "Y" : "Z";
                        if (!faces.ContainsKey(f)) faces[f] = new List<int>();
                        faces[f].Add(i);
                    }

                    foreach (var fk in faces)
                    {
                        var idx = fk.Value;
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

                        float geoW, geoH;
                        if (fk.Key == "X") { geoW = (mxZ - mnZ) * ws.z; geoH = (mxY - mnY) * ws.y; }
                        else if (fk.Key == "Y") { geoW = (mxX - mnX) * ws.x; geoH = (mxZ - mnZ) * ws.z; }
                        else { geoW = (mxX - mnX) * ws.x; geoH = (mxY - mnY) * ws.y; }

                        if (geoW < 0.15f || geoH < 0.15f) continue;   // 跳过窄边

                        float su = mxU - mnU;
                        float sv = mxV - mnV;

                        // 归一化：实际 UV/米 ÷ 期望 0.5
                        float nU = su / geoW / UvPerMeter;
                        float nV = sv / geoH / UvPerMeter;

                        string key = $"{fk.Key}: U×{Mathf.Round(nU * 10) / 10:F1} V×{Mathf.Round(nV * 10) / 10:F1}";

                        if (!modes.ContainsKey(fk.Key)) modes[fk.Key] = new Dictionary<string, int>();
                        if (!modes[fk.Key].ContainsKey(key)) modes[fk.Key][key] = 0;
                        modes[fk.Key][key]++;
                    }
                }

                chapterModes[ch] = modes;
            }

            // 输出
            sb.AppendLine();
            sb.AppendLine("说明：U×1.0 V×1.0 表示「UV/米 = 0.5，正确」");
            sb.AppendLine("      U×1.0 V×0.0 表示「V 方向压扁（窄边正常）」");
            sb.AppendLine();

            foreach (var ch in new[] { 1, 2, 3, 4, 5 })
            {
                if (!chapterModes.ContainsKey(ch)) continue;
                sb.AppendLine($"────────── 第{ch}章 ──────────");
                var modes = chapterModes[ch];

                foreach (var fk in new[] { "X", "Y", "Z" })
                {
                    if (!modes.ContainsKey(fk)) continue;
                    var list = new List<KeyValuePair<string, int>>(modes[fk]);
                    list.Sort((a, b) => b.Value.CompareTo(a.Value));

                    sb.AppendLine($"  {fk} 面组（前 5 种模式）：");
                    for (int i = 0; i < Mathf.Min(5, list.Count); i++)
                        sb.AppendLine($"     {list[i].Key, 28}  {list[i].Value} 次");
                }
                sb.AppendLine();
            }

            // 结论
            sb.AppendLine("═══════════════════════════════════════════════════════");
            sb.AppendLine("  结论");
            sb.AppendLine("═══════════════════════════════════════════════════════");

            if (chapterModes.ContainsKey(1))
            {
                var base1 = chapterModes[1];
                foreach (var ch in new[] { 3, 4, 5 })
                {
                    if (!chapterModes.ContainsKey(ch)) continue;
                    var cm = chapterModes[ch];

                    sb.AppendLine($"  第{ch}章 vs 第一章：");
                    foreach (var fk in new[] { "X", "Y", "Z" })
                    {
                        if (!base1.ContainsKey(fk) || !cm.ContainsKey(fk)) continue;

                        var top1 = GetTop(base1[fk]);
                        var topN = GetTop(cm[fk]);

                        string mark = (top1 == topN) ? "✅ 一致" : "⚠️ 不同";
                        sb.AppendLine($"    {fk}面  第一章主模式={top1}  第{ch}章主模式={topN}  {mark}");
                    }
                    sb.AppendLine();
                }
            }

            string text = sb.ToString();
            Debug.Log(text);
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Ch345UvBaselineCompare.txt",
                              text, new UTF8Encoding(false));
        }

        private static string GetTop(Dictionary<string, int> d)
        {
            string best = "无";
            int max = -1;
            foreach (var kv in d)
                if (kv.Value > max) { max = kv.Value; best = kv.Key; }
            return best;
        }
    }
}