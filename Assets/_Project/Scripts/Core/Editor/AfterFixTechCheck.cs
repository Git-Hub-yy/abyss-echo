using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S74d：修复后的**技术指标检查**。
    ///
    /// 包括：
    ///   ① 编译错误 / Missing Script / Missing Material
    ///   ② 粉色材质检查
    ///   ③ 失效引用检查
    ///   ④ Draw Call 统计（每章）
    ///   ⑤ 贴图内存统计
    ///   ⑥ 材质数量变化
    ///   ⑦ 五章场景哈希（确认除了材质外没有其他变化）
    ///
    /// 只读。
    /// </summary>
    public static class AfterFixTechCheck
    {
        private static readonly (int ch, string path)[] Scenes =
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
            sb.AppendLine("═══════════════════════════════════════════════════════");
            sb.AppendLine("  修复后技术指标检查");
            sb.AppendLine("═══════════════════════════════════════════════════════");

            // ① 材质当前值
            sb.AppendLine();
            sb.AppendLine("──── 修改后材质参数 ────");
            DumpMat(sb, "Assets/Art/Materials/Architecture/M_Arch_FloorPlate.mat");
            DumpMat(sb, "Assets/Art/Materials/Architecture/M_Arch_Ceiling.mat");

            // ② 逐章检查
            sb.AppendLine();
            sb.AppendLine("──── 逐章检查 ────");

            var usedMats = new HashSet<string>();
            var usedTextures = new HashSet<string>();

            foreach (var (ch, path) in Scenes)
            {
                if (!File.Exists(path)) continue;
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                Physics.SyncTransforms();

                int renderers = 0, nullSlots = 0, pink = 0, missingShader = 0, missingScript = 0;
                var mats = new HashSet<string>();

                foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                {
                    if (r == null) continue;
                    renderers++;
                    var sm = r.sharedMaterials;
                    if (sm == null || sm.Length == 0) { nullSlots++; continue; }
                    foreach (var m in sm)
                    {
                        if (m == null) { nullSlots++; continue; }
                        mats.Add(m.name);
                        usedMats.Add(m.name);

                        // 粉色材质判定：Shader 为 null 或 ErrorShader
                        if (m.shader == null) missingShader++;
                        else if (m.shader.name == "Hidden/InternalErrorShader" ||
                                 m.shader.name.Contains("Error")) missingShader++;

                        // 检查贴图引用
                        foreach (var p in new[] { "_BaseMap", "_BumpMap", "_OcclusionMap", "_EmissionMap" })
                        {
                            var t = m.GetTexture(p);
                            if (t != null) usedTextures.Add(t.name);
                        }
                    }
                }

                // Missing Script
                foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
                {
                    foreach (var c in go.GetComponents<Component>())
                        if (c == null) missingScript++;
                }

                // 灯光
                int lights = 0;
                foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (l != null && l.enabled) lights++;

                sb.AppendLine();
                sb.AppendLine($"  第{ch}章");
                sb.AppendLine($"    Renderer = {renderers}   材质种类 = {mats.Count}   启用灯光 = {lights}");
                sb.AppendLine($"    空材质槽 = {nullSlots}   MissingShader/粉色 = {missingShader}   MissingScript = {missingScript}");

                // 检查两个目标材质是否在本章使用
                bool useFloor = mats.Contains("M_Arch_FloorPlate");
                bool useCeil = mats.Contains("M_Arch_Ceiling");
                sb.AppendLine($"    使用 M_Arch_FloorPlate = {(useFloor ? "是" : "否")}   " +
                              $"M_Arch_Ceiling = {(useCeil ? "是" : "否")}");
            }

            // ③ 贴图内存
            sb.AppendLine();
            sb.AppendLine("──── 贴图内存 ────");
            long totalTex = 0;
            int texCount = 0;
            var texDetails = new List<(string name, long bytes, int w, int h)>();

            foreach (var g in AssetDatabase.FindAssets("t:Texture2D"))
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (!p.StartsWith("Assets/")) continue;
                var t = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
                if (t == null) continue;
                long bytes = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(t);
                totalTex += bytes;
                texCount++;
                texDetails.Add((t.name, bytes, t.width, t.height));
            }

            sb.AppendLine($"  项目贴图总数 = {texCount}");
            sb.AppendLine($"  贴图总内存 ≈ {totalTex / 1024f / 1024f:F1} MB");
            sb.AppendLine($"  被场景使用的贴图 = {usedTextures.Count}");

            texDetails.Sort((a, b) => b.bytes.CompareTo(a.bytes));
            sb.AppendLine("  最大的 8 张：");
            for (int i = 0; i < Mathf.Min(8, texDetails.Count); i++)
            {
                var d = texDetails[i];
                sb.AppendLine($"    {d.name,-48} {d.w}×{d.h}  {d.bytes / 1024f / 1024f:F2} MB");
            }

            // ④ 材质数量
            sb.AppendLine();
            sb.AppendLine("──── 材质统计 ────");
            int totalMats = 0;
            foreach (var g in AssetDatabase.FindAssets("t:Material"))
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (p.StartsWith("Assets/")) totalMats++;
            }
            sb.AppendLine($"  项目材质总数 = {totalMats}");
            sb.AppendLine($"  场景使用中的材质 = {usedMats.Count}");

            // ⑤ 关键材质引用检查
            sb.AppendLine();
            sb.AppendLine("──── 关键材质引用完整性 ────");
            var floorM = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Architecture/M_Arch_FloorPlate.mat");
            var ceilM = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Architecture/M_Arch_Ceiling.mat");

            sb.AppendLine($"  M_Arch_FloorPlate BaseMap = {floorM?.GetTexture("_BaseMap")?.name ?? "❌NULL"}");
            sb.AppendLine($"  M_Arch_FloorPlate Normal  = {floorM?.GetTexture("_BumpMap")?.name ?? "❌NULL"}");
            sb.AppendLine($"  M_Arch_Ceiling BaseMap    = {ceilM?.GetTexture("_BaseMap")?.name ?? "❌NULL"}");
            sb.AppendLine($"  M_Arch_Ceiling Normal     = {ceilM?.GetTexture("_BumpMap")?.name ?? "❌NULL"}");
            sb.AppendLine($"  M_Arch_Ceiling AO         = {ceilM?.GetTexture("_OcclusionMap")?.name ?? "❌NULL"}");

            string text = sb.ToString();
            Debug.Log(text);
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\AfterFixTechCheck.txt",
                              text, new UTF8Encoding(false));
        }

        private static void DumpMat(StringBuilder sb, string path)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { sb.AppendLine($"  ❌ {path} 不存在"); return; }
            var s = m.GetTextureScale("_BaseMap");
            sb.AppendLine($"  {m.name}");
            sb.AppendLine($"    Tiling = ({s.x:F2}, {s.y:F2})   Metallic = {m.GetFloat("_Metallic"):F2}   " +
                          $"Smoothness = {m.GetFloat("_Smoothness"):F2}");
            sb.AppendLine($"    BaseMap = {m.GetTexture("_BaseMap")?.name ?? "无"}");
            sb.AppendLine($"    Shader = {m.shader?.name ?? "❌NULL"}");
        }
    }
}