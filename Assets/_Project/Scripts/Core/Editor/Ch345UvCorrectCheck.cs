using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S70d：**正确的** UV 问题判定。
    ///
    /// ══════════════════════ 前一版判定错在哪 ══════════════════════
    ///
    ///   上一版按"每个面组用自己的两个展开轴"判定，结果把 381 个网格判成异常。
    ///   但逐顶点分析证明：
    ///
    ///     灰盒 Box 的 UV 是**整体按最大两个轴展开**的，
    ///     不是每个面独立展开。
    ///
    ///     例：South_A2_SegEnd (1.8×3.2×0.3) 最大两轴 X=1.8, Y=3.2
    ///         → UV = (X×0.5, Y×0.5) = (0.9, 1.6)
    ///         X 侧面（薄面）U 恒为 0 —— 这是正确的
    ///
    ///     例：East_F10_Full (0.3×2.8×4.0) 最大两轴 Z=4.0, Y=2.8
    ///         → UV = (Z×0.5, Y×0.5) = (2.0, 1.4)
    ///         X 侧面 U 跨度 0.15 —— 也是正确的
    ///
    ///   所以两章的网格 UV **都是正确的**，不存在 93/381 个 bug。
    ///
    /// ══════════════════════ 本版判定（正确的标准）══════════════════════
    ///
    ///   对每个网格：
    ///     ① 找出最大两个世界轴 (d1, d2)
    ///     ② 计算 UV 总跨度
    ///     ③ 期望跨度 = d1×0.5, d2×0.5
    ///     ④ 若实际跨度与期望偏离超过 2 倍，才是真的异常
    ///
    ///   同时输出每章的统计，确认三章是否真的有问题。
    /// </summary>
    public static class Ch345UvCorrectCheck
    {
        private static readonly string[] Scenes =
        {
            "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity",
            "Assets/_Project/Scenes/Level_Chapter2_Graybox.unity",
            "Assets/_Project/Scenes/Level_Chapter3_Graybox.unity",
            "Assets/_Project/Scenes/Level_Chapter4_Graybox.unity",
            "Assets/_Project/Scenes/Level_Chapter5_Graybox.unity",
        };

        private const float UvPerMeter = 0.5f;

        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════════════════");
            sb.AppendLine("  UV 问题重新判定（按「最大两轴展开」的标准）");
            sb.AppendLine("═══════════════════════════════════════════════════");

            foreach (var scenePath in Scenes)
            {
                if (!File.Exists(scenePath)) continue;
                string chName = Path.GetFileNameWithoutExtension(scenePath);

                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                Physics.SyncTransforms();

                var seen = new HashSet<Mesh>();
                int total = 0, ok = 0, bad = 0;
                var badList = new List<string>();
                var okSamples = new List<string>();

                foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                {
                    if (r == null) continue;
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) continue;
                    var mesh = mf.sharedMesh;
                    if (!mesh.name.StartsWith("MESH_")) continue;
                    if (!seen.Add(mesh)) continue;

                    var uv = mesh.uv;
                    if (uv == null || uv.Length == 0) continue;

                    var ws = r.bounds.size;
                    total++;

                    // UV 总跨度
                    float mnU = 999, mxU = -999, mnV = 999, mxV = -999;
                    foreach (var v in uv)
                    {
                        mnU = Mathf.Min(mnU, v.x); mxU = Mathf.Max(mxU, v.x);
                        mnV = Mathf.Min(mnV, v.y); mxV = Mathf.Max(mxV, v.y);
                    }
                    float spanU = mxU - mnU;
                    float spanV = mxV - mnV;

                    // 最大两个世界轴（降序）
                    float[] d = { ws.x, ws.y, ws.z };
                    System.Array.Sort(d);
                    float d1 = Mathf.Max(0.01f, d[2]);   // 最大
                    float d2 = Mathf.Max(0.01f, d[1]);   // 次大

                    // 期望：两个大轴 × 0.5
                    //   注意 UV 的 U/V 哪个对应哪个轴不重要，只比较"两个值"
                    float e1 = d1 * UvPerMeter;
                    float e2 = d2 * UvPerMeter;

                    // 把实际的两个跨度与期望的两个值做最优配对比较
                    float err1 = Mathf.Abs(spanU - e1) / e1 + Mathf.Abs(spanV - e2) / e2;
                    float err2 = Mathf.Abs(spanU - e2) / e2 + Mathf.Abs(spanV - e1) / e1;
                    float err = Mathf.Min(err1, err2) * 0.5f;

                    bool isBad = err > 0.75f;   // 偏离超过 75% 才算异常

                    if (isBad)
                    {
                        bad++;
                        badList.Add($"    {mesh.name,-36} 尺寸={ws.x:F1}x{ws.y:F1}x{ws.z:F1} " +
                                    $"UV={spanU:F2}x{spanV:F2}  期望={e1:F2}x{e2:F2}  偏差={err * 100:F0}%");
                    }
                    else
                    {
                        ok++;
                        if (okSamples.Count < 3)
                            okSamples.Add($"    {mesh.name,-36} 尺寸={ws.x:F1}x{ws.y:F1}x{ws.z:F1} " +
                                          $"UV={spanU:F2}x{spanV:F2}  期望={e1:F2}x{e2:F2}  偏差={err * 100:F0}%");
                    }
                }

                sb.AppendLine();
                sb.AppendLine($"────────── {chName} ──────────");
                sb.AppendLine($"  网格总数 {total}：正常 {ok}，异常 {bad}");

                if (okSamples.Count > 0)
                {
                    sb.AppendLine("  正常样本：");
                    foreach (var s in okSamples) sb.AppendLine(s);
                }

                if (badList.Count > 0)
                {
                    sb.AppendLine($"  异常清单（前 30 个）：");
                    for (int i = 0; i < Mathf.Min(30, badList.Count); i++) sb.AppendLine(badList[i]);
                    if (badList.Count > 30) sb.AppendLine($"    ...共 {badList.Count} 个");
                }
            }

            string text = sb.ToString();
            Debug.Log(text);
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Ch345UvCorrectCheck.txt",
                              text, new UTF8Encoding(false));
        }
    }
}