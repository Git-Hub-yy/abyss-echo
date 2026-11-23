using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S69d：第四章「水下维护区」材质分配。
    ///
    /// ══════════════════════ 视觉定位 ══════════════════════
    ///
    ///   重工业、潮湿、压迫感。
    ///   色彩：深枪灰 / 暗蓝 / 锈红 / 暗黄 / 少量应急红光
    ///   材质：锈蚀钢板、粗糙金属、防滑钢板、混凝土、
    ///         橡胶、管道金属、油污水渍、警示条纹、工业设备外壳
    ///
    ///   必须有明显的重工业质感，不能沿用第三章的实验室风格。
    ///
    /// ══════════════════════ 分配策略 ══════════════════════
    ///
    ///   与第三章的区别：
    ///     第三章用 M_Arch_WallSteel（冷蓝钢板）
    ///     第四章改用 M_Mach_WornMetal（锈蚀） + M_Arch_FloorPlate（防滑）
    ///
    ///   G7 心腔（生物区）→ 有机材质
    ///   其他区域 → 重工业金属
    /// </summary>
    public static class Ch4MaterialAssignBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter4_Graybox.unity";
        private const string Lib = "Assets/Art/Materials";

        [MenuItem("ABYSS ECHO/美术 v9.0/分配第四章材质")]
        public static void Build() => Run();

        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Log("已打开第四章场景");

            var M = LoadLib();
            Log($"材质库加载 {M.Count} 个");

            var map = new Dictionary<string, string>
            {
                // 第四章原有材质（全部纯色）
                { "M_Ch4_Metal",     "M_Mach_WornMetal" },      // 锈蚀工业金属
                { "M_Ch4_Rust",      "M_Mach_WornMetal" },
                { "M_Ch4_Bone",      "M_Arch_FloorConcrete" },  // 骨/结石 → 混凝土
                { "M_Ch4_Flesh",     "M_Org_FleshDeep" },        // 血肉 → 最暗版本（第四章红光强，不能再亮）
                { "M_Ch4_FleshDark", "M_Org_FleshDeep" },
                { "M_Ch4_Vein",      "M_Org_Membrane" },        // 血管/膜 → 生物膜
                { "M_HeartGate",     "M_Org_FleshDeep" },
                { "M_HeartValve",    "M_Org_SlimeBio" },
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
                    // ⚠️ 大面积的走廊/墙体不该用血肉（会被 UV 拉伸成红条纹）
                    //   这些物体名以 Corridor_/Transit_ 开头，尺寸长达 17 米
                    if ((name.StartsWith("Corridor_") || name.StartsWith("Transit_"))
                        && (nn.StartsWith("M_Org_")))
                    {
                        target = name.Contains("Ceiling")
                            ? M["M_Arch_Ceiling"]
                            : M["M_Mach_WornMetal"];
                        Log($"  {name} → 工业金属（避免大面积血肉拉伸）");
                    }
                    else if (M.TryGetValue(nn, out var m2))
                    {
                        target = m2;
                    }
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

            foreach (var f in Directory.GetFiles("Assets/_Project/Art/Materials/Ch4", "*.mat"))
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
                "Props/M_Prop_NotePaper",
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

        /// <summary>第四章建筑：重工业 + G7 心腔为生物区。</summary>
        private static Material PickBuilding(string name, Dictionary<string, Material> M, out bool isOrganic)
        {
            isOrganic = false;

            // ── G7 心腔：重工业外壳 + 局部生物组织 ──
            //
            //   第四章定位是「重工业、锈蚀、枪灰」，不应整片血肉。
            //   G7 作为「心腔」保留生物质感，但**只用在少数墙上**，
            //   其余仍是锈蚀金属 —— 这样才有「工业设施被生物侵蚀」的对比。
            if (name.Contains("_G7") || name.Contains("G7_"))
            {
                if (name.StartsWith("Floor")) return M["M_Arch_FloorPlate"];
                if (name.StartsWith("Ceiling")) return M["M_Arch_Ceiling"];

                // 用名字哈希决定哪面墙是生物组织（约 1/3）
                int h = Mathf.Abs(name.GetHashCode() % 4);
                if (h == 0)
                {
                    isOrganic = true;
                    return M["M_Org_Membrane"];      // 生物膜（紫黑，在红光下不刺眼）
                }
                return M["M_Mach_WornMetal"];         // 其余仍是锈蚀钢
            }

            // ── 天花板 ──
            if (name.StartsWith("Ceiling")) return M["M_Arch_Ceiling"];

            // ── 地板 ──
            if (name.StartsWith("Floor"))
            {
                // 连廊：混凝土
                if (name.Contains("Link")) return M["M_Arch_FloorConcrete"];
                // 其他：防滑钢板（重工业标准）
                return M["M_Arch_FloorPlate"];
            }

            // ── 墙体 ──
            if (name.StartsWith("North") || name.StartsWith("South") ||
                name.StartsWith("East") || name.StartsWith("West"))
            {
                // 连廊：管道暗钢
                if (name.Contains("Link")) return M["M_Mach_DarkPainted"];

                // 主区域：锈蚀工业钢板（第四章的核心质感）
                return M["M_Mach_WornMetal"];
            }

            return M["M_Mach_WornMetal"];
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
            string line = $"[Ch4Assign] {msg}";
            Debug.Log(line);
            try
            {
                Directory.CreateDirectory(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports");
                File.AppendAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Ch4MaterialAssign_Result.txt",
                                   line + "\n", new UTF8Encoding(false));
            }
            catch { }
        }
    }
}