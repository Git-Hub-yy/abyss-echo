using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S69b：第三章「科研实验室」材质分配。
    ///
    /// ══════════════════════ 视觉定位 ══════════════════════
    ///
    ///   冷色科技恐怖。
    ///   色彩：冷蓝 / 银灰 / 青绿 / 旧白 / 少量暗红警示
    ///   材质：不锈钢、磨砂金属、实验室玻璃、透明玻璃、白色陶瓷、
    ///         实验设备外壳、橡胶密封圈、显示器发光、培养舱
    ///
    ///   但不能做成干净明亮的医院 —— 要保留废弃科研设施的破败感，
    ///   以及少量异常感染痕迹。
    ///
    /// ══════════════════════ 分配策略 ══════════════════════
    ///
    ///   建筑按房间类型分：
    ///     F9 祭室   → 特殊（供奉台所在，用暗色 + 有机污染）
    ///     F7 镜沼   → 水 + 镜面，地面用混凝土
    ///     其他实验室 → 冷蓝钢板 + 瓷砖
    ///
    ///   有机污染：少数墙面/地面用 M_Org_Membrane（生物膜）少量点缀，
    ///   体现"异常感染痕迹"。
    /// </summary>
    public static class Ch3MaterialAssignBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter3_Graybox.unity";
        private const string Lib = "Assets/Art/Materials";

        [MenuItem("ABYSS ECHO/美术 v9.0/分配第三章材质")]
        public static void Build() => Run();

        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Log("已打开第三章场景");

            var M = LoadLib();
            Log($"材质库加载 {M.Count} 个");

            // 旧材质映射
            var map = new Dictionary<string, string>
            {
                { "M_Ch3_Metal",   "M_Mach_BrushedSteel" },   // 不锈钢
                { "M_Ch3_Dark",    "M_Mach_DarkPainted" },
                { "M_Ch3_Ceramic", "M_Prop_Ceramic" },        // 陶瓷
                { "M_Ch3_Glass",   "M_Tech_Glass" },          // 实验室玻璃
                { "M_Ch3_Wood",    "M_Prop_Wood" },
                { "M_Ch3_Fabric",  "M_Prop_Fabric" },
                { "M_Ch3_Offering","M_Prop_Ceramic" },        // 供奉台：石/陶瓷
                { "M_Pickup_item_bulb", "M_Tech_GlowCyan" },  // 灯泡本体发光
                { "M_Pickup_item_fuse", "M_Mach_BrushedSteel" },
                { "M_Pickup_item_tape", "M_Prop_Plastic" },
                { "M_Monster_Silhouette", "M_Org_FleshDeep" }, // 怪物 → 有机
            };

            int buildingFixed = 0, replaced = 0, organic = 0;

            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (r == null) continue;
                string name = r.gameObject.name;
                var mats = r.sharedMaterials;
                if (mats == null || mats.Length == 0) continue;

                Material target = null;

                // 1. URP 默认 Lit → 建筑材质
                if (mats[0] != null && mats[0].name == "Lit")
                {
                    target = PickBuilding(name, M, out bool isOrganic);
                    if (target != null) { buildingFixed++; if (isOrganic) organic++; }
                }
                // 2. 旧材质映射
                else if (mats[0] != null && map.TryGetValue(mats[0].name, out var nn))
                {
                    if (M.TryGetValue(nn, out var m2)) target = m2;
                }
                // 3. 按对象名特判
                else if (mats[0] != null)
                {
                    target = PickProp(name, M);
                }

                if (target != null && target != mats[0])
                {
                    for (int i = 0; i < mats.Length; i++) mats[i] = target;
                    r.sharedMaterials = mats;
                    EditorUtility.SetDirty(r);
                    replaced++;
                }
            }

            Log($"建筑修复 {buildingFixed}（其中有机 {organic}），总替换 {replaced}");

            // 关闭所有 Ch3 旧材质的发光
            foreach (var f in Directory.GetFiles("Assets/_Project/Art/Materials/Ch3", "*.mat"))
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
            bool ok = EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Log(ok ? "场景已保存" : "❌ 保存失败");

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

        /// <summary>第三章建筑材质：冷蓝实验室风格 + 少量感染痕迹。</summary>
        private static Material PickBuilding(string name, Dictionary<string, Material> M, out bool isOrganic)
        {
            isOrganic = false;

            // ── 天花板 ──
            if (name.StartsWith("Ceiling")) return M["M_Arch_Ceiling"];

            // ── 地板 ──
            if (name.StartsWith("Floor"))
            {
                // F7 镜沼：水底 → 混凝土（湿）
                if (name.Contains("_F7")) return M["M_Arch_FloorConcrete"];
                // F9 祭室：仪式空间 → 深色混凝土
                if (name.Contains("_F9")) return M["M_Arch_FloorConcrete"];
                // 其他实验室：防滑钢板
                return M["M_Arch_FloorPlate"];
            }

            // ── 墙体 ──
            if (name.StartsWith("North") || name.StartsWith("South") ||
                name.StartsWith("East") || name.StartsWith("West"))
            {
                // F9 祭室：感染区 → 生物膜（这是"异常感染痕迹"）
                if (name.Contains("_F9"))
                {
                    isOrganic = true;
                    return M["M_Org_Membrane"];
                }

                // F7 镜沼：湿润墙面 → 深色钢
                if (name.Contains("_F7")) return M["M_Arch_WallPanel"];

                // 连廊/甬道：小板块瓷砖（狭窄、旧）
                if (name.Contains("_Link")) return M["M_Arch_Tile"];

                // 普通实验室：冷蓝钢板（这是第三章的主色调）
                return M["M_Arch_WallSteel"];
            }

            return M["M_Arch_WallSteel"];
        }

        /// <summary>按对象名给道具/设备挑材质。</summary>
        private static Material PickProp(string name, Dictionary<string, Material> M)
        {
            // 供奉台
            if (name.Contains("供奉") || name.Contains("供桌"))
                return M["M_Prop_Ceramic"];

            // 长明灯座（S65 加的）
            if (name.Contains("灯座"))
                return M["M_Mach_DarkPainted"];

            // 镜沼水面
            if (name == "Water_F7" || name.Contains("Mirror"))
                return M["M_Tech_Glass"];

            // 实验台/操作台
            if (name.Contains("实验台") || name.Contains("操作台"))
                return M["M_Prop_Metal"];

            // 柜子/货架
            if (name.Contains("柜") || name.Contains("货架"))
                return M["M_Mach_DeviceShell"];

            // 床/桌
            if (name.Contains("床") || name.Contains("桌"))
                return M["M_Prop_Wood"];

            // 椅
            if (name.Contains("椅"))
                return M["M_Prop_Wood"];

            // 管道
            if (name.Contains("管"))
                return M["M_Mach_Pipe"];

            // 细金属件
            if (name.Contains("腿") || name.Contains("柱") || name.Contains("栏杆"))
                return M["M_Mach_BrushedSteel"];

            return null;
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
            string line = $"[Ch3Assign] {msg}";
            Debug.Log(line);
            try
            {
                Directory.CreateDirectory(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports");
                File.AppendAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Ch3MaterialAssign_Result.txt",
                                   line + "\n", new UTF8Encoding(false));
            }
            catch { }
        }
    }
}