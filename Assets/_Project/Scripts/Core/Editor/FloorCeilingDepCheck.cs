using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S74a：修复前的**依赖关系检查**（只读）。
    ///
    /// 在修改任何材质之前必须确认：
    ///   ① M_Arch_FloorPlate 被哪些对象使用？是否只用于地板？
    ///   ② M_Arch_Ceiling 被哪些对象使用？是否与墙面/金属件共享？
    ///
    /// 如果材质同时被地板和其他物体使用，**不能直接改**，
    /// 必须为地板创建独立副本。
    /// </summary>
    public static class FloorCeilingDepCheck
    {
        private static readonly (int ch, string path)[] Scenes =
        {
            (1, "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity"),
            (2, "Assets/_Project/Scenes/Level_Chapter2_Graybox.unity"),
            (3, "Assets/_Project/Scenes/Level_Chapter3_Graybox.unity"),
            (4, "Assets/_Project/Scenes/Level_Chapter4_Graybox.unity"),
            (5, "Assets/_Project/Scenes/Level_Chapter5_Graybox.unity"),
        };

        private const string FLOOR_MAT = "Assets/Art/Materials/Architecture/M_Arch_FloorPlate.mat";
        private const string CEIL_MAT = "Assets/Art/Materials/Architecture/M_Arch_Ceiling.mat";

        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════════════════════════");
            sb.AppendLine("  修改前依赖检查（只读）");
            sb.AppendLine("═══════════════════════════════════════════════════════════");

            // 材质当前参数
            var floorMat = AssetDatabase.LoadAssetAtPath<Material>(FLOOR_MAT);
            var ceilMat = AssetDatabase.LoadAssetAtPath<Material>(CEIL_MAT);

            sb.AppendLine();
            sb.AppendLine("──── 目标材质当前参数 ────");
            if (floorMat != null) sb.AppendLine($"  {floorMat.name}  Tiling={floorMat.GetTextureScale("_BaseMap").x}  " +
                                                 $"Metallic={floorMat.GetFloat("_Metallic"):F2}  Smoothness={floorMat.GetFloat("_Smoothness"):F2}");
            else sb.AppendLine("  ❌ 找不到 M_Arch_FloorPlate");
            if (ceilMat != null) sb.AppendLine($"  {ceilMat.name}  Tiling={ceilMat.GetTextureScale("_BaseMap").x}  " +
                                                $"Metallic={ceilMat.GetFloat("_Metallic"):F2}  Smoothness={ceilMat.GetFloat("_Smoothness"):F2}");
            else sb.AppendLine("  ❌ 找不到 M_Arch_Ceiling");

            // 逐章扫描使用情况
            var floorUsers = new Dictionary<string, List<string>>();
            var ceilUsers = new Dictionary<string, List<string>>();
            var floorSlots = 0;
            var ceilSlots = 0;

            foreach (var (ch, path) in Scenes)
            {
                if (!File.Exists(path)) continue;
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                Physics.SyncTransforms();

                foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                {
                    if (r == null) continue;
                    var m = r.sharedMaterial;
                    if (m == null) continue;

                    string n = r.gameObject.name;

                    if (m.name == "M_Arch_FloorPlate")
                    {
                        floorSlots++;
                        string key = ClassifyObject(n);
                        if (!floorUsers.ContainsKey(key)) floorUsers[key] = new List<string>();
                        if (floorUsers[key].Count < 100)
                            floorUsers[key].Add($"Ch{ch}/{n}  Tiling={m.GetTextureScale("_BaseMap").x:F2}");
                    }
                    else if (m.name == "M_Arch_Ceiling")
                    {
                        ceilSlots++;
                        string key = ClassifyObject(n);
                        if (!ceilUsers.ContainsKey(key)) ceilUsers[key] = new List<string>();
                        if (ceilUsers[key].Count < 60)
                            ceilUsers[key].Add($"Ch{ch}/{n}");
                    }
                }
            }

            sb.AppendLine();
            sb.AppendLine($"──── M_Arch_FloorPlate 使用对象分类（共 {floorSlots} 个槽）────");
            foreach (var kv in floorUsers)
            {
                sb.AppendLine($"  [{kv.Key}]  {kv.Value.Count} 个样本：");
                foreach (var s in kv.Value) sb.AppendLine($"      {s}");
            }

            sb.AppendLine();
            sb.AppendLine($"──── M_Arch_Ceiling 使用对象分类（共 {ceilSlots} 个槽）────");
            foreach (var kv in ceilUsers)
            {
                sb.AppendLine($"  [{kv.Key}]  {kv.Value.Count} 个样本：");
                foreach (var s in kv.Value) sb.AppendLine($"      {s}");
            }

            // 是否被 Prefab 引用
            sb.AppendLine();
            sb.AppendLine("──── Prefab 引用检查 ────");
            int prefabRefs = 0;
            foreach (var g in AssetDatabase.FindAssets("t:Prefab"))
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                var deps = AssetDatabase.GetDependencies(p, true);
                foreach (var d in deps)
                {
                    if (d == FLOOR_MAT || d == CEIL_MAT)
                    {
                        sb.AppendLine($"  ⚠️ {Path.GetFileName(p)} 引用了 {Path.GetFileName(d)}");
                        prefabRefs++;
                    }
                }
            }
            if (prefabRefs == 0) sb.AppendLine("  ✅ 没有 Prefab 引用这两个材质");

            // 结论
            sb.AppendLine();
            sb.AppendLine("──── 结论 ────");
            bool floorOnlyFloor = floorUsers.ContainsKey("地板") && floorUsers.Count == 1;
            bool ceilOnlyCeil = ceilUsers.ContainsKey("天花板") && ceilUsers.Count == 1;

            sb.AppendLine($"  M_Arch_FloorPlate 仅用于地板？ {(floorOnlyFloor ? "✅ 是，可直接修改 Tiling" : "⚠️ 否，需要独立副本")}");
            sb.AppendLine($"  M_Arch_Ceiling 仅用于天花板？ {(ceilOnlyCeil ? "✅ 是，可直接修改 Metallic" : "⚠️ 否，需要独立副本")}");

            string text = sb.ToString();
            Debug.Log(text);
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\FloorCeilingDepCheck.txt",
                              text, new UTF8Encoding(false));
        }

        private static string ClassifyObject(string name)
        {
            if (name.StartsWith("Floor")) return "地板";
            if (name.StartsWith("Ceiling")) return "天花板";
            if (name.Contains("Door")) return "门";
            if (name.Contains("Wall") || name.Contains("North_") || name.Contains("South_") ||
                name.Contains("East_") || name.Contains("West_")) return "墙体";
            if (name.Contains("Pipe")) return "管道";
            if (name.Contains("Crate")) return "箱子";
            if (name.Contains("Transit")) return "过渡区";
            if (name.Contains("Corridor")) return "走廊";
            return "其他";
        }
    }
}