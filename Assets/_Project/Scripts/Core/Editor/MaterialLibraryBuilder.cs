using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S3B 材质库构建器：把 ambientCG 下载的 2K PBR 贴图配置成正确的导入设置，
    /// 并生成 URP Lit 材质（Color + Normal + Roughness + Metalness + AO）。
    ///
    /// 为什么需要自动配置导入设置：
    ///  - 法线贴图必须设 Texture Type = Normal map，否则会显示成彩色噪声；
    ///  - Roughness / Metalness 是**线性数据**，必须关闭 sRGB，否则材质表现全错；
    ///  - 这些设置在 Inspector 里逐个点需要 20+ 次操作，且极易漏。
    ///
    /// 菜单：ABYSS ECHO/S3B 构建材质库
    /// </summary>
    public static class MaterialLibraryBuilder
    {
        private const string TexRoot = "Assets/_Project/Art/Textures/ambientCG";
        private const string MatRoot = "Assets/_Project/Art/Materials/Level";

        /// <summary>材质定义：ID、贴图集、表面参数、用途说明。</summary>
        private class MatSpec
        {
            public string Id;
            public string TextureSet;     // 对应 Textures/ambientCG 下的子目录
            public float MetallicOverride = 0.08f;  // <0 表示使用贴图的金属度
            public float Smoothness = 0.45f;
            public float Brightness = 1f;           // _BaseColor 乘数；>1 提亮（HDR），用于补偿过暗的贴图
            public Vector2 Tiling = new Vector2(1f, 1f);
            public string Note;
        }

        // 第一章实际使用的材质（按区域功能划分）
        // Brightness 说明：ambientCG 的工业材质普遍偏暗，
        //   Metal027（涂装金属）实测仅 RGB≈30/255，必须提亮约 3 倍才可见；
        //   其余材质按各自明度做小幅补偿，保持整体明暗层次。
        private static readonly MatSpec[] Specs =
        {
            new MatSpec { Id = "M_Wall_Metal",        TextureSet = "Metal027",         MetallicOverride = 0.08f, Smoothness = 0.42f, Brightness = 4.0f, Tiling = new Vector2(1f, 1f),  Note = "涂装金属墙板（走廊/房间）" },
            new MatSpec { Id = "M_Floor_Industrial",  TextureSet = "DiamondPlate009",  MetallicOverride = 0.10f, Smoothness = 0.35f, Brightness = 2.6f, Tiling = new Vector2(1f, 1f),  Note = "花纹钢板（工业地面）" },
            new MatSpec { Id = "M_Concrete",          TextureSet = "Concrete048",      MetallicOverride = 0f, Smoothness = 0.18f, Brightness = 1.15f, Tiling = new Vector2(1f, 1f), Note = "混凝土（舱室/维护区）" },
            new MatSpec { Id = "M_Metal_Rusted",      TextureSet = "MetalPlates013",   MetallicOverride = 0.08f, Smoothness = 0.30f, Brightness = 3.0f, Tiling = new Vector2(1f, 1f),  Note = "锈蚀金属（管道/机柜/设备）" },
            new MatSpec { Id = "M_Pipe",              TextureSet = "Pipe001",          MetallicOverride = 0.10f, Smoothness = 0.36f, Brightness = 3.0f, Tiling = new Vector2(1f, 1f),  Note = "管道表面" },
            new MatSpec { Id = "M_Tiles",             TextureSet = "Tiles141",         MetallicOverride = 0f, Smoothness = 0.55f, Brightness = 1.3f, Tiling = new Vector2(1f, 1f), Note = "瓷砖（洗手间/医疗室）" },
        };

        [MenuItem("ABYSS ECHO/S3B 构建材质库")]
        public static void Build()
        {
            EnsureFolder("Assets/_Project/Art");
            EnsureFolder("Assets/_Project/Art/Materials");
            EnsureFolder(MatRoot);

            int texCount = 0, matCount = 0;
            var lines = new List<string>();
            lines.Add("ABYSS ECHO · S3B 材质库构建");
            lines.Add("来源：ambientCG（CC0，可商用免署名）");
            lines.Add("");

            foreach (var spec in Specs)
            {
                string texDir = TexRoot + "/" + spec.TextureSet;
                if (!AssetDatabase.IsValidFolder(texDir))
                {
                    lines.Add($"[跳过] {spec.Id}：贴图目录不存在 {texDir}");
                    Debug.LogWarning($"[S3B] 缺少贴图目录：{texDir}");
                    continue;
                }

                // 1) 配置所有贴图的导入设置
                var tex = new Dictionary<string, Texture2D>();
                foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { texDir }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    string file = Path.GetFileNameWithoutExtension(path);

                    string role = DetectRole(file);
                    ConfigureTexture(path, role);
                    texCount++;

                    var t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (t != null) tex[role] = t;
                }

                // 2) 创建材质
                string matPath = $"{MatRoot}/{spec.Id}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null)
                {
                    var shader = Shader.Find("Universal Render Pipeline/Lit");
                    if (shader == null)
                    {
                        Debug.LogError("[S3B] 找不到 URP Lit Shader");
                        return;
                    }
                    mat = new Material(shader) { name = spec.Id };
                    AssetDatabase.CreateAsset(mat, matPath);
                }

                // 3) 绑定贴图
                if (tex.TryGetValue("Color", out var colorTex))
                    mat.SetTexture("_BaseMap", colorTex);
                if (tex.TryGetValue("Normal", out var normalTex))
                {
                    mat.SetTexture("_BumpMap", normalTex);
                    mat.EnableKeyword("_NORMALMAP");
                }
                if (tex.TryGetValue("AO", out var aoTex))
                {
                    mat.SetTexture("_OcclusionMap", aoTex);
                    mat.EnableKeyword("_OCCLUSIONMAP");
                }

                // 4) 参数
                // URP Lit 中：_Metallic / _Smoothness 是标量，贴图需走 _MetallicGlossMap（需打包通道）
                // 简化方案：使用贴图平均值或直接指定标量，避免打包通道的复杂度。
                float metallic = spec.MetallicOverride >= 0f
                    ? spec.MetallicOverride
                    : (tex.ContainsKey("Metalness") ? 0.75f : 0.0f);

                mat.SetFloat("_Metallic", metallic);
                mat.SetFloat("_Smoothness", spec.Smoothness);
                mat.SetFloat("_Surface", 0f);          // Opaque
                mat.SetFloat("_SmoothnessTextureChannel", 0f);

                // BaseColor 乘数（S3B 关键修复）：
                // 实测 ambientCG 的 Metal027 贴图本身接近纯黑（RGB≈30/255，亮度 0.12），
                // 在低照度场景里直接显示为全黑。通过提高 _BaseColor 乘数把它提亮到可用范围。
                // URP Lit 允许 _BaseColor 超过 1（HDR），数值 >1 即等比例提亮贴图。
                float boost = spec.Brightness <= 0f ? 1f : spec.Brightness;
                mat.SetColor("_BaseColor", new Color(boost, boost, boost, 1f));

                // 5) Tiling / Offset
                mat.SetTextureScale("_BaseMap", spec.Tiling);
                mat.SetTextureScale("_BumpMap", spec.Tiling);
                mat.SetTextureScale("_OcclusionMap", spec.Tiling);

                EditorUtility.SetDirty(mat);
                matCount++;

                lines.Add($"[完成] {spec.Id,-22} <- {spec.TextureSet,-18} 金属度 {metallic:F2} 光滑度 {spec.Smoothness:F2}  {spec.Note}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            lines.Add("");
            lines.Add($"贴图配置 {texCount} 张，材质生成 {matCount} 个");

            string text = string.Join("\n", lines);
            Debug.Log("<b>[ABYSS ECHO] S3B 材质库构建完成</b>\n" + text);
            WriteReport(text);
        }

        private static string DetectRole(string fileName)
        {
            if (fileName.Contains("Color")) return "Color";
            if (fileName.Contains("Normal")) return "Normal";
            if (fileName.Contains("Roughness")) return "Roughness";
            if (fileName.Contains("Metalness")) return "Metalness";
            if (fileName.Contains("AmbientOcclusion")) return "AO";
            if (fileName.Contains("Displacement")) return "Displacement";
            return "Unknown";
        }

        /// <summary>按贴图用途设置正确的导入参数。</summary>
        private static void ConfigureTexture(string path, string role)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            bool changed = false;

            switch (role)
            {
                case "Color":
                    if (importer.textureType != TextureImporterType.Default) { importer.textureType = TextureImporterType.Default; changed = true; }
                    if (!importer.sRGBTexture) { importer.sRGBTexture = true; changed = true; }
                    if (importer.mipmapEnabled == false) { importer.mipmapEnabled = true; changed = true; }
                    break;

                case "Normal":
                    if (importer.textureType != TextureImporterType.NormalMap) { importer.textureType = TextureImporterType.NormalMap; changed = true; }
                    if (importer.sRGBTexture) { importer.sRGBTexture = false; changed = true; }
                    break;

                case "Roughness":
                case "Metalness":
                case "AO":
                case "Displacement":
                    if (importer.textureType != TextureImporterType.Default) { importer.textureType = TextureImporterType.Default; changed = true; }
                    if (importer.sRGBTexture) { importer.sRGBTexture = false; changed = true; }
                    break;
            }

            // 统一：wrap mode = Repeat（无缝平铺），压缩 = 高质量
            if (importer.wrapMode != TextureWrapMode.Repeat) { importer.wrapMode = TextureWrapMode.Repeat; changed = true; }
            if (importer.textureCompression != TextureImporterCompression.CompressedHQ)
            {
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                changed = true;
            }
            if (importer.maxTextureSize < 2048) { importer.maxTextureSize = 2048; changed = true; }

            if (changed) importer.SaveAndReimport();
        }

        private static void WriteReport(string text)
        {
            try
            {
                string root = Path.GetDirectoryName(Path.GetFullPath(Application.dataPath));
                string path = Path.GetFullPath(Path.Combine(root ?? ".", "../Reports/S3B_MaterialLibrary.txt"));
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, text, new System.Text.UTF8Encoding(false));
                Debug.Log("[S3B] 材质库报告：" + path);
            }
            catch (System.Exception e) { Debug.LogWarning("写报告失败：" + e.Message); }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
