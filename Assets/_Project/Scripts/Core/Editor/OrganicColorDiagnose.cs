using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S76a：第五章有机材质**当前视觉参数诊断**（只读）。
    ///
    /// 玩家反馈：修复 UV 后纹理清楚了，但颜色像"荧光塑料"，太难看。
    ///
    /// 怀疑原因：S69 为了在黑暗场景里看得见，
    /// 把 _BaseColor 乘了 3.8~4.8 倍，导致过亮饱和。
    ///
    /// 本工具输出：
    ///   · 每个有机材质的 _BaseColor 完整 RGBA
    ///   · 实际亮度（maxColorComponent）
    ///   · 颜色饱和度（HSV 的 S 分量）
    ///   · 贴图的实际平均颜色
    ///   · 材质最终呈现色 = BaseColor × 贴图平均色
    ///
    /// 只读。
    /// </summary>
    public static class OrganicColorDiagnose
    {
        private static readonly string[] Mats =
        {
            "Assets/Art/Materials/Organic/M_Org_FleshWet.mat",
            "Assets/Art/Materials/Organic/M_Org_FleshDeep.mat",
            "Assets/Art/Materials/Organic/M_Org_Membrane.mat",
            "Assets/Art/Materials/Organic/M_Org_SlimeBio.mat",
            "Assets/Art/Materials/Organic/M_Org_SlimeBioSmall.mat",
            "Assets/Art/Materials/Organic/M_Org_DarkSkin.mat",
        };

        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            sb.AppendLine("  有机材质颜色诊断（只读）");
            sb.AppendLine("═══════════════════════════════════════════════════════════════");

            sb.AppendLine();
            sb.AppendLine($"{"材质",-24} {"_BaseColor (RGBA)",-30} {"亮度",-8} {"饱和度",-8} {"Tiling",-8} {"贴图平均色",-22} 最终色");
            sb.AppendLine(new string('-', 140));

            foreach (var p in Mats)
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (m == null) { sb.AppendLine($"{Path.GetFileNameWithoutExtension(p),-24} ❌ 不存在"); continue; }

                Color bc = m.GetColor("_BaseColor");
                float bright = bc.maxColorComponent;
                Color.RGBToHSV(bc, out float h, out float s, out float v);

                var tex = m.GetTexture("_BaseMap") as Texture2D;
                string texAvg = "无";
                Color avgCol = Color.white;

                if (tex != null)
                {
                    avgCol = AverageColor(tex);
                    texAvg = $"({avgCol.r:F2},{avgCol.g:F2},{avgCol.b:F2})";
                }

                Color final = new Color(bc.r * avgCol.r, bc.g * avgCol.g, bc.b * avgCol.b);
                Color.RGBToHSV(final, out float fh, out float fs, out float fv);

                sb.AppendLine($"{m.name,-24} " +
                              $"({bc.r:F2},{bc.g:F2},{bc.b:F2},{bc.a:F1})".PadRight(30) +
                              $"{bright,-8:F2} {s,-8:F2} {m.GetTextureScale("_BaseMap").x,-8:F2} " +
                              $"{texAvg,-22} ({final.r:F2},{final.g:F2},{final.b:F2})");

                // 补充：Smoothness 和 Metallic
                sb.AppendLine($"{"",-24}   Smoothness={m.GetFloat("_Smoothness"):F2}  " +
                              $"Metallic={m.GetFloat("_Metallic"):F2}  " +
                              $"Emission={m.GetColor("_EmissionColor").maxColorComponent:F2}  " +
                              $"贴图={tex?.name ?? "无"}");
            }

            // 对比：金属/建筑材质的颜色，作为"正常"参考
            sb.AppendLine();
            sb.AppendLine("──── 参考：金属/建筑材质（正常亮度）────");
            string[] refMats =
            {
                "Assets/Art/Materials/Architecture/M_Arch_WallSteel.mat",
                "Assets/Art/Materials/Architecture/M_Arch_Ceiling.mat",
                "Assets/Art/Materials/Machinery/M_Mach_WornMetal.mat",
            };
            foreach (var p in refMats)
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (m == null) continue;
                Color bc = m.GetColor("_BaseColor");
                sb.AppendLine($"  {m.name,-24} _BaseColor=({bc.r:F2},{bc.g:F2},{bc.b:F2})  亮度={bc.maxColorComponent:F2}");
            }

            string text = sb.ToString();
            Debug.Log(text);
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\OrganicColorDiagnose.txt",
                              text, new UTF8Encoding(false));
        }

        private static Color AverageColor(Texture2D tex)
        {
            try
            {
                var px = tex.GetPixels();
                double r = 0, g = 0, b = 0;
                int step = Mathf.Max(1, px.Length / 4000);
                int n = 0;
                for (int i = 0; i < px.Length; i += step)
                {
                    r += px[i].r; g += px[i].g; b += px[i].b;
                    n++;
                }
                return new Color((float)(r / n), (float)(g / n), (float)(b / n));
            }
            catch { return Color.white; }
        }
    }
}