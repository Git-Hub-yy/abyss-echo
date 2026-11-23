using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S69e：第五章「深海核心区」材质分配。
    ///
    /// ══════════════════════ 视觉定位 ══════════════════════
    ///
    ///   生物机械恐怖。
    ///   色彩：黑绿 / 暗红 / 深紫 / 暗灰 / 微弱青绿生物发光
    ///   材质：**真正的湿润有机组织**、生物膜、黏液、血肉纹理、
    ///         生物脉络、感染金属、生物机械混合、异常发光组织
    ///
    /// ══════════════════════ 关键要求 ══════════════════════
    ///
    ///   任务明确："不允许简单使用混凝土贴图加深色来模拟血肉"
    ///
    ///   本批使用**程序生成的真正有机 PBR 贴图**：
    ///     FleshWet / Membrane / SlimeBio（1024²，含 Color/Normal/Rough/AO）
    ///   生成脚本：Scripts_Backup/gen_organic_v3.py
    ///
    /// ══════════════════════ 分配策略 ══════════════════════
    ///
    ///   第五章是全生物章节，所以有机材质占比可以更高（60~70%），
    ///   但仍要保留「生物机械」的对比 —— 部分结构是金属/管道。
    /// </summary>
    public static class Ch5MaterialAssignBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter5_Graybox.unity";
        private const string Lib = "Assets/Art/Materials";

        [MenuItem("ABYSS ECHO/美术 v9.0/分配第五章材质")]
        public static void Build() => Run();

        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Log("已打开第五章场景");

            var M = LoadLib();
            Log($"材质库加载 {M.Count} 个");

            var map = new Dictionary<string, string>
            {
                { "M_Ch5_Metal",     "M_Org_FleshDeep" },      // 感染金属 → 血肉覆盖
                { "M_Ch5_Rust",      "M_Mach_WornMetal" },     // 锈蚀保持金属（生物机械对比）
                { "M_Ch5_Bone",      "M_Org_Membrane" },       // 骨 → 生物膜
                { "M_EvacConsole",   "M_Mach_DeviceShell" },   // 撤离台：机械
                { "M_EvacScreen",    "M_Tech_ScreenBlue" },    // 屏幕：发光
                { "M_Monster_Silhouette", "M_Org_FleshDeep" },
            };

            int buildingFixed = 0, replaced = 0, organic = 0;

            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (r == null) continue;
                string name = r.gameObject.name;
                var mats = r.sharedMaterials;
                if (mats == null || mats.Length == 0) continue;

                Material target = null;

                if (mats[0] != null && mats[0].name == "Lit")
                {
                    target = PickBuilding(name, M, out bool isOrg);
                    if (target != null) { buildingFixed++; if (isOrg) organic++; }
                }
                else if (mats[0] != null && map.TryGetValue(mats[0].name, out var nn))
                {
                    if (M.TryGetValue(nn, out var m2)) target = m2;
                }

                if (target != null && target != mats[0])
                {
                    for (int i = 0; i < mats.Length; i++) mats[i] = target;
                    r.sharedMaterials = mats;
                    EditorUtility.SetDirty(r);
                    replaced++;
                }
            }

            Log($"建筑修复 {buildingFixed}（有机 {organic}），总替换 {replaced}");

            foreach (var f in Directory.GetFiles("Assets/_Project/Art/Materials/Ch5", "*.mat"))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(f.Replace("\\", "/"));
                if (m == null) continue;
                if (m.IsKeywordEnabled("_EMISSION"))
                {
                    m.DisableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", Color.black);
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                    EditorUtility.SetDirty(m);
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Log("场景已保存");

            Verify();
        }

        private static Dictionary<string, Material> LoadLib()
        {
            var d = new Dictionary<string, Material>();
            foreach (var n in new[]
            {
                "Architecture/M_Arch_WallSteel", "Architecture/M_Arch_WallPanel",
                "Architecture/M_Arch_FloorPlate", "Architecture/M_Arch_FloorConcrete",
                "Architecture/M_Arch_Ceiling", "Architecture/M_Arch_Tile",
                "Architecture/M_Arch_BlastDoor",
                "Machinery/M_Mach_BrushedSteel", "Machinery/M_Mach_DarkPainted",
                "Machinery/M_Mach_Rubber", "Machinery/M_Mach_Pipe",
                "Machinery/M_Mach_WornMetal", "Machinery/M_Mach_DeviceShell",
                "Technology/M_Tech_ScreenBlue", "Technology/M_Tech_GlowCyan",
                "Technology/M_Tech_GlowRed", "Technology/M_Tech_IndicatorGreen",
                "Technology/M_Tech_Glass", "Technology/M_Tech_GlassFrosted",
                "Props/M_Prop_Metal", "Props/M_Prop_Plastic", "Props/M_Prop_Ceramic",
                "Props/M_Prop_Wood", "Props/M_Prop_Fabric", "Props/M_Prop_Crate",
                "Props/M_Prop_Dark", "Props/M_Prop_White", "Props/M_Prop_HazardStripe",
                "Organic/M_Org_FleshWet", "Organic/M_Org_FleshDeep",
                "Organic/M_Org_Membrane", "Organic/M_Org_SlimeBio",
                "Organic/M_Org_SlimeBioSmall",
            })
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>($"{Lib}/{n}.mat");
                if (m != null) d[Path.GetFileName(n)] = m;
            }
            return d;
        }

        /// <summary>第五章建筑：生物机械混合。</summary>
        private static Material PickBuilding(string name, Dictionary<string, Material> M, out bool isOrganic)
        {
            isOrganic = false;

            // 天花板：生物膜（像肉做的顶棚）
            if (name.StartsWith("Ceiling"))
            {
                isOrganic = true;
                return M["M_Org_Membrane"];
            }

            // 地板：防滑钢板（生物机械的地面基座）
            if (name.StartsWith("Floor"))
            {
                if (name.Contains("Link")) return M["M_Arch_FloorConcrete"];
                return M["M_Arch_FloorPlate"];
            }

            // 墙体：用哈希做"感染程度"分布
            //   → 约 60% 有机，40% 金属，形成生物机械混合
            if (name.StartsWith("North") || name.StartsWith("South") ||
                name.StartsWith("East") || name.StartsWith("West"))
            {
                int h = Mathf.Abs(name.GetHashCode() % 10);

                if (h < 3)
                {
                    isOrganic = true;
                    return M["M_Org_FleshWet"];      // 裸露血肉
                }
                if (h < 6)
                {
                    isOrganic = true;
                    return M["M_Org_Membrane"];      // 生物膜覆盖
                }
                if (h < 7)
                {
                    isOrganic = true;
                    return M["M_Org_SlimeBio"];      // 黏液浸润
                }

                // 其余保持金属（感染中、尚未完全吞噬）
                return M["M_Mach_WornMetal"];
            }

            return M["M_Org_Membrane"];
        }

        private static void Verify()
        {
            Log("---- 验证 ----");
            var renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            var stat = new Dictionary<string, int>();
            var tex = new Dictionary<string, bool>();
            int nulls = 0, pink = 0;

            foreach (var r in renderers)
            {
                if (r == null) continue;
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null) { nulls++; continue; }
                    if (!stat.ContainsKey(m.name)) stat[m.name] = 0;
                    stat[m.name]++;
                    if (!tex.ContainsKey(m.name)) tex[m.name] = m.GetTexture("_BaseMap") != null;
                    if (m.shader != null && m.shader.name.Contains("Error")) pink++;
                }
            }

            Log($"Renderer={renderers.Length}  空槽={nulls}  错误shader={pink}  材质种类={stat.Count}");
            var keys = new List<string>(stat.Keys);
            keys.Sort((a, b) => stat[b].CompareTo(stat[a]));
            int withTex = 0;
            foreach (var k in keys)
            {
                if (tex[k]) withTex++;
                Log($"  {k,-26} {stat[k],4} 次  贴图={(tex[k] ? "✓" : "-")}");
            }
            Log($"有贴图 {withTex}/{stat.Count}");
        }

        private static void Log(string msg)
        {
            string line = $"[Ch5Assign] {msg}";
            Debug.Log(line);
            try
            {
                Directory.CreateDirectory(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports");
                File.AppendAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Ch5MaterialAssign_Result.txt",
                                   line + "\n", new UTF8Encoding(false));
            }
            catch { }
        }
    }
}