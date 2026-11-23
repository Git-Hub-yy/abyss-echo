using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S75a：第五章纹理异常**定位诊断**（只读）。
    ///
    /// 任务要求：先区分 UV 问题 / Tiling 问题 / 贴图问题 / Shader 问题，
    /// 不能一律归咎于 UV。
    ///
    /// 本工具输出：
    ///   ① 第五章每种材质的完整参数（Tiling/Metallic/Smoothness/贴图）
    ///   ② 每种材质用在哪些物体上，物体尺寸是多少
    ///   ③ 每个有机物体的「纹理实际尺度」= 物体尺寸 / Tiling / UV密度
    ///   ④ 材质是否与其他章节共享（关键！不能直接改共享材质）
    ///   ⑤ 贴图本身的质量参数（分辨率、Mipmap、各向异性、过滤）
    ///
    /// 只读。
    /// </summary>
    public static class Ch5TextureDiagnose
    {
        private const string SCENE = "Assets/_Project/Scenes/Level_Chapter5_Graybox.unity";

        private static readonly (int ch, string path)[] AllScenes =
        {
            (1, "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity"),
            (2, "Assets/_Project/Scenes/Level_Chapter2_Graybox.unity"),
            (3, "Assets/_Project/Scenes/Level_Chapter3_Graybox.unity"),
            (4, "Assets/_Project/Scenes/Level_Chapter4_Graybox.unity"),
            (5, "Assets/_Project/Scenes/Level_Chapter5_Graybox.unity"),
        };

        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            sb.AppendLine("  第五章 纹理异常定位诊断（只读）");
            sb.AppendLine("═══════════════════════════════════════════════════════════════");

            // ══════════ 一、材质跨章节共享检查 ══════════
            sb.AppendLine();
            sb.AppendLine("══════ 一、材质跨章节使用情况 ══════");
            sb.AppendLine("（这是决定能否直接改材质的关键）");

            var matChapters = new Dictionary<string, HashSet<int>>();

            foreach (var (ch, path) in AllScenes)
            {
                if (!File.Exists(path)) continue;
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                Physics.SyncTransforms();

                foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                {
                    if (r == null || r.sharedMaterial == null) continue;
                    string mn = r.sharedMaterial.name;
                    if (!matChapters.ContainsKey(mn)) matChapters[mn] = new HashSet<int>();
                    matChapters[mn].Add(ch);
                }
            }

            sb.AppendLine();
            sb.AppendLine($"{"材质",-28} {"使用章节",-24} 是否第五章独有");
            sb.AppendLine(new string('-', 80));

            var ch5Mats = new List<string>();
            foreach (var kv in matChapters)
            {
                var chs = new List<int>(kv.Value);
                chs.Sort();
                bool onlyCh5 = chs.Count == 1 && chs[0] == 5;
                string chStr = "Ch" + string.Join(",Ch", chs);

                // 只打印第五章用到的
                if (!kv.Value.Contains(5)) continue;

                ch5Mats.Add(kv.Key);
                sb.AppendLine($"{kv.Key,-28} {chStr,-24} {(onlyCh5 ? "✅ 是" : "❌ 否，共享")}");
            }

            // ══════════ 二、第五章材质完整参数 ══════════
            sb.AppendLine();
            sb.AppendLine("══════ 二、第五章使用的材质完整参数 ══════");
            sb.AppendLine();
            sb.AppendLine($"{"材质",-26} {"Tiling",-10} {"Metallic",-9} {"Smooth",-8} {"BaseMap",-30} {"Normal",-26}");
            sb.AppendLine(new string('-', 130));

            foreach (var mn in ch5Mats)
            {
                Material m = null;
                foreach (var g in AssetDatabase.FindAssets($"t:Material {mn}"))
                {
                    var p = AssetDatabase.GUIDToAssetPath(g);
                    var tmp = AssetDatabase.LoadAssetAtPath<Material>(p);
                    if (tmp != null && tmp.name == mn) { m = tmp; break; }
                }
                if (m == null) continue;

                var sc = m.GetTextureScale("_BaseMap");
                string baseTex = m.GetTexture("_BaseMap")?.name ?? "无(纯色)";
                string normTex = m.GetTexture("_BumpMap")?.name ?? "无";
                sb.AppendLine($"{m.name,-26} {sc.x,-10:F2} {m.GetFloat("_Metallic"),-9:F2} " +
                              $"{m.GetFloat("_Smoothness"),-8:F2} {Trunc(baseTex, 28),-30} {Trunc(normTex, 24),-26}");
            }

            // ══════════ 三、第五章物体与纹理尺度 ══════════
            sb.AppendLine();
            sb.AppendLine("══════ 三、第五章物体尺寸与纹理实际尺度 ══════");
            sb.AppendLine();
            sb.AppendLine("说明：");
            sb.AppendLine("  纹理实际尺度 = 物体尺寸 / (UV跨度 × Tiling)");
            sb.AppendLine("  含义：这个数字表示「贴图循环一次覆盖多少米」");
            sb.AppendLine("  太大 → 纹理被拉伸模糊；太小 → 纹理重复过密");

            EditorSceneManager.OpenScene(SCENE, OpenSceneMode.Single);
            Physics.SyncTransforms();

            var byMat = new Dictionary<string, List<(string name, Vector3 size, float tiling,
                                                    float uvU, float uvV, float metersPerTileU, float metersPerTileV)>>();

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

                var uv = mesh.uv;
                if (uv == null || uv.Length == 0) continue;

                var m = r.sharedMaterial;
                if (m == null) continue;

                float mnU = 999, mxU = -999, mnV = 999, mxV = -999;
                foreach (var v in uv)
                {
                    mnU = Mathf.Min(mnU, v.x); mxU = Mathf.Max(mxU, v.x);
                    mnV = Mathf.Min(mnV, v.y); mxV = Mathf.Max(mxV, v.y);
                }
                float su = mxU - mnU, sv = mxV - mnV;

                float tiling = m.GetTextureScale("_BaseMap").x;
                var ws = r.bounds.size;

                // 沿两个最大轴的"每贴图覆盖米数"
                float[] dims = { ws.x, ws.y, ws.z };
                System.Array.Sort(dims);
                float d1 = Mathf.Max(0.01f, dims[2]);
                float d2 = Mathf.Max(0.01f, dims[1]);

                float mPerTileU = su > 0.001f ? d1 / (su * tiling) : -1f;
                float mPerTileV = sv > 0.001f ? d2 / (sv * tiling) : -1f;

                if (!byMat.ContainsKey(m.name))
                    byMat[m.name] = new List<(string, Vector3, float, float, float, float, float)>();
                byMat[m.name].Add((r.gameObject.name, ws, tiling, su, sv, mPerTileU, mPerTileV));
            }

            foreach (var kv in byMat)
            {
                sb.AppendLine();
                sb.AppendLine($"──── {kv.Key}  （{kv.Value.Count} 个物体）────");
                sb.AppendLine($"  {"对象",-26} {"尺寸(X×Y×Z)",-24} {"Tiling",-8} {"UV跨度",-16} {"每贴图米数(U/V)",-20}");
                sb.AppendLine("  " + new string('-', 110));

                foreach (var e in kv.Value)
                {
                    sb.AppendLine($"  {Trunc(e.name, 24),-26} " +
                                  $"{e.size.x:F1}×{e.size.y:F1}×{e.size.z:F1}".PadRight(24) +
                                  $"{e.tiling,-8:F2} {e.uvU:F2}×{e.uvV:F2}".PadRight(16) +
                                  $"{(e.metersPerTileU > 0 ? e.metersPerTileU.ToString("F2") : "-")} / " +
                                  $"{(e.metersPerTileV > 0 ? e.metersPerTileV.ToString("F2") : "-")}");
                }
            }

            // ══════════ 四、第五章贴图质量参数 ══════════
            sb.AppendLine();
            sb.AppendLine("══════ 四、第五章使用的贴图质量参数 ══════");
            sb.AppendLine();
            sb.AppendLine($"{"贴图",-40} {"尺寸",-12} {"Mipmap",-8} {"Aniso",-7} {"过滤",-14} {"压缩",-16}");
            sb.AppendLine(new string('-', 110));

            var usedTex = new HashSet<string>();
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (r == null || r.sharedMaterial == null) continue;
                var m = r.sharedMaterial;
                foreach (var prop in new[] { "_BaseMap", "_BumpMap", "_OcclusionMap" })
                {
                    var t = m.GetTexture(prop);
                    if (t != null) usedTex.Add(AssetDatabase.GetAssetPath(t));
                }
            }

            foreach (var p in usedTex)
            {
                if (string.IsNullOrEmpty(p)) continue;
                var ti = AssetImporter.GetAtPath(p) as TextureImporter;
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
                if (ti == null || tex == null) continue;

                sb.AppendLine($"{Trunc(Path.GetFileName(p), 38),-40} " +
                              $"{tex.width + "×" + tex.height,-12} " +
                              $"{(ti.mipmapEnabled ? "开" : "关"),-8} " +
                              $"{ti.anisoLevel,-7} " +
                              $"{ti.filterMode,-14} {ti.textureCompression,-16}");
            }

            string text = sb.ToString();
            Debug.Log(text);
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Ch5TextureDiagnose.txt",
                              text, new UTF8Encoding(false));
        }

        private static string Trunc(string s, int n) => s.Length <= n ? s : s.Substring(0, n - 1) + "…";
    }
}