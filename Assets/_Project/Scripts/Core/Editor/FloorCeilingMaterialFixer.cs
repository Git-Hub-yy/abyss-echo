using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S74b：**第一批材质修复** —— 地板 Tiling + 天花板 Metallic。
    ///
    /// ══════════════════════ 执行前依赖检查结果 ══════════════════════
    ///
    ///   M_Arch_FloorPlate（Tiling 6.0 → 1.0）
    ///     · 93 个材质槽，**全部**是 Floor_* 地板、Floor*_Link 连接地板
    ///     · 无一例墙体、道具、Prefab 使用
    ///     · ✅ 可直接修改，无需创建副本
    ///
    ///   M_Arch_Ceiling（Metallic 0.62 → 0.0）
    ///     · 69 个材质槽，**全部**是天花板类：
    ///         Ceiling_*（60）+ Transit_Ceiling（2）+ Corridor_Ceiling（1）
    ///         + Ch4_CorePath_Ceiling（1）
    ///     · 无一例金属龙骨、框架、装饰件使用
    ///     · ✅ 可直接修改，无需创建副本
    ///
    ///   两者均**无 Prefab 引用**。
    ///
    /// ══════════════════════ 为什么可以直接改共享材质 ══════════════════════
    ///
    ///   "共享材质"的风险在于修改会连带影响别的物体。
    ///   但检查证明这两个材质的使用者**全部是同一类物体**，
    ///   所以改它们不会牵连任何非地板/非天花板对象。
    ///
    ///   因此**不创建副本** —— 避免产生无意义的重复材质
    ///   （审计已指出项目有 100 个未使用材质，不应再增加）。
    ///
    /// ══════════════════════ 修改内容 ══════════════════════
    ///
    ///   M_Arch_FloorPlate:
    ///       _BaseMap Tiling/Speed: 6.0,6.0,0,0  →  1.0,1.0,0,0
    ///       （Metallic / Smoothness / 贴图 均不变）
    ///
    ///   M_Arch_Ceiling:
    ///       _Metallic: 0.62  →  0.00
    ///       （Tiling / Smoothness / 贴图 均不变）
    /// </summary>
    public static class FloorCeilingMaterialFixer
    {
        private const string FLOOR_MAT_PATH = "Assets/Art/Materials/Architecture/M_Arch_FloorPlate.mat";
        private const string CEIL_MAT_PATH = "Assets/Art/Materials/Architecture/M_Arch_Ceiling.mat";

        private const float FLOOR_TILING_OLD = 6.0f;
        private const float FLOOR_TILING_NEW = 1.0f;

        private const float CEIL_METALLIC_OLD = 0.62f;
        private const float CEIL_METALLIC_NEW = 0.00f;

        [MenuItem("ABYSS ECHO/修复 v10.1/执行地板Tiling+天花板Metallic修复")]
        public static void Build() => Run(dryRun: false);

        [MenuItem("ABYSS ECHO/修复 v10.1/预演（只读）")]
        public static void DryRun() => Run(dryRun: true);

        public static void Run(bool dryRun)
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════════════════════");
            sb.AppendLine(dryRun ? "  第一批材质修复 —— 预演（只读）"
                                 : "  第一批材质修复 —— 执行");
            sb.AppendLine("═══════════════════════════════════════════════════════");

            int changed = 0;

            // ══════════════════ 1. 地板 Tiling ══════════════════
            sb.AppendLine();
            sb.AppendLine("──── ① M_Arch_FloorPlate：Tiling 6.0 → 1.0 ────");

            var floorMat = AssetDatabase.LoadAssetAtPath<Material>(FLOOR_MAT_PATH);
            if (floorMat == null)
            {
                sb.AppendLine($"  ❌ 找不到 {FLOOR_MAT_PATH}");
            }
            else
            {
                var oldScale = floorMat.GetTextureScale("_BaseMap");
                var oldOffset = floorMat.GetTextureOffset("_BaseMap");

                sb.AppendLine($"  材质路径: {FLOOR_MAT_PATH}");
                sb.AppendLine($"  修改前: Tiling = ({oldScale.x:F2}, {oldScale.y:F2})  Offset = ({oldOffset.x:F2}, {oldOffset.y:F2})");

                // 记录所有会变的属性（用于报告）
                sb.AppendLine($"  不变项: Metallic={floorMat.GetFloat("_Metallic"):F2}  " +
                              $"Smoothness={floorMat.GetFloat("_Smoothness"):F2}  " +
                              $"BaseMap={floorMat.GetTexture("_BaseMap")?.name ?? "无"}  " +
                              $"Normal={floorMat.GetTexture("_BumpMap")?.name ?? "无"}");

                if (!dryRun)
                {
                    floorMat.SetTextureScale("_BaseMap", new Vector2(FLOOR_TILING_NEW, FLOOR_TILING_NEW));
                    floorMat.SetTextureScale("_BumpMap", new Vector2(FLOOR_TILING_NEW, FLOOR_TILING_NEW));
                    floorMat.SetTextureScale("_OcclusionMap", new Vector2(FLOOR_TILING_NEW, FLOOR_TILING_NEW));
                    EditorUtility.SetDirty(floorMat);
                    changed++;

                    var newScale = floorMat.GetTextureScale("_BaseMap");
                    sb.AppendLine($"  修改后: Tiling = ({newScale.x:F2}, {newScale.y:F2})");
                    sb.AppendLine($"  ✅ 已修改");
                }
                else
                {
                    sb.AppendLine($"  [预演] 将改为 ({FLOOR_TILING_NEW:F2}, {FLOOR_TILING_NEW:F2})");
                }
            }

            // ══════════════════ 2. 天花板 Metallic ══════════════════
            sb.AppendLine();
            sb.AppendLine("──── ② M_Arch_Ceiling：Metallic 0.62 → 0.00 ────");

            var ceilMat = AssetDatabase.LoadAssetAtPath<Material>(CEIL_MAT_PATH);
            if (ceilMat == null)
            {
                sb.AppendLine($"  ❌ 找不到 {CEIL_MAT_PATH}");
            }
            else
            {
                sb.AppendLine($"  材质路径: {CEIL_MAT_PATH}");
                sb.AppendLine($"  修改前: Metallic = {ceilMat.GetFloat("_Metallic"):F2}  " +
                              $"Smoothness = {ceilMat.GetFloat("_Smoothness"):F2}");
                var s = ceilMat.GetTextureScale("_BaseMap");
                sb.AppendLine($"  不变项: Tiling={s.x:F2}  " +
                              $"BaseMap={ceilMat.GetTexture("_BaseMap")?.name ?? "无"}  " +
                              $"Normal={ceilMat.GetTexture("_BumpMap")?.name ?? "无"}  " +
                              $"AO={ceilMat.GetTexture("_OcclusionMap")?.name ?? "无"}");

                if (!dryRun)
                {
                    ceilMat.SetFloat("_Metallic", CEIL_METALLIC_NEW);
                    EditorUtility.SetDirty(ceilMat);
                    changed++;

                    sb.AppendLine($"  修改后: Metallic = {ceilMat.GetFloat("_Metallic"):F2}");
                    sb.AppendLine($"  ✅ 已修改");
                }
                else
                {
                    sb.AppendLine($"  [预演] 将改为 {CEIL_METALLIC_NEW:F2}");
                }
            }

            // ══════════════════ 3. 保存 ══════════════════
            sb.AppendLine();
            sb.AppendLine("──── 保存 ────");
            if (!dryRun && changed > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                sb.AppendLine($"  已保存 {changed} 个材质");
            }
            else if (dryRun)
            {
                sb.AppendLine("  [预演] 未保存任何文件");
            }
            else
            {
                sb.AppendLine("  无需保存");
            }

            // ══════════════════ 4. 验证 ══════════════════
            if (!dryRun)
            {
                sb.AppendLine();
                sb.AppendLine("──── 验证 ────");

                AssetDatabase.Refresh();
                var f2 = AssetDatabase.LoadAssetAtPath<Material>(FLOOR_MAT_PATH);
                var c2 = AssetDatabase.LoadAssetAtPath<Material>(CEIL_MAT_PATH);

                if (f2 != null)
                {
                    var s = f2.GetTextureScale("_BaseMap");
                    bool ok = Mathf.Abs(s.x - FLOOR_TILING_NEW) < 0.01f &&
                              Mathf.Abs(s.y - FLOOR_TILING_NEW) < 0.01f;
                    sb.AppendLine($"  FloorPlate Tiling = ({s.x:F2}, {s.y:F2})  {(ok ? "✅" : "❌")}");
                    sb.AppendLine($"    其他属性未变: Metallic={f2.GetFloat("_Metallic"):F2}  " +
                                  $"Smoothness={f2.GetFloat("_Smoothness"):F2}");
                }
                if (c2 != null)
                {
                    float m = c2.GetFloat("_Metallic");
                    bool ok = Mathf.Abs(m - CEIL_METALLIC_NEW) < 0.01f;
                    sb.AppendLine($"  Ceiling Metallic = {m:F2}  {(ok ? "✅" : "❌")}");
                    var s = c2.GetTextureScale("_BaseMap");
                    sb.AppendLine($"    其他属性未变: Tiling={s.x:F2}  Smoothness={c2.GetFloat("_Smoothness"):F2}");
                }
            }

            string text = sb.ToString();
            Debug.Log(text);
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\FloorCeilingFix_Result.txt",
                              text, new UTF8Encoding(false));
        }
    }
}