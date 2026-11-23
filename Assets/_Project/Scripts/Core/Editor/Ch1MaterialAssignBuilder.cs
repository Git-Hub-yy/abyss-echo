using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S67b：把第一章场景的材质分配到新的 PBR 材质库。
    ///
    /// ══════════════════════ 分配策略 ══════════════════════
    ///
    ///   不按"物体名关键词"粗暴匹配，而是**按现有材质 + 对象用途**映射：
    ///
    ///     旧材质                 →  新材质
    ///     M_Wall_Metal           →  M_Arch_WallSteel
    ///     M_Tiles                →  M_Arch_Tile
    ///     M_Floor_Industrial     →  M_Arch_FloorPlate
    ///     M_Concrete             →  M_Arch_FloorConcrete
    ///     M_Prop_Metal           →  M_Prop_Metal（同名，就地升级）
    ///     ...
    ///
    ///   这样保持原有"墙是墙、地板是地板"的设计意图，
    ///   只是把纯色换成有 PBR 贴图的版本。
    ///
    /// ══════════════════════ 原地升级 ══════════════════════
    ///
    ///   同名材质（M_Prop_Metal 等）**直接覆盖原文件内容**，
    ///   不改场景里的引用 —— 这样 81 处引用一次全部生效，
    ///   而且不会产生多余材质实例（性能要求）。
    ///
    ///   新命名的材质（M_Arch_*）才做引用替换。
    /// </summary>
    public static class Ch1MaterialAssignBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity";
        private const string Lib = "Assets/Art/Materials";
        private const string Old = "Assets/_Project/Art/Materials";

        [MenuItem("ABYSS ECHO/美术 v8.0/分配第一章材质")]
        public static void Build() => Run();

        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Log("已打开第一章场景");

            // ══════════════════ 第一步：原地升级同名材质 ══════════════════
            //
            //   M_Prop_* 系列保持原路径和名字，但把内容换成 PBR 版本。
            //   这样场景里的 81 处 M_Prop_Metal 引用不用改。

            int upgraded = 0;
            upgraded += CopyParams($"{Lib}/Props/M_Prop_Metal", $"{Old}/Props/M_Prop_Metal");
            upgraded += CopyParams($"{Lib}/Props/M_Prop_Wood", $"{Old}/Props/M_Prop_Wood");
            upgraded += CopyParams($"{Lib}/Props/M_Prop_Dark", $"{Old}/Props/M_Prop_Dark");
            upgraded += CopyParams($"{Lib}/Props/M_Prop_White", $"{Old}/Props/M_Prop_White");
            upgraded += CopyParams($"{Lib}/Props/M_Prop_Fabric", $"{Old}/Props/M_Prop_Fabric");
            upgraded += CopyParams($"{Lib}/Props/M_Prop_Crate", $"{Old}/Props/M_Prop_Crate");
            upgraded += CopyParams($"{Lib}/Props/M_Prop_Ceramic", $"{Old}/Props/M_Prop_Ceramic");
            upgraded += CopyParams($"{Lib}/Props/M_Prop_NotePaper", $"{Old}/Props/M_Prop_NotePaper");
            upgraded += CopyParams($"{Lib}/Architecture/M_Arch_BlastDoor", $"{Old}/Interactable/M_Door_Metal");

            Log($"原地升级材质 {upgraded} 个");

            // ══════════════════ 第二步：替换场景引用 ══════════════════

            var map = new Dictionary<Material, Material>();

            // ⚠️ 路径必须带 .mat 扩展名，否则 LoadAssetAtPath 返回 null
            void AddMap(string oldPath, string newPath)
            {
                var o = AssetDatabase.LoadAssetAtPath<Material>(WithExt(oldPath));
                var n = AssetDatabase.LoadAssetAtPath<Material>(WithExt(newPath));
                if (o != null && n != null) map[o] = n;
                else Log($"  ⚠️ 映射失败 {oldPath}({o != null}) → {newPath}({n != null})");
            }

            // 建筑
            AddMap($"{Old}/Level/M_Wall_Metal", $"{Lib}/Architecture/M_Arch_WallSteel");
            AddMap($"{Old}/Level/M_Tiles", $"{Lib}/Architecture/M_Arch_Tile");
            AddMap($"{Old}/Level/M_Floor_Industrial", $"{Lib}/Architecture/M_Arch_FloorPlate");
            AddMap($"{Old}/Level/M_Concrete", $"{Lib}/Architecture/M_Arch_FloorConcrete");
            AddMap($"{Old}/Level/M_Pipe", $"{Lib}/Machinery/M_Mach_Pipe");
            AddMap($"{Old}/Level/M_Metal_Rusted", $"{Lib}/Machinery/M_Mach_WornMetal");

            // 其余道具 / 交互物
            AddMap($"{Old}/Props/M_Prop_Locker", $"{Lib}/Machinery/M_Mach_DeviceShell");
            AddMap($"{Old}/Props/M_Prop_Server", $"{Lib}/Machinery/M_Mach_DarkPainted");
            AddMap($"{Old}/Props/M_Prop_Board", $"{Lib}/Props/M_Prop_Plastic");
            AddMap($"{Old}/Props/M_Prop_Red", $"{Lib}/Technology/M_Tech_GlowRed");
            AddMap($"{Old}/Props/M_Prop_Screen", $"{Lib}/Technology/M_Tech_ScreenBlue");

            AddMap($"{Old}/Interactable/M_Console_Desk", $"{Lib}/Machinery/M_Mach_DeviceShell");
            AddMap($"{Old}/Interactable/M_Console_Leg", $"{Lib}/Machinery/M_Mach_DarkPainted");
            AddMap($"{Old}/Interactable/M_Console_Screen", $"{Lib}/Technology/M_Tech_ScreenBlue");
            AddMap($"{Old}/Interactable/M_Door_Deep", $"{Lib}/Architecture/M_Arch_BlastDoor");
            AddMap($"{Old}/Interactable/M_Keypad_Body", $"{Lib}/Machinery/M_Mach_DarkPainted");
            AddMap($"{Old}/Interactable/M_Keypad_Lamp", $"{Lib}/Technology/M_Tech_IndicatorGreen");
            AddMap($"{Old}/Interactable/M_PowerPanel_Body", $"{Lib}/Machinery/M_Mach_DarkPainted");
            AddMap($"{Old}/Interactable/M_PowerPanel_Lamp", $"{Lib}/Technology/M_Tech_GlowRed");
            AddMap($"{Old}/Interactable/M_Pump_Body", $"{Lib}/Machinery/M_Mach_BrushedSteel");
            AddMap($"{Old}/Interactable/M_Pump_Lamp", $"{Lib}/Technology/M_Tech_GlowCyan");
            AddMap($"{Old}/Interactable/M_Pump_Panel", $"{Lib}/Technology/M_Tech_ScreenBlue");
            AddMap($"{Old}/Interactable/M_Tape_Body", $"{Lib}/Props/M_Prop_Plastic");
            AddMap($"{Old}/Interactable/M_Tape_Lamp", $"{Lib}/Technology/M_Tech_IndicatorGreen");
            AddMap($"{Old}/Interactable/M_Water_D2", $"{Lib}/Technology/M_Tech_Glass");
            AddMap($"{Old}/Monster/M_Monster_Silhouette", $"{Lib}/Organic/M_Org_DarkSkin");

            Log($"映射表 {map.Count} 条（无效的已跳过）");

            // 遍历场景所有 Renderer
            int replaced = 0, rendererCount = 0;
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                if (mats == null || mats.Length == 0) continue;

                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    if (map.TryGetValue(mats[i], out var nw) && nw != null)
                    {
                        mats[i] = nw;
                        changed = true;
                    }
                }

                if (changed)
                {
                    r.sharedMaterials = mats;
                    EditorUtility.SetDirty(r);
                    replaced++;
                }
                rendererCount++;
            }

            Log($"遍历 {rendererCount} 个 Renderer，替换 {replaced} 个的材质引用");

            EditorSceneManager.MarkSceneDirty(scene);
            bool ok = EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Log(ok ? "场景已保存" : "❌ 保存失败");

            Verify();
        }

        /// <summary>把源材质的参数复制到目标材质（原地升级，保持路径不变）。</summary>
        private static int CopyParams(string srcPath, string dstPath)
        {
            var src = AssetDatabase.LoadAssetAtPath<Material>(srcPath + ".mat");
            var dst = AssetDatabase.LoadAssetAtPath<Material>(dstPath + ".mat");
            if (src == null) { Log($"  ⚠️ 源材质不存在 {srcPath}"); return 0; }
            if (dst == null) { Log($"  ⚠️ 目标材质不存在 {dstPath}"); return 0; }

            dst.shader = src.shader;

            // 逐项复制
            CopyTex(src, dst, "_BaseMap");
            CopyTex(src, dst, "_BumpMap");
            CopyTex(src, dst, "_OcclusionMap");
            CopyTex(src, dst, "_EmissionMap");

            dst.SetColor("_BaseColor", src.GetColor("_BaseColor"));
            dst.SetFloat("_Metallic", src.GetFloat("_Metallic"));
            dst.SetFloat("_Smoothness", src.GetFloat("_Smoothness"));
            dst.SetFloat("_BumpScale", src.GetFloat("_BumpScale"));
            dst.SetFloat("_OcclusionStrength", src.GetFloat("_OcclusionStrength"));
            dst.SetColor("_EmissionColor", src.GetColor("_EmissionColor"));
            dst.SetTextureScale("_BaseMap", src.GetTextureScale("_BaseMap"));
            dst.SetTextureScale("_BumpMap", src.GetTextureScale("_BumpMap"));

            // 关键字
            CopyKeyword(src, dst, "_NORMALMAP");
            CopyKeyword(src, dst, "_EMISSION");

            dst.SetFloat("_Surface", src.GetFloat("_Surface"));
            dst.SetFloat("_Blend", src.GetFloat("_Blend"));
            dst.renderQueue = src.renderQueue;

            EditorUtility.SetDirty(dst);
            Log($"  ↑ {Path.GetFileName(dstPath)}  ←  {Path.GetFileName(srcPath)}");
            return 1;
        }

        private static string WithExt(string p)
            => p.EndsWith(".mat", System.StringComparison.OrdinalIgnoreCase) ? p : p + ".mat";

        private static void CopyTex(Material src, Material dst, string prop)
        {
            var t = src.GetTexture(prop);
            if (t != null) dst.SetTexture(prop, t);
        }

        private static void CopyKeyword(Material src, Material dst, string kw)
        {
            if (src.IsKeywordEnabled(kw)) dst.EnableKeyword(kw);
            else dst.DisableKeyword(kw);
        }

        private static void Verify()
        {
            Log("---- 验证 ----");
            var renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            var stat = new Dictionary<string, int>();
            var nullMats = new List<string>();
            var pinkish = new List<string>();

            foreach (var r in renderers)
            {
                if (r == null) continue;
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null) { nullMats.Add(r.gameObject.name); continue; }
                    if (!stat.ContainsKey(m.name)) stat[m.name] = 0;
                    stat[m.name]++;

                    // 检查是否是"粉色错误材质"（URP 默认的错误 shader）
                    if (m.shader != null && m.shader.name.Contains("Error"))
                        pinkish.Add($"{r.gameObject.name}:{m.name}");
                }
            }

            Log($"Renderer 总数 = {renderers.Length}");
            Log($"空材质槽 = {nullMats.Count}");
            Log($"错误 shader 材质 = {pinkish.Count}");
            Log($"使用中的材质种类 = {stat.Count}");
            Log("");
            var keys = new List<string>(stat.Keys);
            keys.Sort((a, b) => stat[b].CompareTo(stat[a]));
            foreach (var k in keys)
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(FindMatPath(k));
                bool hasTex = m != null && (m.GetTexture("_BaseMap") != null);
                bool emit = m != null && m.IsKeywordEnabled("_EMISSION");
                Log($"  {k,-30} {stat[k],4} 次   贴图={(hasTex ? "✓" : "-")}  发光={(emit ? "✓" : "-")}");
            }
        }

        private static string FindMatPath(string name)
        {
            foreach (var g in AssetDatabase.FindAssets($"t:Material {name}"))
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (Path.GetFileNameWithoutExtension(p) == name) return p;
            }
            return "";
        }

        private static void Log(string msg)
        {
            string line = $"[Ch1Assign] {msg}";
            Debug.Log(line);
            try
            {
                Directory.CreateDirectory(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports");
                File.AppendAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Ch1MaterialAssign_Result.txt",
                                   line + "\n", new UTF8Encoding(false));
            }
            catch { }
        }
    }
}