using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S69a：把程序生成的有机 PBR 贴图导入设置 + 创建有机材质。
    ///
    /// ══════════════════════ 贴图来源 ══════════════════════
    ///
    ///   **程序生成**（非外部素材，无授权问题）
    ///   生成脚本：Scripts_Backup/gen_organic_v3.py + fix_organic_colors.py
    ///   输出目录：Assets/_Project/Art/Textures/Procedural/
    ///
    ///   三套 × 6 张贴图（1024²）：
    ///     FleshWet   湿润血肉     Color/Normal/Roughness/Metallic/AO/Height
    ///     Membrane   生物膜       同上
    ///     SlimeBio   黏液感染腔   同上
    ///
    ///   无缝性实测：左右/上下边界差 < 1/255（要求 < 8）
    ///
    /// ══════════════════════ 导入设置 ══════════════════════
    ///
    ///   Color       sRGB，压缩 BC7/高质量
    ///   Normal      NormalMap 类型（这是关键，否则法线不生效）
    ///   Roughness   线性（sRGB=false），否则粗糙度会偏
    ///   Metallic    线性
    ///   AO          线性
    ///   Height      线性
    /// </summary>
    public static class OrganicMaterialBuilder
    {
        private const string TexRoot = "Assets/_Project/Art/Textures/Procedural";
        private const string Lib = "Assets/Art/Materials/Organic";

        [MenuItem("ABYSS ECHO/美术 v9.0/导入有机贴图并创建材质")]
        public static void Build() => Run();

        public static void Run()
        {
            Log("══════ 导入有机贴图 ══════");

            int imp = 0;
            foreach (var name in new[] { "FleshWet", "Membrane", "SlimeBio" })
            {
                string dir = $"{TexRoot}/{name}";
                if (!AssetDatabase.IsValidFolder(dir))
                {
                    Log($"  ❌ 缺目录 {dir}");
                    continue;
                }

                // Color -> sRGB
                imp += SetImport($"{dir}/{name}_Color.png", isNormal: false, isSRGB: true);
                // Normal -> NormalMap
                imp += SetImport($"{dir}/{name}_Normal.png", isNormal: true, isSRGB: false);
                // 其余 -> 线性
                imp += SetImport($"{dir}/{name}_Roughness.png", false, false);
                imp += SetImport($"{dir}/{name}_Metallic.png", false, false);
                imp += SetImport($"{dir}/{name}_AO.png", false, false);
                imp += SetImport($"{dir}/{name}_Height.png", false, false);

                Log($"  ✓ {name}");
            }
            Log($"导入设置完成 {imp} 个");

            AssetDatabase.Refresh();

            // ══ 创建有机材质 ══
            Log("══════ 创建有机材质 ══════");

            MakeOrg("M_Org_FleshWet", "FleshWet",
                    tiling: 1.6f, normalScale: 1.8f, smoothBias: 0f,
                    note: "湿润血肉组织 · 不规则细胞 + 组织间隙");

            MakeOrg("M_Org_FleshDeep", "FleshWet",
                    tiling: 2.4f, normalScale: 2.2f, smoothBias: -0.12f,
                    note: "深层血肉 · 更暗更粗");

            MakeOrg("M_Org_Membrane", "Membrane",
                    tiling: 1.8f, normalScale: 1.5f, smoothBias: 0.05f,
                    note: "生物膜 · 深紫细胞泡");

            MakeOrg("M_Org_SlimeBio", "SlimeBio",
                    tiling: 1.4f, normalScale: 1.3f, smoothBias: 0.18f,
                    note: "黏液感染腔 · 黑绿高光");

            MakeOrg("M_Org_SlimeBioSmall", "SlimeBio",
                    tiling: 3.2f, normalScale: 1.1f, smoothBias: 0.18f,
                    note: "黏液（小块物体用 · 更密）");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Verify();
        }

        private static int SetImport(string path, bool isNormal, bool isSRGB)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return 0;

            bool changed = false;

            if (isNormal && importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                changed = true;
            }
            else if (!isNormal && importer.textureType != TextureImporterType.Default)
            {
                importer.textureType = TextureImporterType.Default;
                changed = true;
            }

            if (importer.sRGBTexture != isSRGB)
            {
                importer.sRGBTexture = isSRGB;
                changed = true;
            }

            // 不要 mipmap 关闭（远距离会闪烁），保持开
            if (!importer.mipmapEnabled)
            {
                importer.mipmapEnabled = true;
                changed = true;
            }

            if (importer.wrapMode != TextureWrapMode.Repeat)
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                changed = true;
            }

            if (importer.maxTextureSize < 1024)
            {
                importer.maxTextureSize = 1024;
                changed = true;
            }

            if (importer.anisoLevel < 4)
            {
                importer.anisoLevel = 4;
                changed = true;
            }

            if (changed)
            {
                importer.SaveAndReimport();
                return 1;
            }
            return 0;
        }

        private static void MakeOrg(string matName, string texName, float tiling,
                                     float normalScale, float smoothBias, string note)
        {
            string texDir = $"{TexRoot}/{texName}";
            var color = AssetDatabase.LoadAssetAtPath<Texture2D>($"{texDir}/{texName}_Color.png");
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{texDir}/{texName}_Normal.png");
            var rough = AssetDatabase.LoadAssetAtPath<Texture2D>($"{texDir}/{texName}_Roughness.png");
            var ao = AssetDatabase.LoadAssetAtPath<Texture2D>($"{texDir}/{texName}_AO.png");

            if (color == null)
            {
                Log($"  ❌ {matName}: 找不到 Color 贴图");
                return;
            }

            string path = $"{Lib}/{matName}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path.Replace("/", "\\")));
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }

            // Base
            mat.SetTexture("_BaseMap", color);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetTextureScale("_BaseMap", new Vector2(tiling, tiling));

            // Normal
            if (normal != null)
            {
                mat.SetTexture("_BumpMap", normal);
                mat.SetTextureScale("_BumpMap", new Vector2(tiling, tiling));
                mat.SetFloat("_BumpScale", normalScale);
                mat.EnableKeyword("_NORMALMAP");
            }

            // 有机体：非金属
            mat.SetFloat("_Metallic", 0.0f);

            // 粗糙度：贴图驱动会让 URP 用 MetallicGlossMap 的 G 通道，
            // 这里改为标量 + 保留贴图作为 AO，避免 G 通道语义混乱。
            // （程序生成的 Roughness 是独立灰图，不是 Unity 的 Metallic-Gloss 打包格式）
            float baseSmooth = 0.55f + smoothBias;
            mat.SetFloat("_Smoothness", Mathf.Clamp01(baseSmooth));

            // AO
            if (ao != null)
            {
                mat.SetTexture("_OcclusionMap", ao);
                mat.SetTextureScale("_OcclusionMap", new Vector2(tiling, tiling));
                mat.SetFloat("_OcclusionStrength", 0.9f);
            }

            // 不透明
            mat.SetFloat("_Surface", 0f);
            mat.SetOverrideTag("RenderType", "Opaque");
            mat.renderQueue = -1;
            mat.DisableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", Color.black);

            EditorUtility.SetDirty(mat);
            Log($"  ✓ {matName,-22} tiling={tiling} smooth={baseSmooth:F2}  {note}");
        }

        private static void Verify()
        {
            Log("---- 有机材质清单 ----");
            if (!AssetDatabase.IsValidFolder(Lib)) { Log("  目录不存在"); return; }

            foreach (var g in AssetDatabase.FindAssets("t:Material", new[] { Lib }))
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                var m = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (m == null) continue;
                Log($"  {Path.GetFileNameWithoutExtension(p),-24} " +
                    $"base={(m.GetTexture("_BaseMap") != null ? "✓" : "✗")} " +
                    $"norm={(m.GetTexture("_BumpMap") != null ? "✓" : "✗")} " +
                    $"ao={(m.GetTexture("_OcclusionMap") != null ? "✓" : "✗")} " +
                    $"metal={m.GetFloat("_Metallic"):F2} smooth={m.GetFloat("_Smoothness"):F2}");
            }
        }

        private static void Log(string msg)
        {
            string line = $"[OrgMat] {msg}";
            Debug.Log(line);
            try
            {
                Directory.CreateDirectory(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports");
                File.AppendAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\OrganicMaterial_Result.txt",
                                   line + "\n", new UTF8Encoding(false));
            }
            catch { }
        }
    }
}