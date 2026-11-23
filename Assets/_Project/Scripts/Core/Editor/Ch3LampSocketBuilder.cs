using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S65：给第三章 F9 祭室的两个长明灯座接上灯泡 → 结局 D。
    ///
    /// ══════════════════ 背景 ══════════════════
    ///
    ///   场景里本来就有：
    ///     长明灯座1 @ (11.6, 0.4, 32.4)
    ///     长明灯座2 @ (16.4, 0.4, 32.4)
    ///   而玩家能在 F4 工作间捡到「灯泡」(Pickup_Bulb)，
    ///   但之前灯泡**没有任何用途**。
    ///
    /// ══════════════════ 本 Builder 做什么 ══════════════════
    ///
    ///   ① 给两个灯座挂 LampSocket 组件（幂等）
    ///   ② 每个灯座加一盏 Light（初始关闭，点亮后打开）
    ///   ③ 每个灯座加一个「灯泡视觉标记」（小立方体，初始隐藏）
    ///   ④ 两个灯座互相引用 sibling（任一被点亮，另一个也能看到亮光）
    ///
    ///   不新增交互设施 —— 灯座本来就在场景里。
    /// </summary>
    public static class Ch3LampSocketBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter3_Graybox.unity";

        [MenuItem("ABYSS ECHO/修复 v7.1/第三章 长明灯座接入灯泡结局")]
        public static void Build() => Run();

        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Log("已打开场景");

            Material bulbMat = FindMat("M_Ch3_Lamp") ?? FindMat("M_Ch4_Metal");
            int n = 0;

            var sockets = new System.Collections.Generic.List<AbyssEcho.Puzzle.LampSocket>();

            foreach (var name in new[] { "长明灯座1", "长明灯座2" })
            {
                var go = GameObject.Find(name);
                if (go == null) { Log($"  ❌ 找不到 {name}"); continue; }

                Log($"  {name} @ {go.transform.position}");

                // ① LampSocket 组件
                var socket = go.GetComponent<AbyssEcho.Puzzle.LampSocket>();
                if (socket == null)
                {
                    socket = go.AddComponent<AbyssEcho.Puzzle.LampSocket>();
                    Log($"    + LampSocket");
                    n++;
                }
                socket.requiredItemId = "item.bulb";

                // ② 灯光（初始关闭）
                var lightGo = go.transform.Find("LampLight");
                if (lightGo == null)
                {
                    var lg = new GameObject("LampLight");
                    lg.transform.SetParent(go.transform, false);
                    lg.transform.localPosition = new Vector3(0f, 0.95f, 0f);
                    var l = lg.AddComponent<Light>();
                    l.type = LightType.Point;
                    l.color = new Color(1.0f, 0.86f, 0.62f);   // 暖白
                    l.intensity = 6.5f;
                    l.range = 11f;
                    l.shadows = LightShadows.None;
                    l.renderMode = LightRenderMode.ForcePixel;
                    l.enabled = false;                          // 点亮前不亮
                    socket.lampLight = l;
                    Log($"    + LampLight（初始关闭）");
                    n++;
                    lightGo = lg.transform;
                }

                // ③ 灯泡视觉标记
                var markGo = go.transform.Find("BulbMarker");
                if (markGo == null)
                {
                    var mg = new GameObject("BulbMarker");
                    mg.transform.SetParent(go.transform, false);
                    mg.transform.localPosition = new Vector3(0f, 0.88f, 0f);
                    mg.transform.localScale = new Vector3(0.22f, 0.30f, 0.22f);

                    var mf = mg.AddComponent<MeshFilter>();
                    var mr = mg.AddComponent<MeshRenderer>();
                    var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    mf.sharedMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
                    Object.DestroyImmediate(tmp);
                    if (bulbMat != null) mr.sharedMaterial = bulbMat;

                    var col = mg.AddComponent<SphereCollider>();
                    col.isTrigger = true;                        // 不挡路

                    mg.SetActive(false);                         // 点亮前隐藏
                    socket.bulbMarker = mg.transform;
                    Log($"    + BulbMarker（初始隐藏）");
                    n++;
                    markGo = mg.transform;
                }

                sockets.Add(socket);
            }

            // ④ 互相引用
            if (sockets.Count == 2)
            {
                sockets[0].sibling = sockets[1];
                sockets[1].sibling = sockets[0];
                Log("  ✅ 两个灯座已互相引用");
            }

            if (n > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                bool ok = EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Log(ok ? $"场景已保存（{n} 项）" : "❌ 保存失败");
            }
            else Log("全部已存在，无改动");

            Verify();
        }

        private static void Verify()
        {
            Log("---- 验证 ----");
            Physics.SyncTransforms();

            var list = Object.FindObjectsByType<AbyssEcho.Puzzle.LampSocket>(FindObjectsSortMode.None);
            Log($"场景中 LampSocket 数量 = {list.Length}");
            foreach (var s in list)
            {
                Log($"  {s.gameObject.name} @ {s.transform.position}");
                Log($"    requiredItemId = \"{s.requiredItemId}\"");
                Log($"    IsLit = {s.IsLit}");
                Log($"    lampLight = {(s.lampLight != null ? s.lampLight.name + " enabled=" + s.lampLight.enabled : "空")}");
                Log($"    bulbMarker = {(s.bulbMarker != null ? s.bulbMarker.name + " active=" + s.bulbMarker.gameObject.activeSelf : "空")}");
                Log($"    sibling = {(s.sibling != null ? s.sibling.name : "空")}");
            }

            // 灯泡拾取物确认
            var bulb = GameObject.Find("Pickup_Bulb");
            Log($"Pickup_Bulb 存在 = {bulb != null}" + (bulb != null ? $" @ {bulb.transform.position}" : ""));
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
            string line = $"[Ch3Lamp] {msg}";
            Debug.Log(line);
            try
            {
                Directory.CreateDirectory(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports");
                File.AppendAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Ch3LampSocket_Result.txt",
                                   line + "\n", new UTF8Encoding(false));
            }
            catch { }
        }
    }
}