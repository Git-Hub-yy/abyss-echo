using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S68：第二章材质分配。
    ///
    /// ══════════════════════ 审计发现 ══════════════════════
    ///
    ///   第二章 270 个 Renderer：
    ///     · 198 个用 **URP 包默认 Lit.mat**（纯白、无贴图）—— 建筑全部
    ///     ·  57 个用 7 个 Ch2 专用材质（全部纯色 + 全部开了发光）
    ///     ·  15 个用 Interactable 材质（第一章已处理过一部分）
    ///
    ///   整个第二章只有 1 个材质有贴图。
    ///
    /// ══════════════════════ 分配原则 ══════════════════════
    ///
    ///   ① 建筑（墙/地/顶）→ 按房间类型选材质，不是全部同一种
    ///   ② 生活区（D5）→ 加旧白、灰、暗黄的辅助色（任务要求）
    ///   ③ 家具按材质属性分配：金属柜 → 金属，木桌 → 木，锈桶 → 锈蚀
    ///   ④ 交互物复用第一章已处理的同名材质
    ///   ⑤ 全部复用 Assets/Art/Materials 下的 32 个材质，**不新建**
    ///
    /// ══════════════════════ 为什么按对象名判断 ══════════════════════
    ///
    ///   第二章的建筑物体名字里带房间 ID（如 Ceiling_D2、Floor_E1、
    ///   North_L3_Seg0），可以直接从名字判断房间和构件类型，
    ///   比按材质反推更准确。
    /// </summary>
    public static class Ch2MaterialAssignBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter2_Graybox.unity";
        private const string Lib = "Assets/Art/Materials";

        [MenuItem("ABYSS ECHO/美术 v8.2/分配第二章材质")]
        public static void Build() => Run();

        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Log("已打开第二章场景");

            // ══════════════════ 加载材质库 ══════════════════
            var M = new Dictionary<string, Material>();
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
                "Organic/M_Org_DarkSkin",
            })
            {
                string key = Path.GetFileName(n);
                var mat = AssetDatabase.LoadAssetAtPath<Material>($"{Lib}/{n}.mat");
                if (mat != null) M[key] = mat;
                else Log($"  ⚠️ 材质库缺少 {n}");
            }
            Log($"材质库加载 {M.Count} 个");

            // ══════════════════ 旧材质 → 新材质映射 ══════════════════
            var map = new Dictionary<string, string>();

            // Ch2 专用（全部纯色 → 换成有贴图的）
            map["M_Ch2_Metal"] = "M_Prop_Metal";
            map["M_Ch2_Dark"] = "M_Mach_DarkPainted";
            map["M_Ch2_Crate"] = "M_Prop_Crate";
            map["M_Ch2_Rust"] = "M_Mach_WornMetal";
            map["M_Ch2_Glass"] = "M_Tech_Glass";
            map["M_Pickup_item_glove"] = "M_Prop_Fabric";
            map["M_Pickup_item_tape"] = "M_Prop_Plastic";

            // Interactable（第一章已处理过的沿用）
            map["M_Door_Deep"] = "M_Arch_BlastDoor";
            map["M_Console_Desk"] = "M_Mach_DeviceShell";
            map["M_Console_Leg"] = "M_Mach_DarkPainted";
            map["M_Console_Screen"] = "M_Tech_ScreenBlue";
            map["M_Pump_Body"] = "M_Mach_BrushedSteel";
            map["M_Pump_Lamp"] = "M_Tech_GlowCyan";
            map["M_Pump_Panel"] = "M_Tech_ScreenBlue";
            map["M_Tape_Body"] = "M_Prop_Plastic";
            map["M_Tape_Lamp"] = "M_Tech_IndicatorGreen";
            map["M_Water_D2"] = "M_Tech_Glass";
            map["M_Monster_Silhouette"] = "M_Org_DarkSkin";
            // M_Note_Paper 已经是 M_Prop_NotePaper 的内容，不用换

            int replaced = 0, buildingFixed = 0;

            // ══════════════════ 遍历场景 ══════════════════
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (r == null) continue;
                string name = r.gameObject.name;
                var mats = r.sharedMaterials;
                if (mats == null || mats.Length == 0) continue;

                Material newMat = null;

                // ── 1. URP 默认 Lit.mat → 按构件类型分配建筑材质 ──
                if (mats[0] != null && mats[0].name == "Lit")
                {
                    newMat = PickBuildingMaterial(name, M);
                    if (newMat != null) buildingFixed++;
                }
                // ── 2. 旧材质映射 ──
                else if (mats[0] != null && map.TryGetValue(mats[0].name, out var newName))
                {
                    if (M.TryGetValue(newName, out var nm)) newMat = nm;
                }
                // ── 3. 家具/道具的特殊处理（按对象名）──
                else if (mats[0] != null)
                {
                    newMat = PickPropMaterial(name, mats[0].name, M);
                }

                if (newMat != null && newMat != mats[0])
                {
                    for (int i = 0; i < mats.Length; i++) mats[i] = newMat;
                    r.sharedMaterials = mats;
                    EditorUtility.SetDirty(r);
                    replaced++;
                }
            }

            Log($"建筑材质修复 {buildingFixed} 个，总替换 {replaced} 个");

            // ══════════════════ 关闭多余发光 ══════════════════
            //
            //  第二章 7 个 Ch2 材质全开了 _EMISSION，是"让物体显眼"的偷懒做法。
            //  它们已经不再被引用，但为保险起见仍然关掉。
            foreach (var f in Directory.GetFiles("Assets/_Project/Art/Materials/Ch2", "*.mat"))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(f.Replace("\\", "/"));
                if (m == null) continue;
                if (m.IsKeywordEnabled("_EMISSION"))
                {
                    m.DisableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", Color.black);
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                    EditorUtility.SetDirty(m);
                    Log($"  关闭发光: {m.name}");
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            bool ok = EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Log(ok ? "场景已保存" : "❌ 保存失败");

            Verify();
        }

        /// <summary>按构件类型 + 房间选择建筑材质。</summary>
        private static Material PickBuildingMaterial(string name, Dictionary<string, Material> M)
        {
            // 天花板
            if (name.StartsWith("Ceiling")) return M["M_Arch_Ceiling"];

            // 地板
            if (name.StartsWith("Floor"))
            {
                // D2 是被水淹的大厅 → 防滑钢板（湿）
                if (name.Contains("_D2")) return M["M_Arch_FloorPlate"];
                // E1 声学实验室 → 混凝土（吸音地面）
                if (name.Contains("_E1")) return M["M_Arch_FloorConcrete"];
                // D5 生活区 → 混凝土（旧）
                if (name.Contains("_D5")) return M["M_Arch_FloorConcrete"];
                // 其他 → 防滑钢板
                return M["M_Arch_FloorPlate"];
            }

            // 墙体
            if (name.StartsWith("North") || name.StartsWith("South") ||
                name.StartsWith("East") || name.StartsWith("West"))
            {
                // 甬道（L区）用小板的 Tile 材质，显得狭窄老旧
                if (name.Contains("_L1") || name.Contains("_L2") || name.Contains("_L3") ||
                    name.Contains("_L4") || name.Contains("_L5") || name.Contains("_L7") ||
                    name.Contains("_L8") || name.Contains("_L9") || name.Contains("_L10"))
                    return M["M_Arch_Tile"];

                // 生活区 D5 用浅色板材（生活区更亮）
                if (name.Contains("_D5")) return M["M_Arch_WallPanel"];

                // 其他用标准工业钢板
                return M["M_Arch_WallSteel"];
            }

            // 其他未知建筑构件 → 默认钢板
            return M["M_Arch_WallSteel"];
        }

        /// <summary>按对象名给家具/道具挑材质（比按材质名更贴合实际物品）。</summary>
        private static Material PickPropMaterial(string objName, string oldMat, Dictionary<string, Material> M)
        {
            // ── 金属家具（柜子、货架）──
            if (objName.Contains("柜") || objName.Contains("货架"))
                return M["M_Mach_DeviceShell"];

            // ── 桌子 ──
            if (objName.Contains("实验台") || objName.Contains("操作台"))
                return M["M_Prop_Metal"];          // 金属台面

            if (objName.Contains("长桌") || objName.Contains("供桌") || objName.Contains("档案桌"))
                return M["M_Prop_Wood"];           // 木桌面

            // ── 椅子 ──
            if (objName.Contains("椅"))
                return M["M_Prop_Wood"];

            // ── 腿/柱/栏杆（细金属件）──
            if (objName.Contains("腿") || objName.Contains("柱") || objName.Contains("栏杆"))
                return M["M_Mach_BrushedSteel"];

            // ── 锈桶 ──
            if (objName.Contains("油桶"))
                return M["M_Mach_WornMetal"];

            // ── 箱子 ──
            if (objName.Contains("箱"))
                return M["M_Prop_Crate"];

            // ── 吸音板（暗色多孔）──
            if (objName.Contains("吸音"))
                return M["M_Mach_Rubber"];

            // ── 灯座 ──
            if (objName.Contains("灯座"))
                return M["M_Mach_DarkPainted"];

            // ── 排水口 ──
            if (objName.Contains("排水"))
                return M["M_Mach_DarkPainted"];

            // ── 文件 ──
            if (objName.Contains("文件"))
                return M["M_Prop_NotePaper"];

            // ── 屏幕墙 ──
            if (objName.Contains("屏幕"))
                return M["M_Tech_Glass"];

            // ── 长椅 ──
            if (objName.Contains("长椅"))
                return M["M_Prop_Wood"];

            // ── 控制仪/混响仪/设备 ──
            if (objName.Contains("仪") || objName.Contains("设备") || objName.Contains("控制柜"))
                return M["M_Mach_DeviceShell"];

            // ── 床头柜 ──
            if (objName.Contains("床头"))
                return M["M_Prop_White"];

            // ── 泵 ──
            if (objName.Contains("泵"))
                return M["M_Mach_BrushedSteel"];

            // ── 其他保持原材质（已被 map 处理过）──
            return null;
        }

        private static void Verify()
        {
            Log("---- 验证 ----");
            var renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            var stat = new Dictionary<string, int>();
            var texStat = new Dictionary<string, bool>();
            int nullSlots = 0, pink = 0;

            foreach (var r in renderers)
            {
                if (r == null) continue;
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null) { nullSlots++; continue; }
                    if (!stat.ContainsKey(m.name)) stat[m.name] = 0;
                    stat[m.name]++;
                    if (m.shader != null && m.shader.name.Contains("Error")) pink++;
                    if (!texStat.ContainsKey(m.name))
                        texStat[m.name] = m.GetTexture("_BaseMap") != null;
                }
            }

            Log($"Renderer 总数 = {renderers.Length}");
            Log($"空材质槽 = {nullSlots}");
            Log($"错误 shader = {pink}");
            Log($"材质种类 = {stat.Count}");

            int withTex = 0;
            var keys = new List<string>(stat.Keys);
            keys.Sort((a, b) => stat[b].CompareTo(stat[a]));
            foreach (var k in keys)
            {
                if (texStat[k]) withTex++;
                Log($"  {k,-28} {stat[k],4} 次   贴图={(texStat[k] ? "✓" : "-")}");
            }
            Log($"有贴图的材质 = {withTex} / {stat.Count}");
        }

        private static void Log(string msg)
        {
            string line = $"[Ch2Assign] {msg}";
            Debug.Log(line);
            try
            {
                Directory.CreateDirectory(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports");
                File.AppendAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Ch2MaterialAssign_Result.txt",
                                   line + "\n", new UTF8Encoding(false));
            }
            catch { }
        }
    }
}