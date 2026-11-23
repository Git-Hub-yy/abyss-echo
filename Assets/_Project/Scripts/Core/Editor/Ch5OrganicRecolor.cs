using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S76c：第五章有机材质**配色重做**（《死亡空间》工业血肉风）。
    ///
    /// ══════════════════════ 问题诊断（已验证）══════════════════════
    ///
    ///   现象：出生点附近的紫/粉/绿有机墙面像"荧光塑料"
    ///
    ///   原因1（主因）：_BaseColor 亮度过高
    ///       M_Org_Membrane  亮度 4.86
    ///       M_Org_SlimeBio  亮度 5.09
    ///       M_Org_FleshWet  亮度 3.80
    ///       对比金属材质：M_Arch_WallSteel 0.55
    ///       → 有机材质是金属的 7~9 倍
    ///
    ///   原因2（叠加因素）：出生点灯光密集
    ///       25 米内 52 盏启用点光，Ceiling 强度 9.0 范围 18 米
    ///       → 同一面墙受 5~8 盏灯照射
    ///       → 4.86 × 多灯叠加 = 过曝
    ///
    ///   **灯光不能改（任务禁止），所以只能降材质亮度。**
    ///
    /// ══════════════════════ 共享材质处理 ══════════════════════
    ///
    ///   M_Org_Membrane   Ch3=14  Ch4=8   Ch5=54   ❌ 共享
    ///   M_Org_FleshDeep  Ch4=12  Ch5=22           ❌ 共享
    ///   M_Org_FleshWet   Ch5=35                   ✅ 独有
    ///   M_Org_SlimeBio   Ch5=15                   ✅ 独有
    ///
    ///   共享的两个材质：
    ///       · 为第五章**创建专用副本**（M_Org_Membrane_Ch5 / M_Org_FleshDeep_Ch5）
    ///       · 只替换第五章场景里的引用
    ///       · 第三、四章原材质**完全不动**
    ///
    /// ══════════════════════ 目标配色（死亡空间风格）══════════════════════
    ///
    ///   不是靠"荧光色"表现有机，而是靠：
    ///       · 暗红/褐红的血肉感
    ///       · 深紫黑的膜（几乎黑，只在边缘透紫）
    ///       · 墨绿的黏液（暗淡，不发亮）
    ///       · 高 Smoothness 制造湿润反光（这是关键，不是靠亮度）
    ///
    ///   具体值：
    ///       FleshWet    (0.42, 0.15, 0.12)  亮度 0.42   Smoothness 0.55（湿润）
    ///       FleshDeep   (0.28, 0.10, 0.09)  亮度 0.28   Smoothness 0.45
    ///       Membrane    (0.24, 0.16, 0.28)  亮度 0.28   Smoothness 0.62（膜的反光）
    ///       SlimeBio    (0.18, 0.28, 0.19)  亮度 0.28   Smoothness 0.72（黏液最湿）
    /// </summary>
    public static class Ch5OrganicRecolor
    {
        private const string CH5_SCENE = "Assets/_Project/Scenes/Level_Chapter5_Graybox.unity";
        private const string ORGANIC_DIR = "Assets/Art/Materials/Organic";

        // 目标配色
        private static readonly Dictionary<string, (Color color, float smooth, float metallic)> Targets =
            new Dictionary<string, (Color, float, float)>
        {
            // ══════════════════════ 第二轮调整 ══════════════════════
            //
            //   第一轮把亮度降到 0.28，结果全部变灰黑，有机感全失。
            //   教训：不能只看"过曝"，还要保证颜色能认出来。
            //
            //   第二轮原则：
            //     · 保持颜色可辨识（饱和度 0.3~0.5，不要低于 0.3）
            //     · 亮度取 0.9~1.3（金属是 0.4~0.6，有机略高但有上限）
            //     · 靠 Smoothness 表现湿润，不靠亮度
            //
            //   亮度对比：
            //     原值 4.86 / 3.80 / 5.09  →  过曝
            //     第一轮 0.28                          →  全黑
            //     本轮   0.9~1.3                      →  有颜色但不过曝
            //
            // 血肉：暗红褐，有血色但不刺眼
            { "M_Org_FleshWet",  (new Color(1.15f, 0.42f, 0.34f), 0.58f, 0.00f) },
            // 深处的血肉：更深
            { "M_Org_FleshDeep", (new Color(0.80f, 0.28f, 0.24f), 0.45f, 0.00f) },
            // 膜：紫，但压暗（原 4.86 → 1.05）
            { "M_Org_Membrane",  (new Color(0.72f, 0.48f, 0.85f), 0.62f, 0.00f) },
            // 黏液：墨绿（原 5.09 → 1.00）
            { "M_Org_SlimeBio",  (new Color(0.42f, 0.82f, 0.48f), 0.72f, 0.00f) },
        };

        [MenuItem("ABYSS ECHO/修复 v10.3/第五章有机配色重做")]
        public static void Build() => Run(dryRun: false);

        [MenuItem("ABYSS ECHO/修复 v10.3/预演（只读）")]
        public static void DryRun() => Run(dryRun: true);

        public static void Run(bool dryRun)
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            sb.AppendLine(dryRun ? "  第五章有机配色重做 —— 预演"
                                 : "  第五章有机配色重做 —— 执行");
            sb.AppendLine("═══════════════════════════════════════════════════════════════");

            // ⚠️ 先判断共享关系（这会切换场景），必须在打开第五章之前做
            var sharedMap = new Dictionary<string, bool>();
            foreach (var key in Targets.Keys)
                sharedMap[key] = IsSharedAcrossChapters(key);

            // 再打开第五章
            var scene = EditorSceneManager.OpenScene(CH5_SCENE, OpenSceneMode.Single);
            Physics.SyncTransforms();

            // ══════ 1. 处理共享材质：创建第五章专用副本 ══════
            sb.AppendLine();
            sb.AppendLine("──── ① 共享材质处理 ────");

            var matMap = new Dictionary<string, Material>();   // 原材质名 → 第五章应使用的材质

            foreach (var kv in Targets)
            {
                string name = kv.Key;
                string srcPath = $"{ORGANIC_DIR}/{name}.mat";
                var srcMat = AssetDatabase.LoadAssetAtPath<Material>(srcPath);
                if (srcMat == null) { sb.AppendLine($"  ❌ 找不到 {srcPath}"); continue; }

                // 判断是否共享
                bool shared = sharedMap.ContainsKey(name) && sharedMap[name];

                if (!shared)
                {
                    sb.AppendLine($"  {name,-20} 第五章独有 → 直接修改原材质");
                    matMap[name] = srcMat;

                    if (!dryRun) ApplyTarget(srcMat, kv.Value);
                }
                else
                {
                    string copyPath = $"{ORGANIC_DIR}/{name}_Ch5.mat";
                    var copyMat = AssetDatabase.LoadAssetAtPath<Material>(copyPath);

                    sb.AppendLine($"  {name,-20} 跨章节共享 → 创建副本 {name}_Ch5");

                    if (!dryRun)
                    {
                        if (copyMat == null)
                        {
                            copyMat = new Material(srcMat);
                            copyMat.name = $"{name}_Ch5";
                            AssetDatabase.CreateAsset(copyMat, copyPath);
                            sb.AppendLine($"       + 新建 {copyPath}");
                        }
                        else
                        {
                            sb.AppendLine($"       = 副本已存在，更新参数");
                        }
                        ApplyTarget(copyMat, kv.Value);
                        EditorUtility.SetDirty(copyMat);
                    }
                    else
                    {
                        sb.AppendLine($"       [预演] 将在 {copyPath} 创建/更新");
                    }

                    matMap[name] = copyMat;   // dryRun 时可能是 null，用于统计
                }
            }

            if (!dryRun)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            // ══════ 2. 替换第五章场景引用 ══════
            sb.AppendLine();
            sb.AppendLine("──── ② 替换第五章场景引用 ────");

            int replaced = 0;
            var byMat = new Dictionary<string, int>();

            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (r == null || r.sharedMaterial == null) continue;
                string cur = r.sharedMaterial.name;

                // 如果当前是原材质名，且目标是 Ch5 副本，则替换
                if (matMap.ContainsKey(cur))
                {
                    var target = matMap[cur];
                    if (target == null) continue;
                    if (target == r.sharedMaterial) continue;   // 已经是目标

                    if (!byMat.ContainsKey(cur)) byMat[cur] = 0;
                    byMat[cur]++;

                    if (!dryRun)
                    {
                        r.sharedMaterial = target;
                        EditorUtility.SetDirty(r);
                        replaced++;
                    }
                }
            }

            foreach (var kv in byMat)
                sb.AppendLine($"  {kv.Key,-22} → 替换 {kv.Value} 个对象的引用");

            sb.AppendLine($"  合计替换 {replaced} 个引用");

            // ══════ 3. 保存 ══════
            if (!dryRun)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                bool ok = EditorSceneManager.SaveScene(scene, CH5_SCENE);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                sb.AppendLine();
                sb.AppendLine(ok ? "  ✅ 场景已保存" : "  ❌ 保存失败");
            }
            else
            {
                sb.AppendLine();
                sb.AppendLine("  [预演] 未保存任何文件");
            }

            // ══════ 4. 验证 ══════
            if (!dryRun)
            {
                sb.AppendLine();
                sb.AppendLine("──── ③ 验证（第五章实际使用的材质）────");
                var check = new HashSet<string>();
                foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                {
                    if (r == null || r.sharedMaterial == null) continue;
                    string n = r.sharedMaterial.name;
                    if (!n.StartsWith("M_Org_")) continue;
                    if (!check.Add(n)) continue;

                    var m = r.sharedMaterial;
                    var bc = m.GetColor("_BaseColor");
                    sb.AppendLine($"  {n,-26} _BaseColor=({bc.r:F2},{bc.g:F2},{bc.b:F2})  " +
                                  $"亮度={bc.maxColorComponent:F2}  Smoothness={m.GetFloat("_Smoothness"):F2}");
                }

                sb.AppendLine();
                sb.AppendLine("──── ④ 确认第三、四章材质未变 ────");
                foreach (var n in new[] { "M_Org_Membrane", "M_Org_FleshDeep" })
                {
                    var m = AssetDatabase.LoadAssetAtPath<Material>($"{ORGANIC_DIR}/{n}.mat");
                    if (m == null) continue;
                    var bc = m.GetColor("_BaseColor");
                    sb.AppendLine($"  {n,-22} _BaseColor=({bc.r:F2},{bc.g:F2},{bc.b:F2})  亮度={bc.maxColorComponent:F2}  " +
                                  $"{(bc.maxColorComponent > 2f ? "✅ 保持原值（未动）" : "⚠️ 被改了")}");
                }
            }

            string text = sb.ToString();
            Debug.Log(text);
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Ch5OrganicRecolor_Result.txt",
                              text, new UTF8Encoding(false));
        }

        private static void ApplyTarget(Material m, (Color color, float smooth, float metallic) t)
        {
            m.SetColor("_BaseColor", new Color(t.color.r, t.color.g, t.color.b, 1f));
            m.SetFloat("_Smoothness", t.smooth);
            m.SetFloat("_Metallic", t.metallic);
        }

        /// <summary>判断材质是否被第三或第四章引用。</summary>
        private static bool IsSharedAcrossChapters(string matName)
        {
            string[] others =
            {
                "Assets/_Project/Scenes/Level_Chapter3_Graybox.unity",
                "Assets/_Project/Scenes/Level_Chapter4_Graybox.unity",
            };
            foreach (var p in others)
            {
                if (!File.Exists(p)) continue;
                var s = EditorSceneManager.OpenScene(p, OpenSceneMode.Single);
                foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                {
                    if (r == null || r.sharedMaterial == null) continue;
                    if (r.sharedMaterial.name == matName) return true;
                }
            }
            return false;
        }
    }
}