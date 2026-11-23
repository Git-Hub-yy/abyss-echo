using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S68b：第二章材质分配优化。
    ///
    /// ══════════════════════ 上一轮的问题 ══════════════════════
    ///
    ///   第一轮分配逻辑是"先查材质名映射表，查不到才按对象名判断"。
    ///
    ///   结果：生活区长桌用的旧材质 M_Ch2_Crate 在映射表里 → 直接变成 M_Prop_Crate，
    ///         **根本没走到"桌子应该用木材"的对象名判断**。
    ///
    ///   同样问题：床头柜（应该白色）变成了深色，供桌、档案桌也变成了金属箱材质。
    ///
    /// ══════════════════════ 修正优先级 ══════════════════════
    ///
    ///   **对象名判断优先，材质名映射只作为兜底。**
    ///   因为对象名描述的是"这个东西是什么"，
    ///   而材质名只描述"它以前被涂了什么颜色"。
    ///
    /// ══════════════════════ 生活区辅助色（任务要求）══════════════════════
    ///
    ///   D5 生活区需要"旧白、灰、暗黄"三种辅助色：
    ///     · 旧白 → 床头柜、椅面、储物柜内胆
    ///     · 灰   → 排水口、控制柜面板
    ///     · 暗黄 → 警示区域（甬道口、危险设备）
    /// </summary>
    public static class Ch2MaterialPolishBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter2_Graybox.unity";
        private const string Lib = "Assets/Art/Materials";

        [MenuItem("ABYSS ECHO/美术 v8.2/优化第二章材质分配")]
        public static void Build() => Run();

        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Log("已打开第二章场景");

            var M = new Dictionary<string, Material>();
            foreach (var n in new[]
            {
                "Props/M_Prop_Metal", "Props/M_Prop_Wood", "Props/M_Prop_Crate",
                "Props/M_Prop_Plastic", "Props/M_Prop_Ceramic", "Props/M_Prop_Fabric",
                "Props/M_Prop_White", "Props/M_Prop_HazardStripe", "Props/M_Prop_NotePaper",
                "Props/M_Prop_Dark",
                "Machinery/M_Mach_DeviceShell", "Machinery/M_Mach_DarkPainted",
                "Machinery/M_Mach_BrushedSteel", "Machinery/M_Mach_WornMetal",
                "Machinery/M_Mach_Rubber",
            })
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>($"{Lib}/{n}.mat");
                if (mat != null) M[Path.GetFileName(n)] = mat;
            }
            Log($"加载 {M.Count} 个道具材质");

            int changed = 0;

            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (r == null) continue;
                string name = r.gameObject.name;
                var target = DecideByName(name, M);
                if (target == null) continue;

                if (r.sharedMaterial == target) continue;

                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = target;
                r.sharedMaterials = mats;
                EditorUtility.SetDirty(r);
                changed++;

                if (changed <= 25)
                    Log($"  {name,-20} → {target.name}");
            }

            Log($"按对象名修正 {changed} 个");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Log("场景已保存");

            Verify();
        }

        /// <summary>纯粹按对象名决定材质。</summary>
        private static Material DecideByName(string n, Dictionary<string, Material> M)
        {
            // ══ 桌子类 ══
            if (n.Contains("供桌") && !n.Contains("腿"))
                return M["M_Prop_Wood"];            // 供奉间的供桌：木

            if (n.Contains("档案桌") && !n.Contains("腿"))
                return M["M_Prop_Wood"];            // 档案室桌子：木

            if (n.Contains("生活区长桌"))
                return M["M_Prop_Wood"];            // 生活区长桌：木

            if (n.Contains("实验台") || n.Contains("操作台"))
                return M["M_Prop_Metal"];           // 实验台：金属台面

            // ══ 椅子 ══
            if (n.Contains("椅面"))
                return M["M_Prop_White"];           // 生活区椅面：旧白（辅助色）

            if (n.Contains("椅") && !n.Contains("腿"))
                return M["M_Prop_Wood"];

            // ══ 床/柜 ══
            if (n.Contains("床头柜"))
                return M["M_Prop_White"];           // 生活区床头柜：旧白（辅助色）

            if (n.Contains("储物柜"))
                return M["M_Prop_White"];           // 生活区储物柜：旧白（辅助色）

            if (n.Contains("档案柜") || n.Contains("仪器柜"))
                return M["M_Mach_DeviceShell"];     // 档案柜/仪器柜：设备外壳

            if (n.Contains("控制柜"))
                return M["M_Prop_Ceramic"];         // 控制柜：灰色（辅助色）

            // ══ 危险/警示（暗黄辅助色）══
            if (n.Contains("排水口"))
                return M["M_Prop_HazardStripe"];    // 排水口：黄黑警示

            if (n.Contains("油桶"))
                return M["M_Prop_HazardStripe"];    // 油桶：黄黑警示

            if (n.Contains("箱A"))
                return M["M_Prop_HazardStripe"];    // 带 A 标记的箱子：警示黄

            // ══ 普通箱子 ══
            if (n.StartsWith("箱") || n.Contains("翻倒的箱"))
                return M["M_Prop_Crate"];

            // ══ 其他小件 ══
            if (n.Contains("腿") || n.Contains("栏杆") || n.Contains("柱"))
                return M["M_Mach_BrushedSteel"];

            if (n.Contains("吸音"))
                return M["M_Mach_Rubber"];

            if (n.Contains("长椅"))
                return M["M_Prop_Wood"];

            if (n.Contains("文件"))
                return M["M_Prop_NotePaper"];

            if (n.Contains("灯座"))
                return M["M_Mach_DarkPainted"];

            if (n.Contains("泵"))
                return M["M_Mach_BrushedSteel"];

            if (n.Contains("仪") || n.Contains("悬挂设备"))
                return M["M_Mach_DeviceShell"];

            return null;    // 不改
        }

        private static void Verify()
        {
            Log("---- 验证：生活区辅助色使用情况 ----");
            var stat = new Dictionary<string, int>();
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (r == null || r.sharedMaterial == null) continue;
                Vector3 p = r.transform.position;
                bool inLiving = p.x >= -9f && p.x <= 9f && p.z >= 19f && p.z <= 29f;
                if (!inLiving) continue;
                if (r.sharedMaterial.name.StartsWith("M_Prop") || r.sharedMaterial.name.StartsWith("M_Mach"))
                    Log($"  生活区: {r.gameObject.name,-18} {r.sharedMaterial.name}");
            }
        }

        private static void Log(string msg)
        {
            string line = $"[Ch2Polish] {msg}";
            Debug.Log(line);
            try
            {
                Directory.CreateDirectory(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports");
                File.AppendAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Ch2MaterialPolish_Result.txt",
                                   line + "\n", new UTF8Encoding(false));
            }
            catch { }
        }
    }
}