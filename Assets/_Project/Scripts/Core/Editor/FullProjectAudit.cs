using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S73：全项目五章材质 / PBR / UV 专项审计。
    ///
    /// **绝对只读。不修改任何文件。**
    ///
    /// 本工具只做数据采集，不做判定 —— 判定逻辑放在报告里人工分析，
    /// 避免出现之前"判定标准错误导致误报"的问题。
    ///
    /// 采集内容：
    ///   一、材质覆盖率
    ///       · 每章 Renderer 总数 / 材质槽总数
    ///       · 空槽 / URP 默认 Lit / 纯色无贴图 / 有贴图
    ///       · 失效引用 / 粉色 / 错误 Shader
    ///       · 材质的实际 Shader、资源路径、关键属性
    ///   二、PBR 贴图
    ///       · BaseMap / NormalMap / OcclusionMap / Emission 状态
    ///       · Metallic / Smoothness 数值
    ///       · 贴图的导入设置（NormalMap 类型、sRGB、压缩）
    ///   三、UV
    ///       · 每个 MESH_ 网格的尺寸、UV 范围、Tiling、密度
    ///       · 每个面组的 UV（不是整网格），便于人工判断
    ///   四、材质复用
    ///       · 材质使用次数分布
    ///       · 未被使用的材质
    /// </summary>
    public static class FullProjectAudit
    {
        private static readonly (int ch, string path)[] Scenes =
        {
            (1, "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity"),
            (2, "Assets/_Project/Scenes/Level_Chapter2_Graybox.unity"),
            (3, "Assets/_Project/Scenes/Level_Chapter3_Graybox.unity"),
            (4, "Assets/_Project/Scenes/Level_Chapter4_Graybox.unity"),
            (5, "Assets/_Project/Scenes/Level_Chapter5_Graybox.unity"),
        };

        /// <summary>URP/Lit Shader 的 GUID（用于判断是否真的是 URP Lit）</summary>
        private const string URP_LIT_GUID = "933532a4fcc9baf4fa0491de14d08ed7";

        private static readonly HashSet<string> UsedMaterials = new HashSet<string>();

        [MenuItem("ABYSS ECHO/审计 v10.0/全项目材质UV审计")]
        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("################################################################");
            sb.AppendLine("#  ABYSS ECHO 全项目五章 材质 / PBR / UV 审计");
            sb.AppendLine("#  只读审计 —— 未修改任何文件");
            sb.AppendLine("################################################################");
            sb.AppendLine();

            // ══ 一、逐章材质覆盖 ══
            sb.AppendLine("############################################################");
            sb.AppendLine("#  第一项：材质覆盖率");
            sb.AppendLine("############################################################");

            var chapterData = new Dictionary<int, ChapterStat>();

            foreach (var (ch, path) in Scenes)
            {
                if (!File.Exists(path)) { sb.AppendLine($"\n第{ch}章：❌ 场景不存在 {path}"); continue; }
                var stat = AuditChapter(sb, ch, path);
                chapterData[ch] = stat;
            }

            // ══ 汇总表 ══
            sb.AppendLine();
            sb.AppendLine("════════════════════════════════════════════════════════════════");
            sb.AppendLine("  五章材质覆盖率总表");
            sb.AppendLine("════════════════════════════════════════════════════════════════");
            sb.AppendLine($"{"章节",-6} {"Renderer",-10} {"材质槽",-9} {"空槽",-7} {"默认Lit",-9} {"纯色",-7} {"有贴图",-8} {"材质种类",-10} {"失效引用",-9}");
            sb.AppendLine(new string('-', 90));
            foreach (var ch in new[] { 1, 2, 3, 4, 5 })
            {
                if (!chapterData.ContainsKey(ch)) continue;
                var s = chapterData[ch];
                sb.AppendLine($"{("第" + ch + "章"),-6} {s.renderers,-10} {s.slots,-9} {s.nullSlots,-7} " +
                              $"{s.defaultLit,-9} {s.solid,-7} {s.withTex,-8} {s.matKinds,-10} {s.brokenRefs,-9}");
            }

            // ══ 二、材质清单（去重，带完整属性）══
            sb.AppendLine();
            sb.AppendLine("############################################################");
            sb.AppendLine("#  第二项：使用中的材质完整清单（含 PBR 属性）");
            sb.AppendLine("############################################################");
            DumpMaterialDetails(sb);

            // ══ 三、未使用的材质 ══
            sb.AppendLine();
            sb.AppendLine("############################################################");
            sb.AppendLine("#  第三项：创建但未被使用的材质");
            sb.AppendLine("############################################################");
            DumpUnusedMaterials(sb);

            // ══ 四、贴图导入设置 ══
            sb.AppendLine();
            sb.AppendLine("############################################################");
            sb.AppendLine("#  第四项：贴图导入设置检查");
            sb.AppendLine("############################################################");
            DumpTextureSettings(sb);

            // ══ 五、UV 明细 ══
            sb.AppendLine();
            sb.AppendLine("############################################################");
            sb.AppendLine("#  第五项：UV 明细（逐面组）");
            sb.AppendLine("############################################################");
            DumpUvDetails(sb);

            string text = sb.ToString();
            Debug.Log("审计完成");
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\FullProjectAudit_Raw.txt",
                              text, new UTF8Encoding(false));
        }

        // ════════════════════════════════════════════════════════
        //  逐章扫描
        // ════════════════════════════════════════════════════════

        private class ChapterStat
        {
            public int renderers, slots, nullSlots, defaultLit, solid, withTex, matKinds, brokenRefs;
        }

        private static ChapterStat AuditChapter(StringBuilder sb, int ch, string path)
        {
            sb.AppendLine();
            sb.AppendLine($"────────── 第{ch}章 ──────────");
            sb.AppendLine($"场景：{path}");

            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Physics.SyncTransforms();

            var stat = new ChapterStat();
            var matStat = new Dictionary<string, int>();
            var matInfo = new Dictionary<string, Material>();

            var renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            stat.renderers = renderers.Length;

            foreach (var r in renderers)
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                if (mats == null || mats.Length == 0) { stat.nullSlots++; continue; }

                foreach (var m in mats)
                {
                    stat.slots++;
                    if (m == null) { stat.nullSlots++; stat.brokenRefs++; continue; }

                    if (!matStat.ContainsKey(m.name)) { matStat[m.name] = 0; matInfo[m.name] = m; }
                    matStat[m.name]++;

                    string assetPath = AssetDatabase.GetAssetPath(m);

                    // 判定是否为 URP 包内置默认 Lit
                    bool isDefaultLit = assetPath.StartsWith("Packages/") && m.name == "Lit";

                    if (isDefaultLit) stat.defaultLit++;

                    bool hasTex = m.GetTexture("_BaseMap") != null;
                    if (hasTex) stat.withTex++; else stat.solid++;

                    if (m.shader == null || m.shader.name == "Hidden/InternalErrorShader" ||
                        m.shader.name.Contains("Error"))
                        stat.brokenRefs++;

                    UsedMaterials.Add(assetPath);
                }
            }

            stat.matKinds = matStat.Count;

            sb.AppendLine($"  Renderer = {stat.renderers}   材质槽 = {stat.slots}");
            sb.AppendLine($"  空槽 = {stat.nullSlots}   URP默认Lit = {stat.defaultLit}   " +
                          $"纯色(无BaseMap) = {stat.solid}   有贴图 = {stat.withTex}");
            sb.AppendLine($"  材质种类 = {stat.matKinds}   失效/错误 = {stat.brokenRefs}");

            sb.AppendLine();
            sb.AppendLine($"  材质使用分布：");
            var keys = new List<string>(matStat.Keys);
            keys.Sort((a, b) => matStat[b].CompareTo(matStat[a]));
            foreach (var k in keys)
            {
                var m = matInfo[k];
                string p = AssetDatabase.GetAssetPath(m);
                bool hasTex = m.GetTexture("_BaseMap") != null;
                sb.AppendLine($"    {k,-30} {matStat[k],4} 次  " +
                              $"{(hasTex ? "贴图✓" : "纯色 ")}  {p}");
            }

            return stat;
        }

        // ════════════════════════════════════════════════════════
        //  材质详细属性
        // ════════════════════════════════════════════════════════

        private static void DumpMaterialDetails(StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine($"{"材质名",-30} {"Shader",-14} {"Base",-6} {"Norm",-6} {"AO",-5} {"Metal",-7} {"Smooth",-8} {"Emit",-6} {"Queue",-6} 路径");
            sb.AppendLine(new string('-', 150));

            var seen = new HashSet<string>();
            foreach (var g in AssetDatabase.FindAssets("t:Material"))
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (!UsedMaterials.Contains(p)) continue;
                if (!seen.Add(p)) continue;

                var m = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (m == null) continue;

                sb.AppendLine(DescribeMaterial(m, p));
            }
        }

        private static string DescribeMaterial(Material m, string path)
        {
            string shaderShort = "?";
            if (m.shader != null)
            {
                string sn = m.shader.name;
                if (sn.Contains("Universal Render Pipeline/Lit")) shaderShort = "URP/Lit";
                else if (sn.Contains("Unlit")) shaderShort = "URP/Unlit";
                else if (sn.Contains("Error")) shaderShort = "❌ERROR";
                else shaderShort = sn.Length > 12 ? sn.Substring(0, 12) : sn;
            }

            bool hasBase = m.GetTexture("_BaseMap") != null;
            bool hasNorm = m.GetTexture("_BumpMap") != null;
            bool hasAO = m.GetTexture("_OcclusionMap") != null;
            float metal = m.HasProperty("_Metallic") ? m.GetFloat("_Metallic") : -1;
            float smooth = m.HasProperty("_Smoothness") ? m.GetFloat("_Smoothness") : -1;

            bool emitOn = m.IsKeywordEnabled("_EMISSION");
            Color emitCol = m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor") : Color.black;
            float emitMax = emitCol.maxColorComponent;
            string emitStr = emitOn ? (emitMax > 0.01f ? $"✓{emitMax:F1}" : "关键开但黑") : "-";

            string queue = m.renderQueue < 0 ? "自动" : m.renderQueue.ToString();

            return $"{Trunc(m.name, 28),-30} {shaderShort,-14} " +
                   $"{(hasBase ? "✓" : "✗"),-6} {(hasNorm ? "✓" : "✗"),-6} {(hasAO ? "✓" : "✗"),-5} " +
                   $"{metal,-7:F2} {smooth,-8:F2} {emitStr,-6} {queue,-6} {path}";
        }

        private static string Trunc(string s, int n) => s.Length <= n ? s : s.Substring(0, n - 1) + "…";

        // ════════════════════════════════════════════════════════
        //  未使用材质
        // ════════════════════════════════════════════════════════

        private static void DumpUnusedMaterials(StringBuilder sb)
        {
            int unused = 0;
            var list = new List<string>();

            foreach (var g in AssetDatabase.FindAssets("t:Material"))
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (UsedMaterials.Contains(p)) continue;

                var m = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (m == null) continue;

                var deps = AssetDatabase.GetDependencies(p, false);
                unused++;
                list.Add($"  {m.name,-30} {p}");
            }

            list.Sort();
            sb.AppendLine($"未被任何场景引用的材质 = {unused} 个");
            sb.AppendLine();
            foreach (var l in list) sb.AppendLine(l);
        }

        // ════════════════════════════════════════════════════════
        //  贴图导入设置
        // ════════════════════════════════════════════════════════

        private static void DumpTextureSettings(StringBuilder sb)
        {
            sb.AppendLine($"{"贴图",-46} {"类型",-12} {"sRGB",-6} {"尺寸",-8} {"压缩",-16} 问题");
            sb.AppendLine(new string('-', 120));

            var roots = new[]
            {
                "Assets/_Project/Art/Textures",
                "Assets/_Project/Art/Models",
            };

            foreach (var root in roots)
            {
                if (!AssetDatabase.IsValidFolder(root)) continue;
                foreach (var g in AssetDatabase.FindAssets("t:Texture2D", new[] { root }))
                {
                    string p = AssetDatabase.GUIDToAssetPath(g);
                    var ti = AssetImporter.GetAtPath(p) as TextureImporter;
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
                    if (ti == null || tex == null) continue;

                    var issues = new List<string>();

                    // 文件名暗示是法线但不是 NormalMap 类型
                    string lower = Path.GetFileName(p).ToLower();
                    bool nameSuggestsNormal = lower.Contains("normal") || lower.Contains("_nrm") || lower.Contains("_n.");
                    if (nameSuggestsNormal && ti.textureType != TextureImporterType.NormalMap)
                        issues.Add("应设为NormalMap");

                    // 法线图误开 sRGB
                    if (ti.textureType == TextureImporterType.NormalMap && ti.sRGBTexture)
                        issues.Add("NormalMap不应sRGB");

                    // 尺寸过小
                    if (tex.width < 256 || tex.height < 256)
                        issues.Add($"尺寸小({tex.width}×{tex.height})");

                    // 非 Repeat
                    if (ti.wrapMode != TextureWrapMode.Repeat)
                        issues.Add("wrap非Repeat");

                    string comp = ti.textureCompression.ToString();
                    string typeStr = ti.textureType.ToString();

                    sb.AppendLine($"{Trunc(p.Replace("Assets/_Project/Art/Textures/", "").Replace("Assets/_Project/Art/Models/", ""), 44),-46} " +
                                  $"{typeStr,-12} {(ti.sRGBTexture ? "是" : "否"),-6} {tex.width + "×" + tex.height,-8} " +
                                  $"{comp,-16} {string.Join(", ", issues)}");
                }
            }
        }

        // ════════════════════════════════════════════════════════
        //  UV 明细
        // ════════════════════════════════════════════════════════

        private static void DumpUvDetails(StringBuilder sb)
        {
            foreach (var (ch, path) in Scenes)
            {
                if (!File.Exists(path)) continue;
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                Physics.SyncTransforms();

                sb.AppendLine();
                sb.AppendLine($"────────── 第{ch}章 UV ──────────");

                // 收集所有 MESH_ 网格
                var seen = new HashSet<Mesh>();
                var entries = new List<(string objName, Mesh mesh, Vector3 size, Material mat, float tiling,
                                        Dictionary<string, Vector4> faces)>();

                var ordered = new List<MeshRenderer>();
                foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                    if (r != null) ordered.Add(r);
                ordered.Sort((a, b) => string.Compare(a.gameObject.name, b.gameObject.name, System.StringComparison.Ordinal));

                foreach (var r in ordered)
                {
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) continue;
                    var mesh = mf.sharedMesh;
                    if (!mesh.name.StartsWith("MESH_")) continue;
                    if (!seen.Add(mesh)) continue;

                    var uv = mesh.uv;
                    var verts = mesh.vertices;
                    var norms = mesh.normals;
                    if (uv == null || verts == null || norms == null || uv.Length == 0) continue;

                    var ws = r.bounds.size;
                    float tiling = r.sharedMaterial != null
                        ? r.sharedMaterial.GetTextureScale("_BaseMap").x : 1f;

                    // 逐面组
                    var faces = new Dictionary<string, Vector4>();   // minU, maxU, minV, maxV
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
                        float mnU = 999, mxU = -999, mnV = 999, mxV = -999;
                        foreach (var i in kv.Value)
                        {
                            mnU = Mathf.Min(mnU, uv[i].x); mxU = Mathf.Max(mxU, uv[i].x);
                            mnV = Mathf.Min(mnV, uv[i].y); mxV = Mathf.Max(mxV, uv[i].y);
                        }
                        faces[kv.Key] = new Vector4(mnU, mxU, mnV, mxV);
                    }

                    entries.Add((r.gameObject.name, mesh, ws, r.sharedMaterial, tiling, faces));
                }

                sb.AppendLine($"  MESH_ 网格数 = {entries.Count}");
                sb.AppendLine();
                sb.AppendLine($"  {"对象",-26} {"尺寸(X×Y×Z)",-22} {"材质",-24} {"Tiling",-7} 面组 UV 跨度 (U×V)");
                sb.AppendLine("  " + new string('-', 150));

                foreach (var e in entries)
                {
                    var f = e.faces;
                    var parts = new List<string>();
                    foreach (var k in new[] { "X", "Y", "Z" })
                    {
                        if (!f.ContainsKey(k)) continue;
                        var v = f[k];
                        parts.Add($"{k}:{(v.y - v.x):F2}×{(v.w - v.z):F2}");
                    }

                    sb.AppendLine($"  {Trunc(e.objName, 24),-26} " +
                                  $"{e.size.x:F1}×{e.size.y:F1}×{e.size.z:F1}".PadRight(22) +
                                  $"{(e.mat != null ? Trunc(e.mat.name, 22) : "NULL"),-24} {e.tiling,-7:F2} " +
                                  string.Join("  ", parts));
                }
            }
        }
    }
}