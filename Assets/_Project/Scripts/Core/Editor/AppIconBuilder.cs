using System.IO;
using UnityEditor;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// 程序化生成应用图标（深潜风格）。
    ///
    /// 设计：深蓝黑背景 + 中央声波/深渊涡旋的同心环 + 一个亮点。
    /// 全代码绘制，无需外部图片，符合"不伪造素材"的要求。
    ///
    /// 生成 256×256 PNG 并设置为 PlayerSettings 图标。
    /// </summary>
    public static class AppIconBuilder
    {
        private const int Size = 256;
        private const string IconPath = "Assets/_Project/Art/AppIcon/app_icon.png";

        [MenuItem("ABYSS ECHO/S9 生成应用图标")]
        public static void Build()
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var px = new Color[Size * Size];

            float cx = Size * 0.5f, cy = Size * 0.5f;

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float dx = (x - cx) / (Size * 0.5f);
                    float dy = (y - cy) / (Size * 0.5f);
                    float r = Mathf.Sqrt(dx * dx + dy * dy);

                    // 基础背景：从中心到边缘的径向渐变（深海光衰减）
                    Color bg = Color.Lerp(
                        new Color(0.10f, 0.20f, 0.30f),
                        new Color(0.02f, 0.04f, 0.07f),
                        Mathf.Clamp01(r));

                    // 回声波纹：间距递增、强度递减，模拟声波在深水中扩散衰减
                    //   （等距等亮会像靶子，不等距才有"扩散"感）
                    float ring = 0f;
                    float[] ringRadius = { 0.26f, 0.48f, 0.74f, 1.02f };
                    float[] ringWidth = { 0.020f, 0.016f, 0.013f, 0.010f };
                    float[] ringPower = { 1.00f, 0.62f, 0.34f, 0.15f };

                    for (int i = 0; i < ringRadius.Length; i++)
                    {
                        float d = Mathf.Abs(r - ringRadius[i]);
                        if (d < ringWidth[i])
                        {
                            float k = 1f - d / ringWidth[i];
                            ring += k * k * ringPower[i];   // 平方衰减，边缘更柔
                        }
                    }

                    // 波纹颜色：内圈偏白青，外圈偏深蓝
                    float ringDepth = Mathf.Clamp01(r / 1.05f);
                    Color ringColor = Color.Lerp(
                        new Color(0.62f, 0.90f, 1.00f),
                        new Color(0.16f, 0.40f, 0.72f),
                        ringDepth);

                    Color col = bg + ringColor * ring * 0.72f;

                    // 中心亮点（光源）
                    if (r < 0.10f)
                    {
                        float core = 1f - r / 0.10f;
                        col += new Color(0.85f, 0.95f, 1f) * core * core * 0.9f;
                    }

                    // 圆形遮罩（图标用圆角/圆形更现代）
                    float maskR = 1.0f;
                    float edge = Mathf.Clamp01((maskR - r) / 0.06f);
                    float alpha = Mathf.Clamp01(edge);

                    // 四角外完全透明
                    col.a = alpha;
                    if (alpha < 0.02f) col = new Color(0, 0, 0, 0);

                    px[y * Size + x] = col;
                }
            }

            tex.SetPixels(px);
            tex.Apply();

            // 保存 PNG
            EnsureFolder("Assets/_Project/Art/AppIcon");
            string full = Path.GetFullPath(IconPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllBytes(full, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(IconPath, ImportAssetOptions.ForceUpdate);

            // 配置为纹理（sprite，启用 alpha）
            var importer = AssetImporter.GetAtPath(IconPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = 256;
                importer.SaveAndReimport();
            }

            // 设置为应用图标
            var sprite = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (sprite != null)
            {
                PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Standalone,
                    new Texture2D[] { sprite });
                Debug.Log("<b>[AppIcon]</b> 应用图标已生成并设置\n  " + IconPath);
            }
            else
            {
                Debug.LogError("[AppIcon] 图标加载失败");
            }

            AssetDatabase.SaveAssets();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int idx = path.LastIndexOf('/');
            string parent = path.Substring(0, idx);
            string leaf = path.Substring(idx + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}