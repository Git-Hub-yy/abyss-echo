using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S67c：收尾修正。
    ///
    /// 审计中发现两个材质参数不合理：
    ///   M_Note_Paper        有 _EMISSION 且强度 2.2 → 纸张像灯泡一样发光
    ///   M_Pickup_item_fuse  有 _EMISSION 且强度 2.2 → 保险丝像灯泡一样发光
    ///
    /// 都是早期用"改 EmissionColor 让物品显眼"的偷懒做法，
    /// 在写实风格下必须关掉（真实纸张和保险丝不发光）。
    ///
    /// 同时给 M_Pickup_item_fuse 接上 PBR 贴图（之前是纯色）。
    /// </summary>
    public static class Ch1MaterialPolishBuilder
    {
        private const string Old = "Assets/_Project/Art/Materials";
        private const string Lib = "Assets/Art/Materials";

        [MenuItem("ABYSS ECHO/美术 v8.0/第一章材质收尾修正")]
        public static void Build() => Run();

        public static void Run()
        {
            Log("══════ 材质收尾修正 ══════");

            // ① 纸张：关掉发光，接贴图
            var paper = AssetDatabase.LoadAssetAtPath<Material>($"{Old}/Interactable/M_Note_Paper.mat");
            var paperLib = AssetDatabase.LoadAssetAtPath<Material>($"{Lib}/Props/M_Prop_NotePaper.mat");
            if (paper != null)
            {
                if (paperLib != null)
                {
                    CopyFrom(paperLib, paper);
                    Log("  M_Note_Paper  ←  M_Prop_NotePaper（接贴图）");
                }
                paper.DisableKeyword("_EMISSION");
                paper.SetColor("_EmissionColor", Color.black);
                paper.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                EditorUtility.SetDirty(paper);
                Log("  M_Note_Paper  关闭发光（纸张不该自发光）");
            }
            else Log("  ⚠️ 找不到 M_Note_Paper");

            // ② 保险丝：关掉发光，接金属材质
            var fuse = AssetDatabase.LoadAssetAtPath<Material>($"{Old}/Interactable/M_Pickup_item_fuse.mat");
            var fuseLib = AssetDatabase.LoadAssetAtPath<Material>($"{Lib}/Machinery/M_Mach_BrushedSteel.mat");
            if (fuse != null)
            {
                if (fuseLib != null)
                {
                    CopyFrom(fuseLib, fuse);
                    // 保险丝是黄铜色，覆盖 BaseColor
                    fuse.SetColor("_BaseColor", new Color(0.72f, 0.52f, 0.18f));
                    Log("  M_Pickup_item_fuse  ←  BrushedSteel + 黄铜色（接贴图）");
                }
                fuse.DisableKeyword("_EMISSION");
                fuse.SetColor("_EmissionColor", Color.black);
                fuse.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                EditorUtility.SetDirty(fuse);
                Log("  M_Pickup_item_fuse  关闭发光");
            }
            else Log("  ⚠️ 找不到 M_Pickup_item_fuse");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Verify();
        }

        private static void CopyFrom(Material src, Material dst)
        {
            dst.shader = src.shader;
            foreach (var p in new[] { "_BaseMap", "_BumpMap", "_OcclusionMap" })
            {
                var t = src.GetTexture(p);
                if (t != null) dst.SetTexture(p, t);
                dst.SetTextureScale(p, src.GetTextureScale(p));
            }
            dst.SetColor("_BaseColor", src.GetColor("_BaseColor"));
            dst.SetFloat("_Metallic", src.GetFloat("_Metallic"));
            dst.SetFloat("_Smoothness", src.GetFloat("_Smoothness"));
            dst.SetFloat("_BumpScale", src.GetFloat("_BumpScale"));
            dst.SetFloat("_OcclusionStrength", src.GetFloat("_OcclusionStrength"));
            if (src.IsKeywordEnabled("_NORMALMAP")) dst.EnableKeyword("_NORMALMAP");
            dst.SetFloat("_Surface", 0f);
            dst.renderQueue = -1;
        }

        private static void Verify()
        {
            Log("---- 验证：场景中还有没有不该发光的材质 ----");
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity",
                UnityEditor.SceneManagement.OpenSceneMode.Single);

            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (r == null) continue;
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null || !seen.Add(m.name)) continue;
                    bool emit = m.IsKeywordEnabled("_EMISSION") &&
                                m.GetColor("_EmissionColor").maxColorComponent > 0.01f;
                    bool tex = m.GetTexture("_BaseMap") != null;
                    Log($"  {m.name,-28} 贴图={(tex ? "✓" : "-")} 发光={(emit ? $"✓({m.GetColor("_EmissionColor").maxColorComponent:F1})" : "-")}");
                }
            }
        }

        private static void Log(string msg)
        {
            string line = $"[Ch1Polish] {msg}";
            Debug.Log(line);
            try
            {
                Directory.CreateDirectory(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports");
                File.AppendAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Ch1MaterialPolish_Result.txt",
                                   line + "\n", new UTF8Encoding(false));
            }
            catch { }
        }
    }
}