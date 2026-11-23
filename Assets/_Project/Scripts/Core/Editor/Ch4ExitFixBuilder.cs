using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S57：修复第四章终点门（P0）—— 北墙挖门洞 + 门后逃生通道。
    ///
    /// ══════════════════ 问题（来自只读调查 S56）══════════════════
    ///
    ///   · North_G7_Full 是完整实心北墙（20×8×0.3），没有门洞
    ///   · Door_HeartGate 只是贴在墙内侧的一块板（z=24.7，墙在 z=24.9~25.2）
    ///   · 门滑开后墙仍挡路
    ///   · z=27~42 没有地板
    ///   · 结局触发区 z[37,41] 处于虚空
    ///
    /// ══════════════════ 修复方案 ══════════════════
    ///
    ///   ① 把 North_G7_Full 拆成 3 段（左 / 右 / 门楣）
    ///      门洞：x[-1.6, 1.6]  y[0, 3.0]
    ///      理由：门本体 x[-1.5,1.5] 高 2.80，向 +X 滑开 2.94 米后
    ///            x[-1.5,1.5] 完全空出。门洞取 x[-1.6,1.6] 宽 3.2、
    ///            高 3.0，玩家（半径 0.35、高 1.8）通行绰绰有余。
    ///
    ///   ② 门后通道 z[25.2, 42.2]，宽 3.6（x[-1.8,1.8]），高 4.0
    ///      地板顶面 y=0 与 G7 地板无缝衔接
    ///
    ///   ③ 通道内暗红照明，保持恐怖风格
    ///
    ///   ④ 不改动 HeartValve / Door_HeartGate / 触发区 / 怪物
    ///
    /// ══════════════════ 幂等 ══════════════════
    ///
    ///   已存在的对象直接复用并校正，不重复创建。
    /// </summary>
    public static class Ch4ExitFixBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter4_Graybox.unity";

        // ── 门洞参数 ──
        private const float HOLE_HALF_W = 1.6f;    // 门洞半宽 → x[-1.6, 1.6]
        private const float HOLE_H = 3.0f;         // 门洞高度

        // ── 通道参数 ──
        private const float COR_Z0 = 25.2f;        // 起点（北墙外沿）
        private const float COR_Z1 = 42.2f;        // 终点（覆盖触发区 z[37,41]）
        private const float COR_HALF_IN = 1.8f;    // 内净半宽
        private const float WALL_T = 0.3f;         // 墙厚
        private const float COR_H = 4.0f;          // 通道净高

        [MenuItem("ABYSS ECHO/修复 v6.3/第四章 终点门与门后通道")]
        public static void Build() => Run();

        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Log($"已打开 {ScenePath}");

            int changes = 0;

            // ══════════════════ ① 拆北墙 ══════════════════
            changes += SplitNorthWall();

            // ══════════════════ ② 建门后通道 ══════════════════
            changes += BuildCorridor();

            // ══════════════════ ③ 照明 ══════════════════
            changes += BuildLights();

            // ══════════════════ 保存 ══════════════════
            if (changes > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                bool ok = EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Log(ok ? $"场景已保存（{changes} 项改动）" : "❌ 保存失败");
            }
            else Log("无改动");

            // ══════════════════ 验证 ══════════════════
            Verify();
        }

        // ═══════════════════════════════════════════════════════════
        private static int SplitNorthWall()
        {
            var old = GameObject.Find("North_G7_Full");
            if (old == null)
            {
                Log("  ⚠️ 找不到 North_G7_Full，跳过拆分");
                return 0;
            }

            var oldCol = old.GetComponent<BoxCollider>();
            var oldMr = old.GetComponent<MeshRenderer>();
            Vector3 cs = oldCol.bounds.size;
            Vector3 cc = oldCol.bounds.center;
            Material mat = oldMr != null ? oldMr.sharedMaterial : null;

            Log($"  原北墙: center={cc} size={cs}");

            // 已经拆过？
            bool already = GameObject.Find("North_G7_Left") != null &&
                           GameObject.Find("North_G7_Right") != null;
            if (already)
            {
                Log("  北墙已拆分，跳过（保留现有分段）");
                // 但要确保原来的 Full 已被移除
                if (old != null) { Object.DestroyImmediate(old); Log("    已移除残留的 North_G7_Full"); return 1; }
                return 0;
            }

            float xMin = cc.x - cs.x * 0.5f;   // -10
            float xMax = cc.x + cs.x * 0.5f;   // +10
            float yMin = cc.y - cs.y * 0.5f;   // 0
            float yMax = cc.y + cs.y * 0.5f;   // 8
            float zMin = cc.z - cs.z * 0.5f;   // 24.9
            float zMax = cc.z + cs.z * 0.5f;   // 25.2
            float zMid = cc.z;

            var parent = old.transform.parent != null ? old.transform.parent : null;

            // ── 左段 x[xMin, -HOLE_HALF_W] 全高 ──
            MakeBox("North_G7_Left", parent,
                new Vector3((xMin + (-HOLE_HALF_W)) * 0.5f, cc.y, zMid),
                new Vector3(((-HOLE_HALF_W) - xMin), cs.y, cs.z),
                mat, $"x[{xMin:F1},{(-HOLE_HALF_W):F1}] y[0,8]");

            // ── 右段 x[HOLE_HALF_W, xMax] 全高 ──
            MakeBox("North_G7_Right", parent,
                new Vector3((HOLE_HALF_W + xMax) * 0.5f, cc.y, zMid),
                new Vector3((xMax - HOLE_HALF_W), cs.y, cs.z),
                mat, $"x[{HOLE_HALF_W:F1},{xMax:F1}] y[0,8]");

            // ── 门楣 x[-W, W] y[HOLE_H, yMax] ──
            float lintelH = yMax - HOLE_H;
            MakeBox("North_G7_Top", parent,
                new Vector3(cc.x, HOLE_H + lintelH * 0.5f, zMid),
                new Vector3(HOLE_HALF_W * 2f, lintelH, cs.z),
                mat, $"x[-{HOLE_HALF_W:F1},{HOLE_HALF_W:F1}] y[{HOLE_H:F1},8]");

            // ── 删除原墙 ──
            Object.DestroyImmediate(old);
            Log($"  ✅ 已拆分：三层（左 / 右 / 门楣），门洞 x[-{HOLE_HALF_W},{HOLE_HALF_W}] y[0,{HOLE_H}] z[{zMin},{zMax}]");
            return 1;
        }

        // ═══════════════════════════════════════════════════════════
        private static int BuildCorridor()
        {
            var root = GameObject.Find("--- EXIT_CORRIDOR_CH4 ---");
            if (root == null)
            {
                root = new GameObject("--- EXIT_CORRIDOR_CH4 ---");
                Log("  + 创建根对象 --- EXIT_CORRIDOR_CH4 ---");
            }
            var rt = root.transform;

            Material floorMat = FindMat("M_Ch4_Bone");
            Material wallMat = FindMat("M_Ch4_FleshDark");
            Material ceilMat = FindMat("M_Ch4_Vein");

            float len = COR_Z1 - COR_Z0;
            float zMid = (COR_Z0 + COR_Z1) * 0.5f;
            float outerW = COR_HALF_IN * 2f + WALL_T * 2f;

            int n = 0;

            // 地板：顶面 y=0，与 G7 地板一致
            n += MakeBoxIn("Corridor_Floor",
                new Vector3(0f, -0.25f, zMid), new Vector3(COR_HALF_IN * 2f, 0.5f, len),
                floorMat, rt, "地板顶面 y=0");

            // 天花板：底面 y=4.0
            n += MakeBoxIn("Corridor_Ceiling",
                new Vector3(0f, COR_H + 0.15f, zMid), new Vector3(COR_HALF_IN * 2f, 0.3f, len),
                ceilMat, rt, $"天花板底面 y={COR_H}");

            // 左墙
            n += MakeBoxIn("Corridor_Wall_L",
                new Vector3(-(COR_HALF_IN + WALL_T * 0.5f), (COR_H + 0.3f) * 0.5f, zMid),
                new Vector3(WALL_T, COR_H + 0.3f, len),
                wallMat, rt, "");

            // 右墙
            n += MakeBoxIn("Corridor_Wall_R",
                new Vector3(COR_HALF_IN + WALL_T * 0.5f, (COR_H + 0.3f) * 0.5f, zMid),
                new Vector3(WALL_T, COR_H + 0.3f, len),
                wallMat, rt, "");

            // 终端墙
            n += MakeBoxIn("Corridor_Wall_End",
                new Vector3(0f, (COR_H + 0.3f) * 0.5f, COR_Z1 + WALL_T * 0.5f),
                new Vector3(outerW, COR_H + 0.3f, WALL_T),
                wallMat, rt, "");

            // 入口门框装饰（视觉过渡，不挡路）
            n += MakeBoxIn("Corridor_Frame_L",
                new Vector3(-(HOLE_HALF_W + 0.15f), (HOLE_H + 0.3f) * 0.5f, COR_Z0 + 0.25f),
                new Vector3(0.3f, HOLE_H + 0.3f, 0.5f),
                FindMat("M_Ch4_Metal"), rt, "门框柱（左）");
            n += MakeBoxIn("Corridor_Frame_R",
                new Vector3(HOLE_HALF_W + 0.15f, (HOLE_H + 0.3f) * 0.5f, COR_Z0 + 0.25f),
                new Vector3(0.3f, HOLE_H + 0.3f, 0.5f),
                FindMat("M_Ch4_Metal"), rt, "门框柱（右）");
            n += MakeBoxIn("Corridor_Frame_Top",
                new Vector3(0f, HOLE_H + 0.15f, COR_Z0 + 0.25f),
                new Vector3(HOLE_HALF_W * 2f + 0.6f, 0.3f, 0.5f),
                FindMat("M_Ch4_Metal"), rt, "门框横梁");

            Log($"  通道: z[{COR_Z0:F1},{COR_Z1:F1}] 长 {len:F1}m  宽 {COR_HALF_IN * 2f:F1}m  高 {COR_H}");
            return 1;
        }

        // ═══════════════════════════════════════════════════════════
        private static int BuildLights()
        {
            var root = GameObject.Find("--- EXIT_CORRIDOR_CH4 ---");
            if (root == null) return 0;

            var lightRoot = root.transform.Find("Lights");
            if (lightRoot == null)
            {
                var lg = new GameObject("Lights");
                lg.transform.SetParent(root.transform, false);
                lightRoot = lg.transform;
            }

            int created = 0;
            // 每 4 米一盏暗红灯
            for (float z = COR_Z0 + 2f; z < COR_Z1; z += 4f)
            {
                string nm = $"Light_Exit_{Mathf.RoundToInt(z)}";
                if (lightRoot.Find(nm) != null) continue;

                var go = new GameObject(nm);
                go.transform.SetParent(lightRoot, false);
                go.transform.position = new Vector3(0f, COR_H - 0.6f, z);

                var l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(1.0f, 0.30f, 0.22f);   // 暗红，呼应 M_Ch4_Flesh
                l.intensity = 3.0f;
                l.range = 7f;
                l.shadows = LightShadows.None;             // 省性能
                l.renderMode = LightRenderMode.ForcePixel;

                created++;
            }

            if (created > 0) Log($"  通道照明: 新建 {created} 盏暗红灯");
            return created > 0 ? 1 : 0;
        }

        // ═══════════════════════════════════════════════════════════
        private static int MakeBox(string name, Transform parent, Vector3 center, Vector3 size,
                                    Material mat, string note)
        {
            var existing = GameObject.Find(name);
            if (existing != null) Object.DestroyImmediate(existing);

            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = center;

            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mf.sharedMesh = GetCubeMesh();
            if (mat != null) mr.sharedMaterial = mat;

            var bc = go.AddComponent<BoxCollider>();
            bc.size = Vector3.one;

            go.transform.localScale = size;

            if (!string.IsNullOrEmpty(note)) Log($"    {name} @ {center} size={size}  {note}");
            return 1;
        }

        private static int MakeBoxIn(string name, Vector3 center, Vector3 size,
                                      Material mat, Transform parent, string note)
        {
            var existing = parent.Find(name);
            if (existing != null) return 0;   // 已存在，幂等

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = center;

            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mf.sharedMesh = GetCubeMesh();
            if (mat != null) mr.sharedMaterial = mat;

            var bc = go.AddComponent<BoxCollider>();
            bc.size = Vector3.one;

            go.transform.localScale = size;

            if (!string.IsNullOrEmpty(note)) Log($"    {name} @ {center} size={size}  {note}");
            return 1;
        }

        private static Mesh _cube;
        private static Mesh GetCubeMesh()
        {
            if (_cube != null) return _cube;
            // 用 GameObject.CreatePrimitive 取内置 Cube mesh，然后销毁临时对象
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _cube = tmp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(tmp);
            return _cube;
        }

        private static Material FindMat(string name)
        {
            // 先找已加载的
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                if (r.sharedMaterial != null && r.sharedMaterial.name == name)
                    return r.sharedMaterial;

            // 再从资产库找
            var guids = AssetDatabase.FindAssets($"t:Material {name}");
            foreach (var g in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (Path.GetFileNameWithoutExtension(p) == name)
                    return AssetDatabase.LoadAssetAtPath<Material>(p);
            }
            return null;
        }

        // ═══════════════════════════════════════════════════════════
        private static void Verify()
        {
            Log("---- 验证 ----");
            Physics.SyncTransforms();

            Log($"  North_G7_Full 存在 = {GameObject.Find("North_G7_Full") != null}（应为 False）");
            foreach (var n in new[] { "North_G7_Left", "North_G7_Right", "North_G7_Top" })
            {
                var o = GameObject.Find(n);
                if (o == null) { Log($"  ❌ {n} 缺失"); continue; }
                var c = o.GetComponent<BoxCollider>();
                Log($"  {n}: 包围盒 x[{c.bounds.min.x:F2},{c.bounds.max.x:F2}] " +
                    $"y[{c.bounds.min.y:F2},{c.bounds.max.y:F2}] z[{c.bounds.min.z:F2},{c.bounds.max.z:F2}]");
            }

            // 门洞扫描
            Log("  门洞扫描（z=25.05，y=1.0）：");
            var sb = new StringBuilder();
            for (float x = -2.5f; x <= 2.5f; x += 0.5f)
            {
                bool hit = Physics.CheckSphere(new Vector3(x, 1.0f, 25.05f), 0.2f, ~0, QueryTriggerInteraction.Ignore);
                sb.Append($"{x,5:F1}:{(hit ? "■" : "□")} ");
            }
            Log("    " + sb);

            // 通道地板
            Log("  通道地板连续性（x=0）：");
            sb.Clear();
            for (float z = 25.5f; z <= 42f; z += 2f)
            {
                bool f = Physics.Raycast(new Vector3(0, 2f, z), Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore);
                sb.Append($"{z:F0}:{(f ? "有" : "无")} ");
            }
            Log("    " + sb);

            // 通道内是否有墙挡
            Log("  通道中央净空检测（y=1.0）：");
            sb.Clear();
            for (float z = 26f; z <= 41f; z += 3f)
            {
                bool wall = Physics.CheckSphere(new Vector3(0, 1.0f, z), 0.35f, ~0, QueryTriggerInteraction.Ignore);
                sb.Append($"{z:F0}:{(wall ? "挡" : "通")} ");
            }
            Log("    " + sb);

            // 触发区
            var ending = Object.FindFirstObjectByType<AbyssEcho.Narrative.ChapterEndingSystem>();
            if (ending != null)
            {
                var tcF = typeof(AbyssEcho.Narrative.ChapterEndingSystem).GetField("triggerCenter");
                var tsF = typeof(AbyssEcho.Narrative.ChapterEndingSystem).GetField("triggerSize");
                Vector3 tc = (Vector3)tcF.GetValue(ending);
                Vector3 ts = (Vector3)tsF.GetValue(ending);
                Log($"  结局触发区: center={tc} size={ts} → Z[{tc.z - ts.z * 0.5f:F1},{tc.z + ts.z * 0.5f:F1}]");
                bool inside = tc.z - ts.z * 0.5f >= COR_Z0 && tc.z + ts.z * 0.5f <= COR_Z1;
                Log($"  触发区是否完全落在通道内 = {(inside ? "✅" : "❌")}");
            }

            Log("完成");
        }

        private static void Log(string msg)
        {
            string line = $"[Ch4Exit] {msg}";
            Debug.Log(line);
            Write(line);
        }

        private static void Write(string line)
        {
            try
            {
                string dir = Path.GetFullPath(Path.Combine(
                    Path.GetDirectoryName(Application.dataPath), "../Reports"));
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "Ch4ExitFix_Result.txt"),
                                   line + "\n", new System.Text.UTF8Encoding(false));
            }
            catch { }
        }
    }
}