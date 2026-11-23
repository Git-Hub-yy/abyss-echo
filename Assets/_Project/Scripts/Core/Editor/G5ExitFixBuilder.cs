using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S59：修复 G5 出不去的死路（**最小改动，不重建场景**）。
    ///
    /// ══════════════════ 问题 ══════════════════
    ///
    ///   玩家在 G5 里四面全封，走不出去，所以"找不到去 G7 的门"。
    ///
    ///   实测证据：
    ///     · G5 范围 x[-25,-13] z[-5,5]
    ///     · G5 北墙 North_G5_Full x[-25,-13] z[4.9,5.2] 整条封死
    ///     · G5 东墙 East_G5_Seg0 x[-13.2,-12.9] z[-5,6.8] 整条封死
    ///     · G5 地板止于 z=5，连廊地板从 z=6.9 开始 → 中间 1.9 米虚空
    ///     · S44 写的 AddDoor(g5, East, 8f) 把门洞切在 z[6.8,9.2]，
    ///       完全在 G5 房间外面，G5 的东墙因此变成一整条实心墙
    ///
    /// ══════════════════ 修复 ══════════════════
    ///
    ///   不走 ChapterFourBuilder（那是 NewScene 清空重建，
    ///   会把门/通道/谜题感应区/照明全部冲掉）。
    ///
    ///   只做两件事：
    ///     ① 拆掉 G5 北墙 North_G5_Full，
    ///        换成 左段 / 右段 / 门楣 三段，中间留 2.4 米门洞（世界 x 中心 -19）
    ///     ② 把 G5_G7_Link 连廊的地板/天花板/侧墙向南延伸到 z=5，
    ///        与 G5 北墙门洞无缝衔接（消灭 z[5,6.9] 的虚空）
    ///
    ///   全程不 NewScene、不动 G7、不动玩家、不动谜题。
    /// </summary>
    public static class G5ExitFixBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter4_Graybox.unity";

        // G5 几何
        private const float G5_X0 = -25f, G5_X1 = -13f;
        private const float G5_Z_N = 5.0f;          // G5 北边界
        private const float G5_H = 5.0f;

        // 门洞（G5 北墙，世界 x 中心 = G5 中心 = -19）
        private const float DOOR_CX = -19f;
        private const float DOOR_W = 2.4f;
        private const float DOOR_H = 3.0f;

        // 连廊
        private const float LINK_Z_S = 5.0f;        // 南端（接到 G5）
        private const float LINK_Z_N = 9.2f;        // 北端（保持原样）
        private const float LINK_X0 = -13.2f, LINK_X1 = -9.9f;
        private const float LINK_H = 3.4f;

        [MenuItem("ABYSS ECHO/修复 v6.5/第四章 G5 出口打通")]
        public static void Run() => Fix();

        public static void Fix()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Log("已打开场景");
            Physics.SyncTransforms();

            int n = 0;
            n += SplitNorthWall();
            n += ExtendLink();

            if (n > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                bool ok = EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Log(ok ? "场景已保存" : "❌ 保存失败");
            }

            Verify();
        }

        // ══════════════════════════════════════════════════════
        private static int SplitNorthWall()
        {
            var old = GameObject.Find("North_G5_Full");
            if (old == null) { Log("  North_G5_Full 不存在（可能已修）"); return 0; }

            var bc = old.GetComponent<BoxCollider>();
            var mr = old.GetComponent<MeshRenderer>();
            var b = bc.bounds;
            Material mat = mr != null ? mr.sharedMaterial : null;
            var parent = old.transform.parent;

            Log($"  原北墙 x[{b.min.x:F2},{b.max.x:F2}] y[{b.min.y:F2},{b.max.y:F2}] z[{b.min.z:F2},{b.max.z:F2}]");

            float xMin = b.min.x, xMax = b.max.x;
            float zMid = b.center.z, zSize = b.size.z;
            float ySize = b.size.y;
            float dL = DOOR_CX - DOOR_W * 0.5f;   // -20.2
            float dR = DOOR_CX + DOOR_W * 0.5f;   // -17.8

            // 左段 x[xMin, dL]
            MakeBox("North_G5_Left", parent,
                new Vector3((xMin + dL) * 0.5f, ySize * 0.5f, zMid),
                new Vector3(dL - xMin, ySize, zSize), mat);

            // 右段 x[dR, xMax]
            MakeBox("North_G5_Right", parent,
                new Vector3((dR + xMax) * 0.5f, ySize * 0.5f, zMid),
                new Vector3(xMax - dR, ySize, zSize), mat);

            // 门楣 x[dL, dR] y[DOOR_H, ySize]
            float lintelH = ySize - DOOR_H;
            MakeBox("North_G5_Top", parent,
                new Vector3(DOOR_CX, DOOR_H + lintelH * 0.5f, zMid),
                new Vector3(DOOR_W, lintelH, zSize), mat);

            Object.DestroyImmediate(old);
            Log($"  ✅ G5 北墙拆成 3 段，门洞 x[{dL:F1},{dR:F1}] 宽{DOOR_W} 高{DOOR_H}");
            return 1;
        }

        // ══════════════════════════════════════════════════════
        private static int ExtendLink()
        {
            // 实际房间节点名是 "Room_G5_G7_Link_核心西门"
            var linkRoot = GameObject.Find("Room_G5_G7_Link_核心西门");
            if (linkRoot == null)
            {
                foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                    if (t.name.Contains("G5_G7_Link") && !t.name.Contains("Props"))
                    { linkRoot = t.gameObject; break; }
            }
            if (linkRoot == null) { Log("  ❌ 找不到 G5_G7_Link 房间节点"); return 0; }

            Log($"  G5_G7_Link @ {linkRoot.transform.position}");

            // 现有对象
            var floor = FindChild(linkRoot.transform, "Floor_G5_G7_Link");
            var ceil = FindChild(linkRoot.transform, "Ceiling_G5_G7_Link");
            var southWall = FindChild(linkRoot.transform, "South_G5_G7_Link_Full");

            // 目标：连廊向南延伸到 z=LINK_Z_S
            float oldZ0 = 6.9f;   // 原南端
            float extend = oldZ0 - LINK_Z_S;   // 1.9

            // ① 地板延长
            if (floor != null)
            {
                var bc = floor.GetComponent<BoxCollider>();
                var b = bc.bounds;
                if (b.min.z > LINK_Z_S + 0.05f)
                {
                    float newMin = LINK_Z_S;
                    float newMax = b.max.z;
                    floor.transform.position = new Vector3(b.center.x, b.center.y, (newMin + newMax) * 0.5f);
                    floor.transform.localScale = new Vector3(b.size.x, b.size.y, newMax - newMin);
                    Log($"    Floor_G5_G7_Link 延长到 z[{newMin:F1},{newMax:F1}]");
                }
            }
            else
            {
                MakeBoxIn("Floor_G5_G7_Link_Ext", linkRoot.transform,
                    new Vector3((LINK_X0 + LINK_X1) * 0.5f, -0.25f, (LINK_Z_S + oldZ0) * 0.5f),
                    new Vector3(LINK_X1 - LINK_X0, 0.5f, extend),
                    FindMat("M_Ch4_Bone"));
            }

            // ② 天花板延长
            if (ceil != null)
            {
                var b = ceil.GetComponent<BoxCollider>().bounds;
                if (b.min.z > LINK_Z_S + 0.05f)
                {
                    ceil.transform.position = new Vector3(b.center.x, b.center.y, (LINK_Z_S + b.max.z) * 0.5f);
                    ceil.transform.localScale = new Vector3(b.size.x, b.size.y, b.max.z - LINK_Z_S);
                    Log($"    Ceiling_G5_G7_Link 延长到 z[{LINK_Z_S:F1},{b.max.z:F1}]");
                }
            }

            // ③ 删掉连廊南端的封墙（South_G5_G7_Link_Full 本来挡在 z[6.9,7.2]）
            //    延长后这段墙会横在通道中间，必须去掉
            if (southWall != null)
            {
                Log($"    删除连廊封墙 South_G5_G7_Link_Full");
                Object.DestroyImmediate(southWall.gameObject);
            }

            // ④ 补两侧墙（延长段）
            float zMid = (LINK_Z_S + oldZ0) * 0.5f;
            float zLen = extend;
            MakeBoxIn("West_G5_G7_Link_Ext", linkRoot.transform,
                new Vector3(LINK_X0, LINK_H * 0.5f, zMid),
                new Vector3(0.3f, LINK_H, zLen), FindMat("M_Ch4_FleshDark"));
            MakeBoxIn("East_G5_G7_Link_Ext", linkRoot.transform,
                new Vector3(LINK_X1, LINK_H * 0.5f, zMid),
                new Vector3(0.3f, LINK_H, zLen), FindMat("M_Ch4_FleshDark"));

            Log($"  ✅ 连廊延长到 z[{LINK_Z_S:F1},{LINK_Z_N:F1}]");
            return 1;
        }

        // ══════════════════════════════════════════════════════
        private static GameObject FindChild(Transform parent, string name)
        {
            var t = parent.Find(name);
            if (t != null) return t.gameObject;
            foreach (var tt in parent.GetComponentsInChildren<Transform>(true))
                if (tt.name == name) return tt.gameObject;
            // 再全局找
            foreach (var tt in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (tt.name == name) return tt.gameObject;
            return null;
        }

        private static void MakeBox(string name, Transform parent, Vector3 center, Vector3 size, Material mat)
        {
            var old = GameObject.Find(name);
            if (old != null) Object.DestroyImmediate(old);
            MakeBoxIn(name, parent, center, size, mat);
        }

        private static void MakeBoxIn(string name, Transform parent, Vector3 center, Vector3 size, Material mat)
        {
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
            Log($"      + {name} @ {center} size={size}");
        }

        private static Mesh _cube;
        private static Mesh GetCubeMesh()
        {
            if (_cube != null) return _cube;
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _cube = tmp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(tmp);
            return _cube;
        }

        private static Material FindMat(string name)
        {
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                if (r.sharedMaterial != null && r.sharedMaterial.name == name) return r.sharedMaterial;
            foreach (var g in AssetDatabase.FindAssets($"t:Material {name}"))
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (Path.GetFileNameWithoutExtension(p) == name)
                    return AssetDatabase.LoadAssetAtPath<Material>(p);
            }
            return null;
        }

        // ══════════════════════════════════════════════════════
        private static void Verify()
        {
            Log("---- 验证 ----");
            Physics.SyncTransforms();

            Log($"  North_G5_Full 还在 = {GameObject.Find("North_G5_Full") != null}（应 False）");

            // G5 北墙门洞扫描
            Log("  G5 北墙 z=5 扫描（玩家胶囊，y=1.0）：");
            var sb = new StringBuilder();
            for (float x = -21f; x <= -17f; x += 0.5f)
            {
                bool w = Physics.CheckCapsule(new Vector3(x, 0.35f, 5f), new Vector3(x, 1.45f, 5f),
                                              0.35f, ~0, QueryTriggerInteraction.Ignore);
                sb.Append($"{x:F1}:{(w ? "■" : "□")} ");
            }
            Log("    " + sb);

            // G5 走到连廊的地板连续性
            Log("  地板连续性 x=-19：");
            sb.Clear();
            for (float z = 3f; z <= 10f; z += 1f)
            {
                bool f = Physics.Raycast(new Vector3(-19, 2f, z), Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore);
                sb.Append($"{z:F0}:{(f ? "有" : "无")} ");
            }
            Log("    " + sb);

            // 通行测试
            Log("  通行测试（玩家胶囊）：");
            float[,] pairs = {
                { -19f, 3f, -19f, 6f },
                { -19f, 6f, -17f, 7.5f },
                { -17f, 7.5f, -14f, 8f },
                { -14f, 8f, -11.5f, 8f },
                { -11.5f, 8f, -9.5f, 8.5f },
            };
            for (int i = 0; i < 5; i++)
            {
                Vector3 a = new Vector3(pairs[i, 0], 0, pairs[i, 1]);
                Vector3 b2 = new Vector3(pairs[i, 2], 0, pairs[i, 3]);
                Vector3 dir = (b2 - a).normalized;
                float d = Vector3.Distance(a, b2);
                bool blk = Physics.CapsuleCast(new Vector3(a.x, 0.35f, a.z), new Vector3(a.x, 1.45f, a.z),
                                               0.35f, dir, out var hit, d, ~0, QueryTriggerInteraction.Ignore);
                Log($"    ({a.x:F0},{a.z:F1}) → ({b2.x:F0},{b2.z:F1})  {(blk ? "❌ " + hit.collider.gameObject.name : "✅")}");
            }

            Log("完成");
        }

        private static void Log(string msg)
        {
            string line = $"[G5Fix] {msg}";
            Debug.Log(line);
            try
            {
                string dir = @"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports";
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "G5ExitFix_Result.txt"),
                                   line + "\n", new UTF8Encoding(false));
            }
            catch { }
        }
    }
}