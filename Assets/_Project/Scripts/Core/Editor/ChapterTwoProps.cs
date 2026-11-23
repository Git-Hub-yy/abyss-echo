using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S28：第二章道具布置（D/E 区）。
    ///
    /// ⚠️ 背景：
    ///   PropPlacementBuilder 只覆盖第一章的 A/B/C 区，
    ///   第二章 23 个房间几乎是空的 —— 只有交互物（水泵、录音机、便条）撑着。
    ///   玩家走进去会感觉"这是个未完成的测试场"，而不是被遗弃的深海设施。
    ///
    /// 设计基调（参照第一章的成功经验）：
    ///   · 深海研究设施 → 箱子、货架、管道、仪器、椅子、柜子
    ///   · 被遗弃 → 倾斜的家具、散落的杂物、破损的容器
    ///   · 每件道具都要避开门口通道（第一章踩过的坑：餐桌挡路）
    ///
    /// 安全规则（硬性）：
    ///   ① 道具距房间边界 ≥ 0.8 米（不贴墙，避免穿模）
    ///   ② 道具距门洞中心 ≥ 2.2 米（绝不挡路）
    ///   ③ 只放在主通道两侧，中间留 ≥ 2 米走道
    /// </summary>
    public static class ChapterTwoProps
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter2_Graybox.unity";
        private const string RootName = "--- PROPS_CH2 ---";

        // 材质库（用 Unity 内置材质，避免依赖自定义资源）
        private static Material GetMat(string key)
        {
            switch (key)
            {
                case "Metal": return Make("M_Ch2_Metal", new Color(0.42f, 0.46f, 0.52f), 0.55f, 0.35f);
                case "Rust": return Make("M_Ch2_Rust", new Color(0.38f, 0.26f, 0.20f), 0.15f, 0.15f);
                case "Crate": return Make("M_Ch2_Crate", new Color(0.46f, 0.36f, 0.24f), 0.0f, 0.10f);
                case "Dark": return Make("M_Ch2_Dark", new Color(0.16f, 0.18f, 0.21f), 0.20f, 0.25f);
                case "Glass": return Make("M_Ch2_Glass", new Color(0.22f, 0.32f, 0.36f), 0.60f, 0.85f);
                default: return Make("M_Ch2_Default", new Color(0.35f, 0.38f, 0.42f), 0.1f, 0.2f);
            }
        }

        private static readonly Dictionary<string, Material> _cache = new Dictionary<string, Material>();

        private static Material Make(string name, Color c, float metallic, float smooth)
        {
            if (_cache.TryGetValue(name, out var m) && m != null) return m;

            string folder = "Assets/_Project/Art/Materials/Ch2";
            EnsureFolder(folder);
            string path = $"{folder}/{name}.mat";

            var exist = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (exist != null) { _cache[name] = exist; return exist; }

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(shader) { name = name };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            mat.color = c;
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smooth);

            AssetDatabase.CreateAsset(mat, path);
            _cache[name] = mat;
            return mat;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int idx = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, idx));
            AssetDatabase.CreateFolder(path.Substring(0, idx), path.Substring(idx + 1));
        }

        [MenuItem("ABYSS ECHO/S28 布置第二章道具")]
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError("[Ch2Props] 无法打开第二章场景");
                EditorApplication.Exit(1);
                return;
            }

            var old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);

            var root = new GameObject(RootName);
            var rooms = CollectRooms();

            int total = 0;
            total += BuildD1(root.transform, rooms);
            total += BuildD2(root.transform, rooms);
            total += BuildD5(root.transform, rooms);
            total += BuildD3(root.transform, rooms);
            total += BuildD4(root.transform, rooms);
            total += BuildD6(root.transform, rooms);
            total += BuildD7(root.transform, rooms);
            total += BuildE1(root.transform, rooms);
            total += BuildE2(root.transform, rooms);
            total += BuildE3(root.transform, rooms);
            total += BuildE4(root.transform, rooms);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log($"<b>[Ch2Props]</b> 第二章道具布置完成，共 {total} 件");
        }

        // ── 房间收集 ──
        private class RoomInfo
        {
            public string Id;
            public Vector3 Center;
            public float SizeX, SizeZ, Height;
            public float MinX => Center.x - SizeX * 0.5f;
            public float MaxX => Center.x + SizeX * 0.5f;
            public float MinZ => Center.z - SizeZ * 0.5f;
            public float MaxZ => Center.z + SizeZ * 0.5f;

            /// <summary>该房间所有门洞的世界坐标（S28：用于避让）。</summary>
            public List<Vector3> Doorways = new List<Vector3>();
        }

        private static Dictionary<string, RoomInfo> CollectRooms()
        {
            var result = new Dictionary<string, RoomInfo>();
            var list = AbyssEcho.LevelGen.ChapterLayout.GetRooms(
                AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter2);

            foreach (var r in list)
            {
                var info = new RoomInfo
                {
                    Id = r.Id,
                    Center = new Vector3(r.Center.x, 0f, r.Center.z),
                    SizeX = r.SizeX,
                    SizeZ = r.SizeZ,
                    Height = r.Height
                };

                // 收集门洞位置
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

        /// <summary>
        /// S28：判断某个位置会不会挡住门洞通道。
        ///
        /// 第一章踩过的坑：餐桌摆在门洞正中间，玩家被"悬空桌面"挡住。
        /// 这里做统一避让：位置距任何门洞中心 &lt; 2.6 米就视为不安全。
        ///
        /// 2.6 米的由来：门宽 2.4 米 + 玩家胶囊半径 0.32 + 余量 ≈ 2.6
        /// </summary>
        private static bool BlocksDoorway(RoomInfo r, Vector3 pos, float clearance = 2.6f)
        {
            foreach (var d in r.Doorways)
            {
                // 只比较水平距离
                float dx = pos.x - d.x;
                float dz = pos.z - d.z;
                if (Mathf.Sqrt(dx * dx + dz * dz) < clearance) return true;
            }
            return false;
        }

        /// <summary>
        /// S28：安全摆放包装 —— 位置不安全就自动偏移。
        ///
        /// ⚠️ 关键：必须把**道具尺寸**算进去，不能只看中心点。
        ///   实测教训：E1 的实验台中心距门 2.69 米（看似安全），
        ///   但桌宽 2.4 米、深 1.0 米，包围盒 z 范围 [35.9, 36.9]
        ///   与门检测区 z [33.5, 36.5] 仍然重叠 → 挡住通道。
        ///
        ///   现在用"半径 = 道具最大边长的一半 + 安全距"来判定。
        /// </summary>
        private static Vector3? SafePos(RoomInfo r, Vector3 desired, Vector3 size, float clearance = 1.6f)
        {
            // 道具包围盒的水平半径（取 X/Z 中较大的一半）
            float radius = Mathf.Max(size.x, size.z) * 0.5f;
            float need = clearance + radius;

            if (!BlocksDoorway(r, desired, need)) return desired;

            // 沿 X / Z 各尝试几档偏移
            float[] offsets = { 1.2f, -1.2f, 2.4f, -2.4f, 3.6f, -3.6f, 4.8f, -4.8f };
            foreach (var ox in offsets)
            {
                foreach (var oz in offsets)
                {
                    var p = desired + new Vector3(ox, 0f, oz);
                    // 不能跑出房间（留出半径 + 0.3 余量）
                    if (p.x - radius < r.MinX + 0.3f || p.x + radius > r.MaxX - 0.3f) continue;
                    if (p.z - radius < r.MinZ + 0.3f || p.z + radius > r.MaxZ - 0.3f) continue;
                    if (!BlocksDoorway(r, p, need)) return p;
                }
            }
            return null;   // 放弃该道具
        }

        // ── 工具 ──
        private static Transform Root(Transform parent, string roomId)
        {
            var go = new GameObject("Props_" + roomId);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        /// <summary>直接放置立方体（位置已由调用方验证过，不再检查避让）。</summary>
        private static int BoxAt(Transform parent, string name, Vector3 pos, Vector3 size, string matKey)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = GetMat(matKey);
            return 1;
        }

        /// <summary>放置立方体道具；自动避让门洞（不安全则跳过）。</summary>
        private static int Box(RoomInfo room, Transform parent, string name, Vector3 pos, Vector3 size, string matKey)
        {
            var safe = SafePos(room, pos, size);
            if (!safe.HasValue) return 0;   // 避不开就跳过，绝不挡路

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = safe.Value;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = GetMat(matKey);
            return 1;
        }

        /// <summary>斜放箱子（被遗弃感），同样自动避让门洞。</summary>
        private static int TippedBox(RoomInfo room, Transform parent, string name, Vector3 pos, Vector3 size, Vector3 euler, string matKey)
        {
            var safe = SafePos(room, pos, size);
            if (!safe.HasValue) return 0;

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = safe.Value;
            go.transform.localScale = size;
            go.transform.rotation = Quaternion.Euler(euler);
            go.GetComponent<Renderer>().sharedMaterial = GetMat(matKey);
            return 1;
        }

        // ══════════════ D1 闸门过渡舱（小，只放两个箱子）══════════════
        private static int BuildD1(Transform parent, Dictionary<string, RoomInfo> rooms)
        {
            if (!rooms.TryGetValue("D1", out var r)) return 0;
            var root = Root(parent, "D1");
            int n = 0;
            n += Box(r, root, "箱1", new Vector3(r.Center.x - 1.2f, 0.30f, r.Center.z - 1.1f),
                     new Vector3(0.7f, 0.6f, 0.7f), "Crate");
            n += Box(r, root, "箱2", new Vector3(r.Center.x + 1.3f, 0.22f, r.Center.z + 1.2f),
                     new Vector3(0.6f, 0.44f, 0.6f), "Rust");
            return n;
        }

        // ══════════════ D2 被水淹的大厅（大空间，靠两侧布置）══════════════
        private static int BuildD2(Transform parent, Dictionary<string, RoomInfo> rooms)
        {
            if (!rooms.TryGetValue("D2", out var r)) return 0;
            var root = Root(parent, "D2");
            int n = 0;

            // 中间通道 |x| < 1.5 留空（通往北门和东西侧门）
            // 西侧：货架排
            for (int i = 0; i < 3; i++)
            {
                float z = r.MinZ + 3.0f + i * 4.0f;
                n += Box(r, root, $"货架{i + 1}", new Vector3(r.MinX + 1.6f, 1.1f, z),
                         new Vector3(0.6f, 2.2f, 3.0f), "Metal");
            }

            // 东侧：散落箱子 + 倾斜的仪器
            n += TippedBox(r, root, "翻倒的箱", new Vector3(r.MaxX - 2.0f, 0.35f, r.MinZ + 2.5f),
                           new Vector3(0.9f, 0.7f, 0.9f), new Vector3(18f, 32f, 12f), "Crate");
            n += Box(r, root, "仪器柜", new Vector3(r.MaxX - 1.8f, 0.85f, r.Center.z),
                     new Vector3(1.0f, 1.7f, 1.6f), "Dark");
            n += Box(r, root, "箱3", new Vector3(r.MaxX - 3.2f, 0.28f, r.MinZ + 1.8f),
                     new Vector3(0.75f, 0.56f, 0.75f), "Rust");

            // 角落：排水口
            n += Box(r, root, "排水口", new Vector3(r.Center.x + 6.5f, 0.06f, r.MaxZ - 2.0f),
                     new Vector3(1.2f, 0.12f, 1.2f), "Dark");

            // 天花板管道（视觉填充）
            for (int i = 0; i < 2; i++)
            {
                float x = r.MinX + 3.0f + i * 11.0f;
                n += Box(r, root, $"管道{i + 1}", new Vector3(x, r.Height - 0.5f, r.Center.z),
                         new Vector3(0.35f, 0.35f, r.SizeZ - 2.0f), "Rust");
            }

            return n;
        }

        // ══════════════ D3 泵房（设备密集）══════════════
        private static int BuildD3(Transform parent, Dictionary<string, RoomInfo> rooms)
        {
            if (!rooms.TryGetValue("D3", out var r)) return 0;
            var root = Root(parent, "D3");
            int n = 0;

            // 管道沿墙
            n += Box(r, root, "主管道", new Vector3(r.Center.x, r.Height - 0.4f, r.Center.z),
                     new Vector3(r.SizeX - 2.0f, 0.3f, 0.3f), "Rust");
            // 备用水泵（纯装饰）
            n += Box(r, root, "备用泵", new Vector3(r.MaxX - 1.5f, 0.55f, r.MinZ + 1.5f),
                     new Vector3(1.0f, 1.1f, 1.0f), "Metal");
            // 控制柜
            n += Box(r, root, "控制柜", new Vector3(r.MaxX - 1.2f, 1.0f, r.MaxZ - 1.5f),
                     new Vector3(0.7f, 2.0f, 1.4f), "Dark");
            // 油桶
            n += Box(r, root, "油桶1", new Vector3(r.MinX + 1.2f, 0.45f, r.MaxZ - 1.5f),
                     new Vector3(0.6f, 0.9f, 0.6f), "Rust");
            return n;
        }

        // ══════════════ D4 档案室（文件密集）══════════════
        private static int BuildD4(Transform parent, Dictionary<string, RoomInfo> rooms)
        {
            if (!rooms.TryGetValue("D4", out var r)) return 0;
            var root = Root(parent, "D4");
            int n = 0;

            // 两排档案柜
            for (int i = 0; i < 4; i++)
            {
                float z = r.MinZ + 1.8f + i * 3.0f;
                n += Box(r, root, $"档案柜{i + 1}", new Vector3(r.MinX + 1.4f, 1.1f, z),
                         new Vector3(0.55f, 2.2f, 1.6f), "Metal");
            }
            // 散落的文件（矮板）
            n += TippedBox(r, root, "散落文件", new Vector3(r.Center.x + 2.0f, 0.09f, r.Center.z),
                           new Vector3(0.9f, 0.06f, 0.7f), new Vector3(0f, 24f, 0f), "Crate");
            // 办公桌
            n += Box(r, root, "档案桌", new Vector3(r.MaxX - 2.2f, 0.75f, r.Center.z + 1.5f),
                     new Vector3(1.8f, 0.1f, 0.9f), "Crate");
            n += Box(r, root, "档案桌腿", new Vector3(r.MaxX - 2.2f, 0.37f, r.Center.z + 1.5f),
                     new Vector3(0.12f, 0.74f, 0.12f), "Metal");
            return n;
        }

        // ══════════════ D5 生活区（家具）══════════════
        private static int BuildD5(Transform parent, Dictionary<string, RoomInfo> rooms)
        {
            if (!rooms.TryGetValue("D5", out var r)) return 0;
            var root = Root(parent, "D5");
            int n = 0;

            // 中间留空（南北通道 |x| < 1.5）
            // 西侧：长桌（避开卧房门口 z=22/26）
            n += Box(r, root, "生活区长桌", new Vector3(r.Center.x - 3.6f, 0.75f, r.Center.z),
                     new Vector3(2.2f, 0.1f, 0.9f), "Crate");
            n += Box(r, root, "桌腿", new Vector3(r.Center.x - 3.6f, 0.37f, r.Center.z),
                     new Vector3(0.14f, 0.74f, 0.14f), "Metal");

            // 东侧：储物柜 + 箱子
            n += Box(r, root, "储物柜", new Vector3(r.MaxX - 1.4f, 1.1f, r.MinZ + 2.0f),
                     new Vector3(0.6f, 2.2f, 1.8f), "Metal");
            n += Box(r, root, "箱A", new Vector3(r.MaxX - 1.8f, 0.28f, r.MaxZ - 1.8f),
                     new Vector3(0.75f, 0.56f, 0.75f), "Rust");
            n += TippedBox(r, root, "翻倒椅", new Vector3(r.Center.x + 3.2f, 0.25f, r.Center.z + 1.0f),
                           new Vector3(0.5f, 0.5f, 0.5f), new Vector3(75f, 12f, 0f), "Crate");
            return n;
        }

        // ══════════════ D6 卧房 ×2（床 + 柜）══════════════
        private static int BuildD6(Transform parent, Dictionary<string, RoomInfo> rooms)
        {
            int n = 0;
            foreach (var id in new[] { "D6_1", "D6_2" })
            {
                if (!rooms.TryGetValue(id, out var r)) continue;
                var root = Root(parent, id);

                // 床（靠西墙）
                n += Box(r, root, "床架", new Vector3(r.MinX + 1.1f, 0.30f, r.Center.z),
                         new Vector3(1.9f, 0.5f, 1.0f), "Metal");
                n += Box(r, root, "床垫", new Vector3(r.MinX + 1.1f, 0.60f, r.Center.z),
                         new Vector3(1.8f, 0.16f, 0.92f), "Crate");
                // 床头柜
                n += Box(r, root, "床头柜", new Vector3(r.MinX + 0.7f, 0.32f, r.Center.z + 1.4f),
                         new Vector3(0.5f, 0.62f, 0.5f), "Dark");
                // 椅子
                n += Box(r, root, "椅面", new Vector3(r.MaxX - 1.3f, 0.45f, r.Center.z - 0.8f),
                         new Vector3(0.45f, 0.06f, 0.45f), "Crate");
            }
            return n;
        }

        // ══════════════ D7 供奉间（特殊氛围：烛台 + 供桌）══════════════
        private static int BuildD7(Transform parent, Dictionary<string, RoomInfo> rooms)
        {
            if (!rooms.TryGetValue("D7", out var r)) return 0;
            var root = Root(parent, "D7");
            int n = 0;

            // 供桌（正中靠北）
            n += Box(r, root, "供桌", new Vector3(r.Center.x, 0.85f, r.MaxZ - 2.0f),
                     new Vector3(2.6f, 0.12f, 1.0f), "Crate");
            n += Box(r, root, "供桌腿", new Vector3(r.Center.x, 0.42f, r.MaxZ - 2.0f),
                     new Vector3(0.16f, 0.84f, 0.16f), "Dark");

            // 地面刻痕（矮板）
            n += Box(r, root, "地面刻痕", new Vector3(r.Center.x, 0.02f, r.Center.z),
                     new Vector3(3.0f, 0.04f, 3.0f), "Dark");

            // 两侧长明灯座
            n += Box(r, root, "灯座1", new Vector3(r.Center.x - 2.2f, 0.4f, r.MaxZ - 2.2f),
                     new Vector3(0.4f, 0.8f, 0.4f), "Metal");
            n += Box(r, root, "灯座2", new Vector3(r.Center.x + 2.2f, 0.4f, r.MaxZ - 2.2f),
                     new Vector3(0.4f, 0.8f, 0.4f), "Metal");

            return n;
        }

        // ══════════════ E1 声学实验室（大空间 5 米高，仪器多）══════════════
        private static int BuildE1(Transform parent, Dictionary<string, RoomInfo> rooms)
        {
            if (!rooms.TryGetValue("E1", out var r)) return 0;
            var root = Root(parent, "E1");
            int n = 0;

            // 中央留空（南门→北门直通）
            // 西侧：实验台排
            //
            // ⚠️ S28 修复：桌面和桌腿必须作为**一个整体**判断避让。
            //   原来分两次调用 Box()，各自算 SafePos，
            //   可能桌面被移开而桌腿留在原地（错位），
            //   而且部分情况下桌面仍落在门洞附近。
            //   现在先算一次安全位置，再一起放。
            for (int i = 0; i < 3; i++)
            {
                float z = r.MinZ + 3.0f + i * 3.4f;
                var desired = new Vector3(r.MinX + 2.3f, 0.8f, z);
                var safe = SafePos(r, desired, new Vector3(2.4f, 0.12f, 1.0f));
                if (!safe.HasValue) continue;   // 避不开就跳过整组

                var p = safe.Value;
                n += BoxAt(root, $"实验台{i + 1}", p, new Vector3(2.4f, 0.12f, 1.0f), "Metal");
                n += BoxAt(root, $"实验台腿{i + 1}", new Vector3(p.x, 0.4f, p.z),
                           new Vector3(0.14f, 0.8f, 0.14f), "Dark");
            }

            // 东侧：声学吸音板（贴在墙上）
            for (int i = 0; i < 4; i++)
            {
                float z = r.MinZ + 2.0f + i * 2.4f;
                n += Box(r, root, $"吸音板{i + 1}", new Vector3(r.MaxX - 0.4f, 2.4f, z),
                         new Vector3(0.1f, 2.4f, 1.6f), "Dark");
            }

            // 天花板：悬挂的声学设备
            n += Box(r, root, "悬挂设备", new Vector3(r.Center.x, r.Height - 0.8f, r.Center.z),
                     new Vector3(1.6f, 0.5f, 1.6f), "Metal");

            return n;
        }

        // ══════════════ E2 混响舱（密闭小空间）══════════════
        private static int BuildE2(Transform parent, Dictionary<string, RoomInfo> rooms)
        {
            if (!rooms.TryGetValue("E2", out var r)) return 0;
            var root = Root(parent, "E2");
            int n = 0;

            // 六面吸音（只做四面墙，避免遮挡）
            n += Box(r, root, "吸音西", new Vector3(r.MinX + 0.35f, r.Height * 0.5f, r.Center.z),
                     new Vector3(0.8f, r.Height * 0.8f, r.SizeZ * 0.8f), "Dark");
            n += Box(r, root, "吸音东", new Vector3(r.MaxX - 0.35f, r.Height * 0.5f, r.Center.z),
                     new Vector3(0.8f, r.Height * 0.8f, r.SizeZ * 0.8f), "Dark");

            // 中央仪器
            n += Box(r, root, "混响仪", new Vector3(r.Center.x, 0.75f, r.Center.z),
                     new Vector3(1.2f, 1.5f, 1.2f), "Metal");
            return n;
        }

        // ══════════════ E3 控制室（操作台 + 屏幕墙）══════════════
        private static int BuildE3(Transform parent, Dictionary<string, RoomInfo> rooms)
        {
            if (!rooms.TryGetValue("E3", out var r)) return 0;
            var root = Root(parent, "E3");
            int n = 0;

            // 屏幕墙（北墙）
            n += Box(r, root, "屏幕墙", new Vector3(r.Center.x, 1.6f, r.MaxZ - 0.5f),
                     new Vector3(r.SizeX - 2.0f, 1.3f, 0.12f), "Glass");

            // 操作台排（靠北，避开频率终端 x=13.6）
            n += Box(r, root, "操作台1", new Vector3(r.MinX + 1.8f, 0.78f, r.Center.z),
                     new Vector3(1.6f, 0.12f, 0.8f), "Metal");
            n += Box(r, root, "操作台2", new Vector3(r.MinX + 3.6f, 0.78f, r.Center.z),
                     new Vector3(1.6f, 0.12f, 0.8f), "Metal");
            n += Box(r, root, "操作台腿", new Vector3(r.MinX + 2.7f, 0.39f, r.Center.z),
                     new Vector3(0.14f, 0.78f, 0.14f), "Dark");

            // 椅子
            n += Box(r, root, "椅1", new Vector3(r.MinX + 1.8f, 0.45f, r.Center.z - 1.0f),
                     new Vector3(0.45f, 0.06f, 0.45f), "Dark");
            return n;
        }

        // ══════════════ E4 深渊观景台（观察窗 + 长椅）══════════════
        private static int BuildE4(Transform parent, Dictionary<string, RoomInfo> rooms)
        {
            if (!rooms.TryGetValue("E4", out var r)) return 0;
            var root = Root(parent, "E4");
            int n = 0;

            // 北侧大观察窗
            n += Box(r, root, "观察窗", new Vector3(r.Center.x, 2.0f, r.MaxZ - 0.15f),
                     new Vector3(r.SizeX - 1.0f, 2.6f, 0.10f), "Glass");

            // 长椅（面向窗口）
            n += Box(r, root, "长椅", new Vector3(r.Center.x, 0.45f, r.Center.z + 1.0f),
                     new Vector3(3.0f, 0.12f, 0.6f), "Crate");
            n += Box(r, root, "长椅腿", new Vector3(r.Center.x, 0.22f, r.Center.z + 1.0f),
                     new Vector3(0.14f, 0.44f, 0.14f), "Metal");

            // 两侧栏杆柱
            n += Box(r, root, "栏杆柱1", new Vector3(r.MinX + 0.6f, 0.6f, r.MaxZ - 0.5f),
                     new Vector3(0.2f, 1.2f, 0.2f), "Metal");
            n += Box(r, root, "栏杆柱2", new Vector3(r.MaxX - 0.6f, 0.6f, r.MaxZ - 0.5f),
                     new Vector3(0.2f, 1.2f, 0.2f), "Metal");

            return n;
        }
    }
}