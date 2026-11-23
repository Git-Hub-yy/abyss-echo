using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S69c：有机材质亮度修正。
    ///
    /// ══════════════════════ 问题 ══════════════════════
    ///
    ///   第三章截图实测：有机材质特写平均亮度仅 0.069。
    ///   原因：
    ///     ① 贴图本身极暗（Membrane 平均亮度 0.061，为了"深海黑紫"）
    ///     ② 第三章场景光照本来偏暗（偏恐怖氛围）
    ///     两者相乘 → 几乎全黑，看不出材质。
    ///
    /// ══════════════════════ 修正 ══════════════════════
    ///
    ///   用 _BaseColor 作为提亮倍率（URP/Lit 里 BaseColor 会和 BaseMap 相乘）。
    ///   贴图保持暗，运行时乘一个 >1 的系数 → 在暗场景里能看清，
    ///   同时不改变贴图本身的色彩关系。
    ///
    ///   注意：BaseColor 的 alpha 必须保持 1，否则透明。
    /// </summary>
    public static class OrganicBrightnessBuilder
    {
        private const string Lib = "Assets/Art/Materials/Organic";

        [MenuItem("ABYSS ECHO/美术馆/有机材质提亮")]
        public static void Build() => Run();

        public static void Run()
        {
            Log("══════ 有机材质提亮 ══════");

            // 材质名 → (提亮倍率, 色调微调)
            var cfg = new Dictionary<string, (float mul, Color tint)>
            {
                { "M_Org_FleshWet",      (3.8f, new Color(1.00f, 0.94f, 0.94f)) },
                { "M_Org_FleshDeep",     (2.8f, new Color(0.94f, 0.90f, 0.92f)) },
                { "M_Org_Membrane",      (4.5f, new Color(1.04f, 0.96f, 1.08f)) },
                { "M_Org_SlimeBio",      (4.8f, new Color(0.96f, 1.06f, 0.96f)) },
                { "M_Org_SlimeBioSmall", (4.8f, new Color(0.96f, 1.06f, 0.96f)) },
            };

            foreach (var kv in cfg)
            {
                string path = $"{Lib}/{kv.Key}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null) { Log($"  ⚠️ 缺 {kv.Key}"); continue; }

                var c = kv.Value.tint * kv.Value.mul;
                c.a = 1f;
                m.SetColor("_BaseColor", c);

                // 提亮后粗糙度稍微提高一点，避免过曝发亮
                float smooth = m.GetFloat("_Smoothness");
                m.SetFloat("_Smoothness", Mathf.Clamp01(smooth * 0.92f));

                EditorUtility.SetDirty(m);
                Log($"  {kv.Key,-22} BaseColor=({c.r:F2},{c.g:F2},{c.b:F2}) smooth={m.GetFloat("_Smoothness"):F2}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Log("完成");
        }

        private static void Log(string msg)
        {
            string line = $"[OrgBright] {msg}";
            Debug.Log(line);
            try
            {
                Directory.CreateDirectory(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports");
                File.AppendAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\OrganicBrightness_Result.txt",
                                   line + "\n", new UTF8Encoding(false));
            }
            catch { }
        }
    }
}