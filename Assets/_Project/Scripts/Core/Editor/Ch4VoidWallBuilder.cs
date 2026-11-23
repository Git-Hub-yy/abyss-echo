using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S72c：**用墙体封死虚空**（按玩家要求：建墙防止掉下去）。
    ///
    /// ══════════════════════ 扫描发现的实际地形 ══════════════════════
    ///
    ///   扫描图（█=有地面 ·=虚空）：
    ///
    ///        -26  -22  -18  -14  -10   -6
    ///    9.0 ············█████████████████
    ///    7.0 ············█████████████████
    ///    6.5 ············█████████████████········   ← x=-9.5 右侧是空的
    ///    5.0 ··███████████████████████████········
    ///    4.5 ··███████████████████···············
    ///
    ///   **真正需要封的是**：
    ///     A. 连廊东侧 x=-9.85 边界，z=5.0~6.5 有一段没有地面
    ///        （玩家从连廊往东走会掉下去）
    ///     B. G5 北墙外 z=5.15~6.9、x=-20.2 以左（G5 西北角外）
    ///
    ///   其余虚空（x<-20.5、z>9.5）都在**封闭建筑外部**，
    ///   玩家从室内出不去，不需要处理。
    ///
    /// ══════════════════════ 方案：建墙 ══════════════════════
    ///
    ///   玩家明确要求"建一堵墙防止掉进虚空"。
    ///   比补地板更安全 —— 补地板玩家还是能走到边缘；
    ///   建墙则直接物理阻挡。
    ///
    ///   在 G5 北侧通路的两侧建**实体围栏墙**：
    ///     1. 西侧墙：x = -20.44（与 G5Door_Frame_L 对齐），z[5, 9.5]
    ///     2. 东侧墙：x = -9.85（连廊东边界），z[5, 9.5]
    ///     3. 北侧墙：z = 9.5（连廊北端封口），x[-20.44, -9.85]
    ///
    ///   墙高 3 米（与门框一致），厚 0.3 米。
    ///   不改变任何玩法逻辑，只加碰撞阻挡。
    /// </summary>
    public static class Ch4VoidWallBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter4_Graybox.unity";

        // 墙体参数
        private const float WALL_BOTTOM = 0f;
        private const float WALL_HEIGHT = 3.0f;
        private const float WALL_THICK = 0.3f;

        // 通路边界（由扫描结果确定）
        private const float PASSAGE_X_MIN = -20.44f;   // 与 G5Door_Frame_L 对齐
        private const float PASSAGE_X_MAX = -9.85f;    // 连廊东边界
        private const float PASSAGE_Z_MIN = 5.00f;     // G5 北墙
        private const float PASSAGE_Z_MAX = 9.50f;     // 连廊北端

        [MenuItem("ABYSS ECHO/修复 v9.6/建墙封堵虚空")]
        public static void Build() => Run();

        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════════════════");
            sb.AppendLine("  建墙封堵虚空");
            sb.AppendLine("═══════════════════════════════════════════════════");

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Physics.SyncTransforms();

            // 父节点
            Transform parent = null;
            var g5floor = GameObject.Find("Floor_G5");
            if (g5floor != null) parent = g5floor.transform.parent;
            sb.AppendLine($"父节点 = {(parent != null ? parent.name : "场景根")}");

            // 材质：用舱门材质（厚重的感觉）
            string[] matCandidates = {
                "Assets/Art/Materials/Architecture/M_Arch_BlastDoor.mat",
                "Assets/Art/Materials/Machinery/M_Mach_WornMetal.mat",
                "Assets/_Project/Art/Materials/Level/M_Wall_Metal.mat",
            };
            Material mat = null;
            string matUsed = "NULL";
            foreach (var p in matCandidates)
            {
                mat = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (mat != null) { matUsed = mat.name; break; }
            }
            sb.AppendLine($"墙体材质 = {matUsed}");
            sb.AppendLine();

            int added = 0;

            // ══ 1. 西侧墙（封住 x < -20.44 的虚空）══
            added += MakeWall("Ch4_VoidWall_West",
                              PASSAGE_X_MIN - WALL_THICK * 0.5f,
                              PASSAGE_Z_MIN, PASSAGE_Z_MAX,
                              horizontal: false, mat, parent, sb);

            // ══ 2. 东侧墙（只封 z[5.0, 6.8]）══
            //
            //   ⚠️ 关键：G7 西门洞在 (-10, 0, 8)，宽 2.4 → z 范围约 [6.8, 9.2]
            //   东墙不能挡住这个门洞，否则玩家进不去核心腔。
            //   真正缺地面的是 z=5.0~6.5 那一段，只封这里。
            const float GATE_Z_MIN = 6.80f;
            added += MakeWall("Ch4_VoidWall_East",
                              PASSAGE_X_MAX + WALL_THICK * 0.5f,
                              PASSAGE_Z_MIN, GATE_Z_MIN,
                              horizontal: false, mat, parent, sb);

            // ══ 3. 北侧墙（封住 z > 9.5 的虚空）══
            //
            //   只封到 x=-13.0（连廊西边界），不延伸到 G7 方向，
            //   避免挡住 G7 西门。
            added += MakeWall("Ch4_VoidWall_North",
                              PASSAGE_Z_MAX + WALL_THICK * 0.5f,
                              PASSAGE_X_MIN - WALL_THICK,
                              -13.0f,
                              horizontal: true, mat, parent, sb);

            sb.AppendLine();
            sb.AppendLine($"新增墙体 {added} 个");

            if (added > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                bool ok = EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                sb.AppendLine(ok ? "场景已保存" : "❌ 保存失败");
            }

            Verify(sb);

            string text = sb.ToString();
            Debug.Log(text);
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Ch4VoidWall.txt",
                              text, new UTF8Encoding(false));
        }

        /// <summary>建一堵墙：horizontal=true 时沿 X 延伸（法线 Z），否则沿 Z 延伸（法线 X）。</summary>
        private static int MakeWall(string name, float fixedCoord, float from, float to,
                                     bool horizontal, Material mat, Transform parent, StringBuilder sb)
        {
            if (GameObject.Find(name) != null) { sb.AppendLine($"  = {name} 已存在，跳过"); return 0; }

            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, true);

            float mid = (from + to) * 0.5f;
            float len = Mathf.Abs(to - from);

            if (horizontal)
            {
                // 沿 X 延伸，位置 z = fixedCoord
                go.transform.position = new Vector3(mid, WALL_BOTTOM + WALL_HEIGHT * 0.5f, fixedCoord);
                go.transform.localScale = new Vector3(len, WALL_HEIGHT, WALL_THICK);
            }
            else
            {
                // 沿 Z 延伸，位置 x = fixedCoord
                go.transform.position = new Vector3(fixedCoord, WALL_BOTTOM + WALL_HEIGHT * 0.5f, mid);
                go.transform.localScale = new Vector3(WALL_THICK, WALL_HEIGHT, len);
            }
            go.transform.rotation = Quaternion.identity;

            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mf.sharedMesh = temp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(temp);
            if (mat != null) mr.sharedMaterial = mat;

            var col = go.AddComponent<BoxCollider>();
            col.size = Vector3.one;

            var p = go.transform.position;
            var s = go.transform.localScale;
            sb.AppendLine($"  + {name}");
            sb.AppendLine($"      中心 ({p.x:F2}, {p.y:F2}, {p.z:F2})  尺寸 {s.x:F2} x {s.y:F2} x {s.z:F2}");
            return 1;
        }

        private static void Verify(StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine("──── 验证：走廊两侧是否已封住 ────");
            Physics.SyncTransforms();

            // 沿 x 扫描 z=7 的水平剖面，看是否有墙阻挡
            sb.AppendLine("  z=7.0 处，沿 x 从 -21 到 -9 的阻挡情况：");
            for (float x = -21f; x <= -9f; x += 0.5f)
            {
                // 从 z=7 向 x 方向水平射线，看能否穿过
                bool blockedWest = Physics.Raycast(new Vector3(x, 1.5f, 7f), Vector3.left, 1.0f);
                bool blockedEast = Physics.Raycast(new Vector3(x, 1.5f, 7f), Vector3.right, 1.0f);
                sb.Append($"    x={x,6:F1}  左挡={(blockedWest ? "█" : "·")} 右挡={(blockedEast ? "█" : "·")}\n");
            }

            // 地面检查
            sb.AppendLine();
            sb.AppendLine("  地面检查（沿走廊中线 x=-15）：");
            for (float z = 5f; z <= 10f; z += 0.5f)
            {
                bool has = false;
                var hits = Physics.RaycastAll(new Vector3(-15f, 6f, z), Vector3.down, 20f);
                foreach (var h in hits)
                {
                    if (h.collider == null) continue;
                    var b = h.collider.bounds;
                    if (-15f < b.min.x || -15f > b.max.x) continue;
                    if (z < b.min.z || z > b.max.z) continue;
                    if (b.max.y > 1.0f || b.max.y < -1.0f) continue;
                    has = true; break;
                }
                sb.Append($"    z={z,5:F1}  {(has ? "✅ 有地面" : "❌ 虚空")}\n");
            }
        }
    }
}