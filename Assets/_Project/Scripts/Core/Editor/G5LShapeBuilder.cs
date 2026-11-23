using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S61：把 G5 出口做成**看得见、走得通的 L 形通廊**。
    ///
    /// ══════════════════ 现状问题 ══════════════════
    ///
    ///   几何上：G5 北门洞(-19,5) → 过渡区(x[-20.2,-9.9] z[5,6.9]) → 连廊(x[-13.2,-9.9] z[5,9.2]) → G7
    ///   实测 195 个采样点全部可走。
    ///
    ///   但**视觉上像死路**：
    ///     玩家站在 G5 北门洞朝北看，1.9 米处就是 Transit_Wall_North（z=6.9），
    ///     看起来门洞后面就是一堵墙，不像通道。
    ///     玩家必须"出门后向东转 90°"，但没有任何视觉提示。
    ///
    ///   而这堵墙**又是必须的**：
    ///     过渡区北边 x[-20.2,-13.2] z>6.9 是虚空（无地板），
    ///     去掉墙玩家会掉下去。
    ///
    /// ══════════════════ 修复 ══════════════════
    ///
    ///   把过渡区向北扩成 L 形，让玩家出门洞后**能直接看到前方向东延伸的通廊**：
    ///
    ///     ① 新增 Transition_Floor_N：x[-20.2,-13.2] z[6.9,9.2]
    ///        → 把过渡区和连廊北段在 z 方向对齐（都是 z[5,9.2]）
    ///     ② 新增 Transition_Ceiling_N：同范围，deca y=3.4
    ///     ③ Transit_Wall_North 向北移到 z=9.2（成为新 L 区的北墙）
    ///     ④ 新增 Transition_Wall_West_N：x=-20.2 处封西侧
    ///     ⑤ 过渡区加一盏灯，照亮出口方向
    ///
    ///   这样玩家从门洞出来，前方是一个 10 米进深、向东敞开的空间，
    ///   一眼能看出"要往东边那条通廊走"。
    /// </summary>
    public static class G5LShapeBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter4_Graybox.unity";

        // L 形扩展区
        private const float EX0 = -20.2f, EX1 = -13.2f;   // 与过渡区西沿 / 连廊西沿对齐
        private const float EZ0 = 6.9f, EZ1 = 9.2f;       // 与连廊北端对齐
        private const float EH = 3.4f;

        [MenuItem("ABYSS ECHO/修复 v6.7/第四章 G5 出口 L 形通廊")]
        public static void Build() => Run();

        public static void Run()
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

            float cx = (EX0 + EX1) * 0.5f;
            float cz = (EZ0 + EZ1) * 0.5f;
            float sx = EX1 - EX0;
            float sz = EZ1 - EZ0;

            int n = 0;

            // ① L 区地板
            n += MakeBoxIn("Transit_Floor_North", parent.transform,
                new Vector3(cx, -0.25f, cz), new Vector3(sx, 0.5f, sz), floorMat, "L区北段地板");

            // ② L 区天花板
            n += MakeBoxIn("Transit_Ceiling_North", parent.transform,
                new Vector3(cx, EH + 0.15f, cz), new Vector3(sx, 0.3f, sz), ceilMat, "L区北段天花板");

            // ③ 把原 Transit_Wall_North 向北移到 z=9.2 之外（成为 L 区北墙）
            var oldNorth = GameObject.Find("Transit_Wall_North");
            if (oldNorth != null)
            {
                Object.DestroyImmediate(oldNorth);
                Log("  删除原 Transit_Wall_North（会挡住视线）");
                n++;
            }
            n += MakeBoxIn("Transit_Wall_North_End", parent.transform,
                new Vector3(cx, EH * 0.5f, EZ1 + 0.15f), new Vector3(sx, EH, 0.3f), wallMat, "L区北端封墙");

            // ④ L 区西墙
            n += MakeBoxIn("Transit_Wall_West_North", parent.transform,
                new Vector3(EX0 - 0.15f, EH * 0.5f, cz), new Vector3(0.3f, EH, sz), wallMat, "L区西墙");

            // ⑤ 过渡区补一盏灯，照亮出口
            var lightRoot = parent.transform.Find("Lights");
            if (lightRoot == null)
            {
                var lg = new GameObject("Lights");
                lg.transform.SetParent(parent.transform, false);
                lightRoot = lg.transform;
            }
            if (lightRoot.Find("Light_Transit") == null)
            {
                var go = new GameObject("Light_Transit");
                go.transform.SetParent(lightRoot, false);
                go.transform.position = new Vector3(cx, EH - 0.5f, cz);
                var l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(1.0f, 0.35f, 0.22f);
                l.intensity = 4.5f;
                l.range = 9f;
                l.shadows = LightShadows.None;
                l.renderMode = LightRenderMode.ForcePixel;
                Log("    + Light_Transit");
                n++;
            }

            // ⑥ 过渡区（z[5,6.9]）也补一盏，让门洞内外亮度一致
            if (lightRoot.Find("Light_Transit_S") == null)
            {
                var go = new GameObject("Light_Transit_S");
                go.transform.SetParent(lightRoot, false);
                go.transform.position = new Vector3(-16.5f, EH - 0.5f, 5.95f);
                var l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(1.0f, 0.35f, 0.22f);
                l.intensity = 4f;
                l.range = 8f;
                l.shadows = LightShadows.None;
                l.renderMode = LightRenderMode.ForcePixel;
                Log("    + Light_Transit_S");
                n++;
            }

            if (n > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                bool ok = EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Log(ok ? $"场景已保存（{n} 项）" : "❌ 保存失败");
            }

            Verify();
        }

        private static void Verify()
        {
            Log("---- 验证 ----");
            Physics.SyncTransforms();

            // 地板连续性
            Log("地板（x=-19 门洞正前方，向北）：");
            for (float z = 5f; z <= 9.5f; z += 0.5f)
            {
                bool f = Physics.Raycast(new Vector3(-19, 2f, z), Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore);
                string nm = "";
                if (f) { Physics.Raycast(new Vector3(-19,2f,z), Vector3.down, out var h, 4f, ~0, QueryTriggerInteraction.Ignore); nm = h.collider.gameObject.name; }
                Log($"  z={z,4:F1}  {(f ? "✅ " + nm : "❌ 无地板")}");
            }

            Log("地板（x=-16 z=8，向东）：");
            for (float x = -19f; x <= -9f; x += 1f)
            {
                bool f = Physics.Raycast(new Vector3(x, 2f, 8f), Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore);
                Log($"  x={x,6:F1}  {(f ? "✅ 有地板" : "❌ 无地板")}");
            }

            // 通行
            Log("玩家胶囊通行（门洞→L区→连廊→G7）：");
            var path = new[]
            {
                new Vector3(-19, 0, 4f),
                new Vector3(-19, 0, 6f),
                new Vector3(-17, 0, 7.5f),
                new Vector3(-15, 0, 8.2f),
                new Vector3(-13, 0, 8.2f),
                new Vector3(-11.5f, 0, 8.2f),
                new Vector3(-10, 0, 8.2f),
                new Vector3(-9, 0, 8.5f),
            };
            for (int i = 0; i < path.Length - 1; i++)
            {
                Vector3 a = path[i], b = path[i + 1];
                Vector3 dir = (b - a).normalized; dir.y = 0;
                float d = Vector3.Distance(new Vector3(a.x,0,a.z), new Vector3(b.x,0,b.z));
                int steps = Mathf.Max(1, Mathf.CeilToInt(d / 0.12f));
                int bad = 0; string fb = "";
                for (int k = 0; k <= steps; k++)
                {
                    Vector3 p = Vector3.Lerp(new Vector3(a.x,0,a.z), new Vector3(b.x,0,b.z), (float)k/steps);
                    bool floor = Physics.Raycast(new Vector3(p.x,2f,p.z), Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore);
                    bool blk = false; string who = "";
                    if (floor) {
                        foreach (var h in Physics.OverlapCapsule(new Vector3(p.x,0.35f,p.z), new Vector3(p.x,1.45f,p.z), 0.35f, ~0, QueryTriggerInteraction.Ignore)) {
                            if (h == null || h.isTrigger) continue;
                            if (h.gameObject.name.Contains("Floor")) continue;
                            if (h.bounds.max.y < 0.2f) continue;
                            blk = true; who = h.gameObject.name; break;
                        }
                    }
                    if (!floor || blk) { bad++; if (fb=="") fb = $"({p.x:F1},{p.z:F1}){(floor?"被"+who:"无地板")}"; }
                }
                Log($"  ({a.x,6:F1},{a.z,4:F1}) → ({b.x,6:F1},{b.z,4:F1})  {steps+1,3}点  {(bad==0?"✅":"❌"+bad+"点 "+fb)}");
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
            string line = $"[G5LShape] {msg}";
            Debug.Log(line);
            try
            {
                Directory.CreateDirectory(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports");
                File.AppendAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\G5LShape_Result.txt",
                                   line + "\n", new UTF8Encoding(false));
            }
            catch { }
        }
    }
}