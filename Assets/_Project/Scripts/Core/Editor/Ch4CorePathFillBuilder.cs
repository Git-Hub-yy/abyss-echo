using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S71b：修复第四章 G5 北门 → G5_G7_Link 连廊之间的**地面断层**。
    ///
    /// ══════════════════════ 实测缺陷 ══════════════════════
    ///
    ///   G5 北门门洞      X[-20.20, -17.80]   中心 x = -19
    ///   Transit_Floor    X[-20.20,  -9.90]   Z[5.00, 6.90]
    ///   Transit_Floor_North X[-20.20, -13.20] Z[6.90, 9.20]
    ///   Floor_G5_G7_Link   X[-13.15,  -9.85] Z[5.00, 9.15]
    ///
    ///   玩家从北门 (x≈-19) 走出后向北：
    ///     z=5.00~6.90  有地面 ✅
    ///     z=6.90~9.20  有地面 ✅（但 X 只到 -13.20）
    ///     要进连廊必须横移到 x > -13.15
    ///     而 z=6.90~9.20 且 x=-17.8~-13.2 的区域**没有地面** ❌
    ///
    ///   实测密集采样（0.25 米）确认：
    ///     x=-19 沿线在 z>9.2 之后全是虚空（15.25 米）
    ///     z=5.5~9.0 时 x=-20.2~-13.2 有地面，但到 x=-13.15 才接上连廊
    ///
    /// ══════════════════════ 修复方案 ══════════════════════
    ///
    ///   在 z[6.90, 9.20]、x[-17.80, -13.15] 补一块过渡地板，
    ///   把「北门走廊」和「通往 G7 的连廊」接起来。
    ///
    ///   只新增地面（Floor）和天花板，不移动任何现有物体。
    ///   不修改碰撞体、不修改 Transform、不修改任何脚本。
    /// ══════════════════════ 尺寸 ══════════════════════
    ///
    ///   补齐区域：
    ///     X: -17.80 → -13.15   宽 4.65 米
    ///     Z:  6.90 →  9.20     深 2.30 米
    ///   地板厚度 0.5（与 Transit_Floor 一致）
    ///   地板顶面 y = 0.00（与现有地面平齐）
    ///   天花板 y[3.40, 3.70]（与 Transit_Ceiling_North 一致）
    /// </summary>
    public static class Ch4CorePathFillBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter4_Graybox.unity";

        // 补齐区域
        private const float X_MIN = -17.80f;
        private const float X_MAX = -13.15f;
        private const float Z_MIN = 6.90f;
        private const float Z_MAX = 9.20f;

        // 地板：顶面 y=0，厚 0.5
        private const float FLOOR_TOP = 0.00f;
        private const float FLOOR_THICK = 0.50f;

        // 天花板：y[3.40, 3.70]
        private const float CEIL_BOTTOM = 3.40f;
        private const float CEIL_THICK = 0.30f;

        [MenuItem("ABYSS ECHO/修复 v9.4/补齐核心腔路径地面")]
        public static void Build() => Run();

        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Log("已打开第四章场景");

            // ══ 找父节点与材质 ══
            Transform parent = null;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t.name == "--- GEOMETRY_CH4 ---" || t.name.Contains("GEOMETRY"))
                { parent = t; break; }
            }
            if (parent == null)
            {
                var g = GameObject.Find("Floor_G5");
                if (g != null) parent = g.transform.parent;
            }
            Log($"父节点 = {(parent != null ? parent.name : "无（将放场景根）")}");

            Material floorMat = null;
            Material ceilMat = null;
            var fg = GameObject.Find("Transit_Floor");
            var cg = GameObject.Find("Transit_Ceiling_North");
            if (fg != null) floorMat = fg.GetComponent<MeshRenderer>()?.sharedMaterial;
            if (cg != null) ceilMat = cg.GetComponent<MeshRenderer>()?.sharedMaterial;
            Log($"地板材质 = {floorMat?.name ?? "NULL"}   天花板材质 = {ceilMat?.name ?? "NULL"}");

            int added = 0;

            // ══ 补地板 ══
            added += MakeBox("Ch4_CorePath_Floor",
                             X_MIN, X_MAX, Z_MIN, Z_MAX,
                             FLOOR_TOP, FLOOR_THICK,
                             floorMat, parent);

            // ══ 补天花板 ══
            added += MakeBox("Ch4_CorePath_Ceiling",
                             X_MIN, X_MAX, Z_MIN, Z_MAX,
                             CEIL_BOTTOM + CEIL_THICK, CEIL_THICK,
                             ceilMat, parent);

            // ══ 补东侧墙（x=-13.15 侧，封住过渡区与连廊之间的空隙外的部分）══
            //
            //   过渡区 X 到 -13.15，连廊也在 X[-13.15,-9.85]，
            //   两者在 x=-13.15 处正好衔接，不需要额外墙。

            // ══ 补北侧封口（z=9.20）══
            //
            //   Transit_Wall_North_End 在 X[-20.20, -13.20]，Z[9.20, 9.50]
            //   过渡区补到 x=-13.15，差 0.05 米，实际会被连廊侧墙覆盖，不用补。

            if (added > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                bool ok = EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Log(ok ? $"已保存场景（新增 {added} 个对象）" : "❌ 保存失败");
            }
            else Log("无需改动");

            Verify();
        }

        /// <summary>创建一个立方体 Box（带 MeshRenderer + BoxCollider）。</summary>
        private static int MakeBox(string name, float xMin, float xMax,
                                    float zMin, float zMax, float topY, float thick,
                                    Material mat, Transform parent)
        {
            // 已存在就跳过（幂等）
            var exist = GameObject.Find(name);
            if (exist != null)
            {
                Log($"  = {name} 已存在，跳过");
                return 0;
            }

            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);

            float cx = (xMin + xMax) * 0.5f;
            float cz = (zMin + zMax) * 0.5f;
            float cy = topY - thick * 0.5f;

            go.transform.position = new Vector3(cx, cy, cz);
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = new Vector3(xMax - xMin, thick, zMax - zMin);

            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();

            // 用 Unity 内置 Cube 网格
            var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mf.sharedMesh = temp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(temp);

            if (mat != null) mr.sharedMaterial = mat;

            var col = go.AddComponent<BoxCollider>();
            col.size = Vector3.one;

            Log($"  + {name,-28} X[{xMin:F2},{xMax:F2}] Z[{zMin:F2},{zMax:F2}] " +
                $"Y顶={topY:F2} 厚={thick:F2}  材质={mat?.name ?? "无"}");
            return 1;
        }

        private static void Verify()
        {
            Log("---- 验证：沿 x=-19 和 x=-15 向北采样 ----");
            Physics.SyncTransforms();

            foreach (float probeX in new[] { -19f, -16f, -14f, -11f })
            {
                float lastHoleZ = -1f;
                var holes = new System.Collections.Generic.List<float>();

                for (float z = 4.5f; z <= 9.10f; z += 0.25f)
                {
                    var hits = Physics.RaycastAll(new Vector3(probeX, 6f, z), Vector3.down, 20f,
                                                  ~0, QueryTriggerInteraction.Ignore);
                    bool has = false;
                    foreach (var h in hits)
                    {
                        if (h.collider == null) continue;
                        var b = h.collider.bounds;
                        if (probeX < b.min.x - 0.01f || probeX > b.max.x + 0.01f) continue;
                        if (z < b.min.z - 0.01f || z > b.max.z + 0.01f) continue;
                        if (b.max.y > 1.0f) continue;      // 只看地面高度
                        if (b.max.y < -1.0f) continue;
                        has = true; break;
                    }
                    if (!has) { holes.Add(z); lastHoleZ = z; }
                }

                Log($"  x={probeX,6:F1}: {(holes.Count == 0 ? "✅ 全程有地面" : $"❌ {holes.Count} 个虚空点，最大 z={lastHoleZ:F2}")}");
            }
        }

        private static void Log(string msg)
        {
            string line = $"[Ch4Fill] {msg}";
            Debug.Log(line);
            try
            {
                Directory.CreateDirectory(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports");
                File.AppendAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Ch4CorePathFill_Result.txt",
                                   line + "\n", new UTF8Encoding(false));
            }
            catch { }
        }
    }
}