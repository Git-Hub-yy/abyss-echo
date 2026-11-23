using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S62：给 G5 北门洞加门框 + 指引光，让玩家一眼看出"这里能走"。
    ///
    /// ══════════════════ 为什么需要 ══════════════════
    ///
    ///   像素检测显示：站在 G5 中心朝北看，画面中央亮度 0.44，
    ///   左侧墙面 0.199 —— 中央确实有亮区。
    ///   但那主要是**手电筒打在墙上的光斑**，门洞本身的轮廓看不见，
    ///   因为门洞里面和墙面都是同一材质的暗红，边界完全糊在一起。
    ///
    /// ══════════════════ 修复 ══════════════════
    ///
    ///   ① 门框：用 M_Ch4_Metal（和其他门框一致），
    ///      左右两根立柱 + 上方横梁，形成明确的矩形边界
    ///   ② 门洞内壁：给洞口加一圈"内衬"（薄板），
    ///      从 G5 侧看就是一圈金属边，一眼能认出是通道口
    ///   ③ 指引光：门洞正后方 1.5 米加一盏暖光，
    ///      让洞口透出光，比周围墙面亮 → "里面有路"
    ///
    ///   不改地、不改通行性，纯视觉增强。
    /// </summary>
    public static class G5DoorFrameBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter4_Graybox.unity";

        // 门洞（G5 北墙）
        private const float DOOR_CX = -19.0f;
        private const float DOOR_W = 2.4f;
        private const float DOOR_H = 3.0f;
        private const float WALL_Z = 5.0f;      // 墙中心 z
        private const float WALL_T = 0.30f;     // 墙厚

        [MenuItem("ABYSS ECHO/修复 v6.8/第四章 G5 门洞加门框")]
        public static void Build() => Run();

        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Log("已打开场景");
            Physics.SyncTransforms();

            var roomRoot = GameObject.Find("Room_G5_组织层");
            if (roomRoot == null)
                roomRoot = GameObject.Find("Room_G5");

            var holder = GameObject.Find("--- G5_DOOR_FRAME ---");
            if (holder == null)
            {
                holder = new GameObject("--- G5_DOOR_FRAME ---");
                Log("+ 创建 --- G5_DOOR_FRAME ---");
            }

            Material metal = FindMat("M_Ch4_Metal");
            Material flesh = FindMat("M_Ch4_FleshDark");

            float halfW = DOOR_W * 0.5f;
            float zFront = WALL_Z - WALL_T * 0.5f - 0.02f;   // 墙的 G5 侧表面
            float zBack = WALL_Z + WALL_T * 0.5f + 0.02f;    // 墙的北侧表面
            int n = 0;

            // ① 左右立柱（朝向 G5 的一面）
            n += MakeBox("G5Door_Frame_L", holder.transform,
                new Vector3(DOOR_CX - halfW - 0.12f, DOOR_H * 0.5f, zFront),
                new Vector3(0.24f, DOOR_H, 0.12f), metal, "左立柱");
            n += MakeBox("G5Door_Frame_R", holder.transform,
                new Vector3(DOOR_CX + halfW + 0.12f, DOOR_H * 0.5f, zFront),
                new Vector3(0.24f, DOOR_H, 0.12f), metal, "右立柱");

            // ② 上方横梁
            n += MakeBox("G5Door_Frame_Top", holder.transform,
                new Vector3(DOOR_CX, DOOR_H + 0.12f, zFront),
                new Vector3(DOOR_W + 0.48f, 0.24f, 0.12f), metal, "上横梁");

            // ③ 洞内衬（把洞口内侧刷成金属，形成深度感）
            //    左右内壁
            n += MakeBox("G5Door_Lining_L", holder.transform,
                new Vector3(DOOR_CX - halfW + 0.03f, DOOR_H * 0.5f, WALL_Z),
                new Vector3(0.06f, DOOR_H, WALL_T), metal, "左内衬");
            n += MakeBox("G5Door_Lining_R", holder.transform,
                new Vector3(DOOR_CX + halfW - 0.03f, DOOR_H * 0.5f, WALL_Z),
                new Vector3(0.06f, DOOR_H, WALL_T), metal, "右内衬");
            //    顶内衬
            n += MakeBox("G5Door_Lining_T", holder.transform,
                new Vector3(DOOR_CX, DOOR_H - 0.03f, WALL_Z),
                new Vector3(DOOR_W, 0.06f, WALL_T), metal, "顶内衬");

            // ④ 地面门槛（低位，不挡路，只是视觉标记）
            n += MakeBox("G5Door_Threshold", holder.transform,
                new Vector3(DOOR_CX, 0.02f, WALL_Z),
                new Vector3(DOOR_W + 0.48f, 0.04f, WALL_T + 0.1f), flesh, "门槛");

            // ⑤ 指引光：门洞正后方 1.2 米，暖光透出来
            if (holder.transform.Find("Light_G5Door_Guide") == null)
            {
                var go = new GameObject("Light_G5Door_Guide");
                go.transform.SetParent(holder.transform, false);
                go.transform.position = new Vector3(DOOR_CX, DOOR_H * 0.6f, zBack + 1.2f);
                var l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(1.0f, 0.55f, 0.30f);   // 暖橙，比环境光更亮更暖
                l.intensity = 6f;
                l.range = 7f;
                l.shadows = LightShadows.None;
                l.renderMode = LightRenderMode.ForcePixel;
                Log("    + Light_G5Door_Guide");
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

            Log("门洞净宽（用 0.15 半径小球，只查金属框和墙体）：");
            int blocked = 0;
            float halfW = DOOR_W * 0.5f;
            for (float x = DOOR_CX - halfW; x <= DOOR_CX + halfW + 0.01f; x += 0.15f)
            {
                var hits = Physics.OverlapSphere(new Vector3(x, 1.0f, WALL_Z), 0.15f,
                                                  ~0, QueryTriggerInteraction.Ignore);
                var names = new System.Collections.Generic.List<string>();
                foreach (var h in hits)
                {
                    if (h == null || h.isTrigger) continue;
                    if (h.gameObject.name.Contains("Floor")) continue;
                    if (h.gameObject.name.Contains("Threshold")) continue;   // 门槛不挡路
                    names.Add(h.gameObject.name);
                }
                if (names.Count > 0) { blocked++; Log($"  x={x:F2} 被 [{string.Join(",", names)}] 挡"); }
            }
            Log($"  被挡的点 = {blocked}（应为 0）");

            Log("门框对象：");
            var holder = GameObject.Find("--- G5_DOOR_FRAME ---");
            if (holder != null)
                foreach (Transform c in holder.transform)
                {
                    var bc = c.GetComponent<BoxCollider>();
                    if (bc != null)
                    {
                        var b = bc.bounds;
                        Log($"  {c.name,-22} x[{b.min.x:F2},{b.max.x:F2}] y[{b.min.y:F2},{b.max.y:F2}] z[{b.min.z:F2},{b.max.z:F2}]");
                    }
                    else Log($"  {c.name,-22} (Light)");
                }
            Log("完成");
        }

        private static int MakeBox(string name, Transform parent, Vector3 center, Vector3 size,
                                    Material mat, string note)
        {
            var ex = parent.Find(name);
            if (ex != null) Object.DestroyImmediate(ex.gameObject);

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
            string line = $"[G5Frame] {msg}";
            Debug.Log(line);
            try
            {
                Directory.CreateDirectory(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports");
                File.AppendAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\G5DoorFrame_Result.txt",
                                   line + "\n", new UTF8Encoding(false));
            }
            catch { }
        }
    }
}