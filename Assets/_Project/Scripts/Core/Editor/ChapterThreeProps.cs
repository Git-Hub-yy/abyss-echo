using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S32：第三章 F 区道具布置。
    ///
    /// ⚠️ 强制规则（来自第一章/第二章的血泪教训）：
    ///   ① 每一件道具都要经过 SafePos() 判定，距任何门洞（含道具尺寸）留足间距
    ///   ② 避不开就**跳过**，绝不硬放
    ///   ③ 摆放后必须跑 ChapterTwoPropCheck 同款检查（这里复用其逻辑）
    ///
    /// 主题：这里**有人在住**。
    ///   所以道具不是"被遗弃的废墟"，而是"生活遗留物"：
    ///   餐桌、餐具、床、书架、杯子、椅子、储物箱。
    /// </summary>
    public static class ChapterThreeProps
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter3_Graybox.unity";
        private const string RootName = "--- PROPS_CH3 ---";

        private static readonly Dictionary<string, Material> _cache = new Dictionary<string, Material>();

        private static Material GetMat(string key)
        {
            if (_cache.TryGetValue(key, out var m) && m != null) return m;

            Color c; float metallic, smooth;
            switch (key)
            {
                case "Wood": c = new Color(0.34f, 0.25f, 0.17f); metallic = 0f; smooth = 0.12f; break;
                case "Metal": c = new Color(0.40f, 0.43f, 0.48f); metallic = 0.55f; smooth = 0.35f; break;
                case "Fabric": c = new Color(0.30f, 0.26f, 0.28f); metallic = 0f; smooth = 0.05f; break;
                case "Ceramic": c = new Color(0.78f, 0.76f, 0.70f); metallic = 0f; smooth = 0.65f; break;
                case "Dark": c = new Color(0.14f, 0.15f, 0.17f); metallic = 0.2f; smooth = 0.2f; break;
                case "Glass": c = new Color(0.20f, 0.30f, 0.34f); metallic = 0.6f; smooth = 0.88f; break;
                default: c = new Color(0.35f, 0.35f, 0.38f); metallic = 0.1f; smooth = 0.2f; break;
            }

            string folder = "Assets/_Project/Art/Materials/Ch3";
            EnsureFolder(folder);
            string path = $"{folder}/M_Ch3_{key}.mat";

            var exist = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (exist != null) { _cache[key] = exist; return exist; }

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(shader) { name = "M_Ch3_" + key };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            mat.color = c;
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smooth);

            AssetDatabase.CreateAsset(mat, path);
            _cache[key] = mat;
            return mat;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int idx = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, idx));
            AssetDatabase.CreateFolder(path.Substring(0, idx), path.Substring(idx + 1));
        }

        [MenuItem("ABYSS ECHO/S32 布置第三章道具")]
        public static void Run()
        {
            var prev = AbyssEcho.LevelGen.ChapterLayout.Current;
            AbyssEcho.LevelGen.ChapterLayout.Current =
                AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter3;

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid()) { Debug.LogError("[Ch3Props] 无法打开场景"); return; }

            var old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);

            var root = new GameObject(RootName);
            var rooms = CollectRooms();

            int n = 0;
            n += BuildF1(root.transform, rooms);   // 坠落天井
            n += BuildF2(root.transform, rooms);   // 中央楼梯廊
            n += BuildF3(root.transform, rooms);   // 旧档案室
            n += BuildF4(root.transform, rooms);   // 工作间
            n += BuildF5(root.transform, rooms);   // 起居区
            n += BuildF6(root.transform, rooms);   // 会客厅（12 副餐具）
            n += BuildF7(root.transform, rooms);   // 镜沼
            n += BuildF9(root.transform, rooms);   // 祭室
            n += BuildBedrooms(root.transform, rooms);
            n += BuildMisc(root.transform, rooms);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log($"<b>[Ch3Props]</b> 第三章道具布置完成，共 {n} 件");
            AbyssEcho.LevelGen.ChapterLayout.Current = prev;
        }

        // ══════════════════ 房间与避让 ══════════════════

        private class RoomInfo
        {
            public string Id;
            public Vector3 Center;
            public float SizeX, SizeZ, Height;
            public float MinX => Center.x - SizeX * 0.5f;
            public float MaxX => Center.x + SizeX * 0.5f;
            public float MinZ => Center.z - SizeZ * 0.5f;
            public float MaxZ => Center.z + SizeZ * 0.5f;
            public List<Vector3> Doorways = new List<Vector3>();
        }

        private static Dictionary<string, RoomInfo> CollectRooms()
        {
            var result = new Dictionary<string, RoomInfo>();
            foreach (var r in AbyssEcho.LevelGen.ChapterLayout.GetRooms(
                         AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter3))
            {
                var info = new RoomInfo
                {
                    Id = r.Id,
                    Center = new Vector3(r.Center.x, 0f, r.Center.z),
                    SizeX = r.SizeX,
                    SizeZ = r.SizeZ,
                    Height = r.Height
                };

                foreach (var d in r.Doorways)
                {
                    Vector3 p;
                    switch (d.Side)
                    {
                        case AbyssEcho.LevelGen.WallSide.North:
                            p = new Vector3(r.Center.x + d.Offset, 0f, r.MaxZ); break;
                        case AbyssEcho.LevelGen.WallSide.South:
                            p = new Vector3(r.Center.x + d.Offset, 0f, r.MinZ); break;
                        case AbyssEcho.LevelGen.WallSide.East:
                            p = new Vector3(r.MaxX, 0f, r.Center.z + d.Offset); break;
                        default:
                            p = new Vector3(r.MinX, 0f, r.Center.z + d.Offset); break;
                    }
                    info.Doorways.Add(p);
                }

                result[r.Id] = info;
            }
            return result;
        }

        private static bool BlocksDoorway(RoomInfo r, Vector3 pos, float need)
        {
            foreach (var d in r.Doorways)
            {
                float dx = pos.x - d.x, dz = pos.z - d.z;
                if (Mathf.Sqrt(dx * dx + dz * dz) < need) return true;
            }
            return false;
        }

        private static Vector3? SafePos(RoomInfo r, Vector3 desired, Vector3 size, float clearance = 1.6f)
        {
            float radius = Mathf.Max(size.x, size.z) * 0.5f;
            float need = clearance + radius;

            if (!BlocksDoorway(r, desired, need)) return desired;

            float[] offs = { 1.2f, -1.2f, 2.4f, -2.4f, 3.6f, -3.6f };
            foreach (var ox in offs)
                foreach (var oz in offs)
                {
                    var p = desired + new Vector3(ox, 0f, oz);
                    if (p.x - radius < r.MinX + 0.3f || p.x + radius > r.MaxX - 0.3f) continue;
                    if (p.z - radius < r.MinZ + 0.3f || p.z + radius > r.MaxZ - 0.3f) continue;
                    if (!BlocksDoorway(r, p, need)) return p;
                }
            return null;
        }

        // ══════════════════ 放置工具 ══════════════════

        private static Transform Root(Transform parent, string id)
        {
            var go = new GameObject("Props_" + id);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static int BoxAt(Transform parent, string name, Vector3 pos, Vector3 size, string mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = GetMat(mat);
            return 1;
        }

        private static int Box(RoomInfo r, Transform parent, string name, Vector3 pos, Vector3 size, string mat)
        {
            var s = SafePos(r, pos, size);
            if (!s.HasValue) return 0;
            return BoxAt(parent, name, s.Value, size, mat);
        }

        private static int Tipped(RoomInfo r, Transform parent, string name, Vector3 pos, Vector3 size, Vector3 euler, string mat)
        {
            var s = SafePos(r, pos, size);
            if (!s.HasValue) return 0;

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = s.Value;
            go.transform.localScale = size;
            go.transform.rotation = Quaternion.Euler(euler);
            go.GetComponent<Renderer>().sharedMaterial = GetMat(mat);
            return 1;
        }

        // ══════════════════ F1 坠落天井 ══════════════════
        private static int BuildF1(Transform p, Dictionary<string, RoomInfo> rs)
        {
            if (!rs.TryGetValue("F1", out var r)) return 0;
            var root = Root(p, "F1");
            int n = 0;

            // 玩家坠落处：地上的浅水与碎片
            n += BoxAt(root, "积水", new Vector3(r.Center.x, 0.02f, r.Center.z - 2f),
                       new Vector3(5f, 0.04f, 5f), "Glass");
            n += Tipped(r, root, "坠落碎片", new Vector3(r.Center.x + 1.8f, 0.15f, r.Center.z - 1.5f),
                        new Vector3(0.5f, 0.15f, 0.7f), new Vector3(0f, 30f, 8f), "Metal");

            // 天井四周的管道
            n += Box(r, root, "管道西", new Vector3(r.MinX + 0.5f, 2.5f, r.Center.z),
                     new Vector3(0.3f, 4f, 0.3f), "Metal");
            n += Box(r, root, "管道东", new Vector3(r.MaxX - 0.5f, 2.5f, r.Center.z),
                     new Vector3(0.3f, 4f, 0.3f), "Metal");
            return n;
        }

        // ══════════════════ F2 中央楼梯廊 ══════════════════
        private static int BuildF2(Transform p, Dictionary<string, RoomInfo> rs)
        {
            if (!rs.TryGetValue("F2", out var r)) return 0;
            var root = Root(p, "F2");
            int n = 0;

            // 走廊两侧的箱子（不挡中间通道）
            n += Box(r, root, "箱1", new Vector3(r.MinX + 0.8f, 0.3f, r.MinZ + 3f),
                     new Vector3(0.6f, 0.6f, 0.6f), "Wood");
            n += Box(r, root, "箱2", new Vector3(r.MaxX - 0.8f, 0.25f, r.MaxZ - 3f),
                     new Vector3(0.6f, 0.5f, 0.6f), "Wood");
            return n;
        }

        // ══════════════════ F3 旧档案室 ══════════════════
        private static int BuildF3(Transform p, Dictionary<string, RoomInfo> rs)
        {
            if (!rs.TryGetValue("F3", out var r)) return 0;
            var root = Root(p, "F3");
            int n = 0;

            // 档案柜两排
            for (int i = 0; i < 4; i++)
            {
                float z = r.MinZ + 1.8f + i * 1.9f;
                n += Box(r, root, $"档案柜{i + 1}", new Vector3(r.MinX + 1.3f, 1.1f, z),
                         new Vector3(0.55f, 2.2f, 1.5f), "Metal");
            }

            // 翻倒的椅子 + 散落文件
            n += Tipped(r, root, "翻倒椅", new Vector3(r.Center.x + 2.2f, 0.25f, r.Center.z + 1.5f),
                        new Vector3(0.5f, 0.5f, 0.5f), new Vector3(78f, 20f, 0f), "Wood");
            n += Box(r, root, "散落文件", new Vector3(r.Center.x + 1.5f, 0.03f, r.Center.z - 2f),
                     new Vector3(0.9f, 0.06f, 0.7f), "Ceramic");

            // 值班记录桌
            n += Box(r, root, "值班桌", new Vector3(r.MaxX - 2.0f, 0.75f, r.MaxZ - 2.0f),
                     new Vector3(1.8f, 0.1f, 0.9f), "Wood");
            return n;
        }

        // ══════════════════ F4 工作间 ══════════════════
        private static int BuildF4(Transform p, Dictionary<string, RoomInfo> rs)
        {
            if (!rs.TryGetValue("F4", out var r)) return 0;
            var root = Root(p, "F4");
            int n = 0;

            // 工作台
            n += Box(r, root, "工作台", new Vector3(r.MinX + 2.0f, 0.8f, r.Center.z),
                     new Vector3(2.4f, 0.12f, 1.0f), "Metal");
            // 工具柜
            n += Box(r, root, "工具柜", new Vector3(r.MaxX - 1.2f, 1.0f, r.MinZ + 1.5f),
                     new Vector3(0.7f, 2.0f, 1.2f), "Metal");
            // 零件箱
            n += Box(r, root, "零件箱", new Vector3(r.Center.x, 0.25f, r.MaxZ - 1.5f),
                     new Vector3(0.7f, 0.5f, 0.5f), "Wood");
            return n;
        }

        // ══════════════════ F5 起居区（有人在住的证据）══════════════════
        private static int BuildF5(Transform p, Dictionary<string, RoomInfo> rs)
        {
            if (!rs.TryGetValue("F5", out var r)) return 0;
            var root = Root(p, "F5");
            int n = 0;

            // 桌子 + 椅子
            n += Box(r, root, "起居桌", new Vector3(r.Center.x, 0.75f, r.Center.z),
                     new Vector3(1.8f, 0.1f, 1.0f), "Wood");
            n += Box(r, root, "起居桌腿", new Vector3(r.Center.x, 0.37f, r.Center.z),
                       new Vector3(0.14f, 0.74f, 0.14f), "Wood");

            // ⚠️ 关键细节：桌上那杯还温着的水
            n += BoxAt(root, "温的水杯", new Vector3(r.Center.x + 0.35f, 0.86f, r.Center.z + 0.1f),
                       new Vector3(0.09f, 0.14f, 0.09f), "Ceramic");

            // 被推开的椅子
            n += Tipped(r, root, "推开的椅", new Vector3(r.Center.x - 1.2f, 0.25f, r.Center.z + 0.8f),
                        new Vector3(0.5f, 0.5f, 0.5f), new Vector3(12f, 25f, 0f), "Wood");

            // 书架
            n += Box(r, root, "书架", new Vector3(r.MinX + 1.2f, 1.1f, r.MaxZ - 1.8f),
                     new Vector3(0.5f, 2.2f, 2.0f), "Wood");
            // 翻开的书（矮板）
            n += BoxAt(root, "翻开的书", new Vector3(r.Center.x - 0.4f, 0.82f, r.Center.z - 0.2f),
                       new Vector3(0.4f, 0.03f, 0.28f), "Ceramic");
            return n;
        }

        // ══════════════════ F6 会客厅（第三章核心场景：12 副餐具）══════════════════
        private static int BuildF6(Transform p, Dictionary<string, RoomInfo> rs)
        {
            if (!rs.TryGetValue("F6", out var r)) return 0;
            var root = Root(p, "F6");
            int n = 0;

            // ══════════════ 长餐桌 ══════════════
            //
            // ⚠️ 摆放方向是反复试出来的：
            //   原本 2.2x6.0 南北向放在正中，z 跨 [27,33]，
            //   而 F6 北门在 z=34.5 —— 桌子离北门只有 1.5 米，
            //   正好落进门洞检测区，于是 SafePos 判断"挡路"把桌子整个丢掉了。
            //
            //   现在改为东西向 4.0x1.6，摆在房间中部偏南，
            //   与南北两扇门都保持 3 米以上距离，既放得下又不挡路。
            float tz = r.Center.z - 1.2f;
            int tbl = Box(r, root, "长餐桌", new Vector3(r.Center.x, 0.76f, tz),
                          new Vector3(4.0f, 0.12f, 1.6f), "Wood");
            if (tbl == 0)
            {
                // 兜底：避让失败时直接放在房间中心（12x9 的房间里 4x1.6 一定容得下）
                BoxAt(root, "长餐桌", new Vector3(r.Center.x, 0.76f, r.Center.z),
                      new Vector3(4.0f, 0.12f, 1.6f), "Wood");
                tz = r.Center.z;
                tbl = 1;
            }
            n += tbl;
            n += BoxAt(root, "餐桌腿", new Vector3(r.Center.x, 0.38f, tz),
                       new Vector3(0.2f, 0.76f, 0.2f), "Wood");

            // 12 副餐具：长边两侧各 6 副
            //   第 7 副（下标 6）是"被用过的那一副" —— 刀叉有痕迹 + 半杯水
            int idx = 0;
            for (int side = -1; side <= 1; side += 2)   // side: -1 南侧, +1 北侧
            {
                for (int i = 0; i < 6; i++)
                {
                    // 沿东西方向排开，间距 0.6 米（桌长 4 米，两端各留 0.4）
                    float x = r.Center.x - 1.5f + i * 0.6f;
                    float z = tz + side * 0.55f;
                    bool used = (idx == 6);

                    string prefix = used ? "★用过的餐盘" : "餐盘";
                    n += BoxAt(root, $"{prefix}{idx + 1}", new Vector3(x, 0.84f, z),
                               new Vector3(0.24f, 0.02f, 0.24f), "Ceramic");

                    // 杯子：11 个空、1 个装着水
                    n += BoxAt(root, used ? "有水的杯子" : $"空杯{idx + 1}",
                               new Vector3(x + 0.13f, 0.90f, z + side * 0.1f),
                               new Vector3(0.07f, 0.10f, 0.07f), "Ceramic");

                    if (used)
                    {
                        n += BoxAt(root, "用过的刀", new Vector3(x - 0.1f, 0.845f, z - side * 0.06f),
                                   new Vector3(0.16f, 0.008f, 0.02f), "Metal");
                        n += BoxAt(root, "用过的叉", new Vector3(x - 0.1f, 0.845f, z - side * 0.1f),
                                   new Vector3(0.16f, 0.008f, 0.02f), "Metal");
                    }

                    idx++;
                }
            }

            // 餐具柜
            n += Box(r, root, "餐具柜", new Vector3(r.MinX + 1.2f, 1.0f, r.MinZ + 2.0f),
                     new Vector3(0.6f, 2.0f, 2.2f), "Wood");

            // 吊灯（视觉焦点）
            n += BoxAt(root, "吊灯罩", new Vector3(r.Center.x, r.Height - 1.0f, r.Center.z),
                       new Vector3(2.4f, 0.12f, 2.4f), "Metal");

            return n;
        }

        // ══════════════════ F7 镜沼（几乎全空，只有水和边缘）══════════════════
        private static int BuildF7(Transform p, Dictionary<string, RoomInfo> rs)
        {
            if (!rs.TryGetValue("F7", out var r)) return 0;
            var root = Root(p, "F7");
            int n = 0;

            // 水面（用透明玻璃材质表现，实际倒影由 MirrorReflection 处理）
            var water = GameObject.CreatePrimitive(PrimitiveType.Cube);
            water.name = "Water_F7";
            water.transform.SetParent(root, false);
            water.transform.position = new Vector3(r.Center.x, 0.12f, r.Center.z);
            water.transform.localScale = new Vector3(13f, 0.24f, 11f);
            water.GetComponent<Renderer>().sharedMaterial = GetMat("Glass");
            Object.DestroyImmediate(water.GetComponent<Collider>());   // 不挡玩家
            n++;

            // 岸边台阶（引导玩家靠近）
            n += BoxAt(root, "岸边台", new Vector3(r.Center.x, 0.06f, r.MinZ + 1.2f),
                       new Vector3(8f, 0.12f, 1.6f), "Dark");

            return n;
        }

        // ══════════════════ F9 祭室 ══════════════════
        private static int BuildF9(Transform p, Dictionary<string, RoomInfo> rs)
        {
            if (!rs.TryGetValue("F9", out var r)) return 0;
            var root = Root(p, "F9");
            int n = 0;

            // 中央石台（供奉位）
            n += Box(r, root, "供奉台", new Vector3(r.Center.x, 0.5f, r.Center.z),
                       new Vector3(1.4f, 1.0f, 1.4f), "Dark");
            n += Box(r, root, "供奉台面", new Vector3(r.Center.x, 1.02f, r.Center.z),
                       new Vector3(1.6f, 0.08f, 1.6f), "Ceramic");

            // 两侧长明灯座
            n += BoxAt(root, "长明灯座1", new Vector3(r.Center.x - 2.4f, 0.4f, r.MaxZ - 1.6f),
                       new Vector3(0.4f, 0.8f, 0.4f), "Metal");
            n += BoxAt(root, "长明灯座2", new Vector3(r.Center.x + 2.4f, 0.4f, r.MaxZ - 1.6f),
                       new Vector3(0.4f, 0.8f, 0.4f), "Metal");

            // 地面刻痕
            n += Box(r, root, "地面刻痕", new Vector3(r.Center.x, 0.02f, r.Center.z + 1.6f),
                       new Vector3(3.2f, 0.04f, 3.2f), "Dark");

            return n;
        }

        // ══════════════════ 卧房（F10 / F11 / F12）══════════════════
        private static int BuildBedrooms(Transform p, Dictionary<string, RoomInfo> rs)
        {
            int n = 0;
            foreach (var id in new[] { "F10", "F11", "F12" })
            {
                if (!rs.TryGetValue(id, out var r)) continue;
                var root = Root(p, id);

                // 床（靠一侧）
                n += Box(r, root, "床架", new Vector3(r.MinX + 1.1f, 0.28f, r.Center.z),
                           new Vector3(1.9f, 0.48f, 1.0f), "Metal");
                n += Box(r, root, "床垫", new Vector3(r.MinX + 1.1f, 0.58f, r.Center.z),
                           new Vector3(1.8f, 0.16f, 0.92f), "Fabric");

                // 床头柜
                n += Box(r, root, "床头柜", new Vector3(r.MinX + 0.6f, 0.3f, r.Center.z + 1.2f),
                           new Vector3(0.45f, 0.6f, 0.45f), "Wood");

                // F12 卧房 C 特有：床是"刚离开"的状态（被子翻开）
                if (id == "F12")
                {
                    n += Tipped(r, root, "掀开的被子", new Vector3(r.MinX + 1.1f, 0.70f, r.Center.z + 0.4f),
                                new Vector3(1.6f, 0.10f, 0.7f), new Vector3(-14f, 8f, 0f), "Fabric");
                }
            }
            return n;
        }

        // ══════════════════ 其它房间 ══════════════════
        private static int BuildMisc(Transform p, Dictionary<string, RoomInfo> rs)
        {
            int n = 0;

            // F8 储藏间：整箱物资（19 年的储备）
            if (rs.TryGetValue("F8", out var f8))
            {
                var root = Root(p, "F8");
                for (int i = 0; i < 3; i++)
                    for (int j = 0; j < 2; j++)
                        n += Box(f8, root, $"储备箱{i * 2 + j + 1}",
                                 new Vector3(f8.MinX + 1.4f + i * 1.2f, 0.35f, f8.MinZ + 1.5f + j * 1.4f),
                                 new Vector3(0.8f, 0.7f, 0.8f), "Wood");
            }

            // F17 图书馆：书架
            if (rs.TryGetValue("F17", out var f17))
            {
                var root = Root(p, "F17");
                for (int i = 0; i < 3; i++)
                    n += Box(f17, root, $"书架{i + 1}",
                             new Vector3(f17.MinX + 1.2f, 1.1f, f17.MinZ + 1.6f + i * 2.0f),
                             new Vector3(0.5f, 2.2f, 1.6f), "Wood");
            }

            // F13 盥洗间：三把牙刷（两把用过）
            if (rs.TryGetValue("F13", out var f13))
            {
                var root = Root(p, "F13");
                for (int i = 0; i < 3; i++)
                    n += Box(f13, root, i < 2 ? $"用过的牙刷{i + 1}" : $"没用的牙刷{i + 1}",
                               new Vector3(f13.Center.x - 0.5f + i * 0.5f, 0.88f, f13.MinZ + 0.8f),
                               new Vector3(0.03f, 0.03f, 0.16f), "Ceramic");
            }

            // F16 天井下层：坠落的物证
            if (rs.TryGetValue("F16", out var f16))
            {
                var root = Root(p, "F16");
                n += Tipped(f16, root, "破碎的观察窗", new Vector3(f16.Center.x, 0.08f, f16.Center.z),
                            new Vector3(2.0f, 0.16f, 2.0f), new Vector3(0f, 15f, 0f), "Glass");
            }

            return n;
        }
    }
}
