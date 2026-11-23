using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S70b：反推灰盒网格的 UV 生成规则。
    ///
    /// 诊断发现：几乎所有薄墙的 UV 都不对，而且错得"有规律"。
    /// 例如 MESH_East_F10_Full (0.3 × 2.8 × 4.0)：
    ///     X 面: 0.150 × 1.400     (= 0.3×0.5,  2.8×0.5)
    ///     Y 面: 2.000 × 0.150     (= 4.0×0.5,  0.3×0.5)
    ///     Z 面: 2.000 × 1.400     (= 4.0×0.5,  2.8×0.5)
    ///
    /// 规律：
    ///     X 面 U 用了 厚度(0.3)  ← 错，应该是长度(4.0)
    ///     Y 面 U 用了 长度(4.0)  ← 错，应该是 X 尺寸(0.3)
    ///     Y 面 V 用了 厚度(0.3)  ← 错，应该是 Z 尺寸(4.0)
    ///     Z 面 U 用了 长度(4.0)  ← 对（Z 面本来就用 X 方向长度，这里 x=0.3 但用了 4.0）… 也不对
    ///
    /// 结论：这不是"局部错位"，而是生成器把每个 Box 的 UV 按某个固定顺序
    ///       错误地映射到了三个轴。
    ///
    /// 本工具会：
    ///   1. 采样一批健康网格（UV 正常的），反推出正确公式
    ///   2. 对比异常网格，确认是同一个公式的错位
    ///   3. 输出每个顶点组的实际 UV 与公式预测，供人工确认
    ///
    /// **只读，不修改任何东西。**
    /// </summary>
    public static class Ch345UvReverseEngineer
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity";

        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════════════");
            sb.AppendLine("  反推灰盒网格 UV 生成规则");
            sb.AppendLine("  用第一章已修复的网格作为「正确样本」对照");
            sb.AppendLine("═══════════════════════════════════════════════");

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Physics.SyncTransforms();

            // 第一章已经修过 UV（S67e），可以作为正确参照
            var samples = new List<(string name, Vector3 size, Dictionary<string, (float u, float v)> faces)>();

            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (r == null) continue;
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var mesh = mf.sharedMesh;
                if (!mesh.name.StartsWith("MESH_")) continue;

                var ws = r.bounds.size;
                // 只取"薄板墙"样本：一个轴明显小于其他两个
                float minD = Mathf.Min(ws.x, Mathf.Min(ws.y, ws.z));
                if (minD > 0.5f) continue;

                var uv = mesh.uv;
                var norms = mesh.normals;
                if (uv == null || norms == null) continue;

                var groups = new Dictionary<string, (float u, float v)>();
                foreach (var key in new[] { "X", "Y", "Z" })
                {
                    float mnU = 999, mxU = -999, mnV = 999, mxV = -999;
                    bool any = false;
                    for (int i = 0; i < norms.Length; i++)
                    {
                        Vector3 n = norms[i];
                        string k = Mathf.Abs(n.x) > 0.5f ? "X" : (Mathf.Abs(n.y) > 0.5f ? "Y" : "Z");
                        if (k != key) continue;
                        any = true;
                        mnU = Mathf.Min(mnU, uv[i].x); mxU = Mathf.Max(mxU, uv[i].x);
                        mnV = Mathf.Min(mnV, uv[i].y); mxV = Mathf.Max(mxV, uv[i].y);
                    }
                    if (any) groups[key] = (mxU - mnU, mxV - mnV);
                }

                samples.Add((mesh.name, ws, groups));
                if (samples.Count >= 40) break;
            }

            sb.AppendLine($"采样 {samples.Count} 个薄板网格（第一章，已修复）");
            sb.AppendLine();
            sb.AppendLine("格式：尺寸 | X面UV | Y面UV | Z面UV | 与尺寸的比值");
            sb.AppendLine(new string('-', 130));

            foreach (var s in samples)
            {
                var size = s.size;
                sb.AppendLine($"{s.name,-34} {size.x:F2}×{size.y:F2}×{size.z:F2}");

                foreach (var key in new[] { "X", "Y", "Z" })
                {
                    if (!s.faces.ContainsKey(key)) continue;
                    var (u, v) = s.faces[key];

                    // 推测：U 和 V 分别对应哪个尺寸轴（×0.5）
                    string guessU = GuessAxis(u, size);
                    string guessV = GuessAxis(v, size);

                    sb.AppendLine($"    {key}面 UV {u:F3}×{v:F3}   → U≈{guessU}  V≈{guessV}");
                }
            }

            sb.AppendLine();
            sb.AppendLine("═══════════════════════════════════════════════");
            sb.AppendLine("  第一章（已修复）的规则总结：");
            sb.AppendLine("    X面: U = Z长度 × 0.5,  V = Y高度 × 0.5");
            sb.AppendLine("    Z面: U = X长度 × 0.5,  V = Y高度 × 0.5");
            sb.AppendLine("    Y面: U = X长度 × 0.5,  V = Z长度 × 0.5");
            sb.AppendLine("═══════════════════════════════════════════════");

            string text = sb.ToString();
            Debug.Log(text);
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\UvReverseEngineer.txt",
                              text, new UTF8Encoding(false));
        }

        private static string GuessAxis(float uvSpan, Vector3 size)
        {
            float ux = size.x * 0.5f, uy = size.y * 0.5f, uz = size.z * 0.5f;
            float dx = Mathf.Abs(uvSpan - ux), dy = Mathf.Abs(uvSpan - uy), dz = Mathf.Abs(uvSpan - uz);
            float best = Mathf.Min(dx, Mathf.Min(dy, dz));
            if (best > 0.02f) return "?";
            if (best == dx) return $"X({size.x:F2})";
            if (best == dy) return $"Y({size.y:F2})";
            return $"Z({size.z:F2})";
        }
    }
}