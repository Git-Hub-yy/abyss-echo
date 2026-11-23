using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S59b：补齐 G5 北门洞 → 连廊之间的 L 形过渡地板。
    ///
    /// ══════════════════ 现状 ══════════════════
    ///
    ///   门洞（G5 北墙）：x[-20.2, -17.8]  z=5
    ///   连廊（G5_G7_Link）：x[-13.2, -9.9]  z[5.0, 9.2]
    ///
    ///   X 方向差 4.6 米 —— 玩家出门洞后必须先向东走，
    ///   但中间 x[-17.8, -13.2] z[5, 6.9] 没有地板，会踩空。
    ///
    /// ══════════════════ 修复 ══════════════════
    ///
    ///   补一块过渡地面：x[-20.2, -9.9] z[5.0, 6.9]
    ///     顶面 y=0（与 G5 / 连廊完全平齐）
    ///   补对应天花板：x[-20.2, -9.9] z[5.0, 6.9]（底面 y=3.4，与连廊一致）
    ///   补北侧墙：    x[-20.2, -13.2] z[6.9, 7.2]（把过渡区北边封住）
    ///   补西侧墙：    x[-20.5, -20.2] z[5.0, 6.9]（封住过渡区西边）
    ///
    ///   不 NewScene、不动 G7、不动玩家、不动谜题、不动怪物。
    /// </summary>
    public static class G5LFloorBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter4_Graybox.unity";

        // 过渡区
        private const float TX0 = -20.2f, TX1 = -9.9f;   // 与门洞左沿 / 连廊右沿对齐
        private const float TZ0 = 5.0f, TZ1 = 6.9f;      // 从 G5 北墙到连廊南端
        private const float TH = 3.4f;                    // 与连廊同高

        [MenuItem("ABYSS ECHO/修复 v6.5/第四章 G5 过渡地板")]
        public static void Run() => Fix();

        public static void Fix()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Log("已打开场景");
            Physics.SyncTransforms();

            var parent = GameObject.Find("Room_G5_G7_Link_核心西门");
            if (parent == null)
            {
                foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                    if (t.name.Contains("G5_G7_Link") && !t.name.Contains("Props"))
                    { parent = t.gameObject; break; }
            }
            if (parent == null) { Log("❌ 找不到连廊节点"); return; }

            Material floorMat = FindMat("M_Ch4_Bone");
            Material wallMat = FindMat("M_Ch4_FleshDark");
            Material ceilMat = FindMat("M_Ch4_Vein");

            float cx = (TX0 + TX1) * 0.5f;
            float cz = (TZ0 + TZ1) * 0.5f;
            float sx = TX1 - TX0;
            float sz = TZ1 - TZ0;

            int n = 0;

            // ① 过渡地板（顶面 y=0）
            n += MakeBoxIn("Transit_Floor", parent.transform,
                new Vector3(cx, -0.25f, cz), new Vector3(sx, 0.5f, sz), floorMat, "过渡地板 顶面y=0");

            // ② 过渡天花板（底面 y=TH）
            n += MakeBoxIn("Transit_Ceiling", parent.transform,
                new Vector3(cx, TH + 0.15f, cz), new Vector3(sx, 0.3f, sz), ceilMat, $"过渡天花板 底面y={TH}");

            // ③ 北墙（封住过渡区北边，x 从 TX0 到连廊左沿 -13.2）
            float nwX1 = -13.2f;
            n += MakeBoxIn("Transit_Wall_North", parent.transform,
                new Vector3((TX0 + nwX1) * 0.5f, TH * 0.5f, TZ1 + 0.15f),
                new Vector3(nwX1 - TX0, TH, 0.3f), wallMat, "过渡区北墙");

            // ④ 西墙（封住过渡区西边）
            n += MakeBoxIn("Transit_Wall_West", parent.transform,
                new Vector3(TX0 - 0.15f, TH * 0.5f, cz),
                new Vector3(0.3f, TH, sz), wallMat, "过渡区西墙");

            if (n > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                bool ok = EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Log(ok ? $"场景已保存（{n} 个新对象）" : "❌ 保存失败");
            }
            else Log("全部已存在，无改动");

            Verify();
        }

        private static void Verify()
        {
            Log("---- 验证 ----");
            Physics.SyncTransforms();
            var sb = new StringBuilder();

            // 走完整路径
            Log("真实路径地板连续性：");
            var path = new[]
            {
                new Vector3(-19, 0, 4),
                new Vector3(-19, 0, 5),
                new Vector3(-19, 0, 5.5f),
                new Vector3(-18, 0, 6),
                new Vector3(-16, 0, 6.5f),
                new Vector3(-14, 0, 6.8f),
                new Vector3(-12.5f, 0, 7.5f),
                new Vector3(-11.5f, 0, 8),
                new Vector3(-9.5f, 0, 8),
            };
            int gaps = 0;
            foreach (var p in path)
            {
                bool f = Physics.Raycast(p + Vector3.up * 2f, Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore);
                string nm = "";
                if (f) { Physics.Raycast(p + Vector3.up * 2f, Vector3.down, out var h, 4f, ~0, QueryTriggerInteraction.Ignore); nm = h.collider.gameObject.name; }
                else gaps++;
                Log($"  ({p.x,6:F1},{p.z,4:F1})  {(f ? "✅ " + nm : "❌ 无地板")}");
            }
            Log($"  缺口数 = {gaps}");

            // 玩家胶囊通行
            Log("玩家胶囊逐段通行：");
            for (int i = 0; i < path.Length - 1; i++)
            {
                Vector3 a = path[i], b = path[i + 1];
                Vector3 dir = (b - a).normalized;
                dir.y = 0;
                float d = Vector3.Distance(new Vector3(a.x, 0, a.z), new Vector3(b.x, 0, b.z));
                bool blk = Physics.CapsuleCast(
                    new Vector3(a.x, 0.35f, a.z), new Vector3(a.x, 1.45f, a.z),
                    0.35f, dir, out var hit, d, ~0, QueryTriggerInteraction.Ignore);
                Log($"  ({a.x,6:F1},{a.z,4:F1}) → ({b.x,6:F1},{b.z,4:F1})  {d,4:F1}m  {(blk ? "❌ " + hit.collider.gameObject.name : "✅")}");
            }
            Log("完成");
        }

        private static int MakeBoxIn(string name, Transform parent, Vector3 center, Vector3 size,
                                      Material mat, string note)
        {
            var ex = parent.Find(name);
            if (ex != null) return 0;

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

            Log($"    + {name} @ {center} size={size}  {note}");
            return 1;
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

        private static void Log(string msg)
        {
            string line = $"[G5L] {msg}";
            Debug.Log(line);
            try
            {
                Directory.CreateDirectory(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports");
                File.AppendAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\G5LFloor_Result.txt",
                                   line + "\n", new UTF8Encoding(false));
            }
            catch { }
        }
    }
}