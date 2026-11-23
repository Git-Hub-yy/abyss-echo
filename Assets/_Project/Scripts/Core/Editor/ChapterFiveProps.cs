using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S38：第五章 G 区道具布置。
    ///
    /// 主题：**这里不是建筑，是某种东西的内部。**
    ///
    /// 所以道具不是"人留下的家具"，而是：
    ///   · 有机的、软的、在生长的东西（肉瘤、膜、脉）
    ///   · 残留的人造物（说明这里曾经是基地，但已经被"吞"了）
    ///   · 神经/血管状的结构（用长条方块表现）
    ///
    /// 严格遵守：所有道具必须走 SafePos 避让门洞。
    /// </summary>
    public static class ChapterFiveProps
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter5_Graybox.unity";
        private const string RootName = "--- PROPS_CH5 ---";

        private static readonly Dictionary<string, Material> _cache = new Dictionary<string, Material>();

        private static Material GetMat(string key)
        {
            if (_cache.TryGetValue(key, out var m) && m != null) return m;

            Color c; float metallic, smooth;
            switch (key)
            {
                case "Flesh": c = new Color(0.42f, 0.18f, 0.18f); metallic = 0f; smooth = 0.35f; break;
                case "FleshDark": c = new Color(0.24f, 0.10f, 0.11f); metallic = 0f; smooth = 0.25f; break;
                case "Bone": c = new Color(0.72f, 0.68f, 0.58f); metallic = 0f; smooth = 0.30f; break;
                case "Vein": c = new Color(0.50f, 0.12f, 0.14f); metallic = 0.1f; smooth = 0.55f; break;
                case "Metal": c = new Color(0.36f, 0.34f, 0.33f); metallic = 0.55f; smooth = 0.30f; break;
                case "Rust": c = new Color(0.34f, 0.22f, 0.16f); metallic = 0.2f; smooth = 0.15f; break;
                default: c = new Color(0.35f, 0.30f, 0.30f); metallic = 0.1f; smooth = 0.2f; break;
            }

            string folder = "Assets/_Project/Art/Materials/Ch5";
            EnsureFolder(folder);
            string path = $"{folder}/M_Ch5_{key}.mat";

            var exist = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (exist != null) { _cache[key] = exist; return exist; }

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(shader) { name = "M_Ch5_" + key };
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

        [MenuItem("ABYSS ECHO/S40 布置第五章道具")]
        public static void Run()
        {
            var prev = AbyssEcho.LevelGen.ChapterLayout.Current;
            AbyssEcho.LevelGen.ChapterLayout.Current = AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter5;

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid()) { Debug.LogError("[Ch5Props] 无法打开场景"); return; }

            var old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);

            var root = new GameObject(RootName);
            var rooms = CollectRooms();

            int n = 0;
            foreach (var kv in rooms)
                n += BuildRoom(root.transform, kv.Value);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log($"<b>[Ch5Props]</b> 第五章道具布置完成，共 {n} 件");
            AbyssEcho.LevelGen.ChapterLayout.Current = prev;
        }

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
            var d = new Dictionary<string, RoomInfo>();
            foreach (var r in AbyssEcho.LevelGen.ChapterLayout.GetRooms(
                         AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter5))
            {
                var info = new RoomInfo
                {
                    Id = r.Id,
                    Center = new Vector3(r.Center.x, 0f, r.Center.z),
                    SizeX = r.SizeX,
                    SizeZ = r.SizeZ,
                    Height = r.Height
                };
                foreach (var dw in r.Doorways)
                {
                    Vector3 p;
                    switch (dw.Side)
                    {
                        case AbyssEcho.LevelGen.WallSide.North: p = new Vector3(r.Center.x + dw.Offset, 0f, r.MaxZ); break;
                        case AbyssEcho.LevelGen.WallSide.South: p = new Vector3(r.Center.x + dw.Offset, 0f, r.MinZ); break;
                        case AbyssEcho.LevelGen.WallSide.East: p = new Vector3(r.MaxX, 0f, r.Center.z + dw.Offset); break;
                        default: p = new Vector3(r.MinX, 0f, r.Center.z + dw.Offset); break;
                    }
                    info.Doorways.Add(p);
                }
                d[r.Id] = info;
            }
            return d;
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

        // ══════════════════ 按房间生成 ══════════════════

        private static int BuildRoom(Transform p, RoomInfo r)
        {
            var root = Root(p, r.Id);
            int n = 0;

            switch (r.Id)
            {
                case "H1":
                    // 底层平台：从 G 区下来的地方，有攀爬痕迹
                    n += Box(r, root, "攀爬架", new Vector3(r.Center.x - 2.5f, 1.2f, r.Center.z),
                             new Vector3(1.6f, 2.4f, 1.2f), "Metal");
                    n += Tipped(r, root, "掉落的工具箱", new Vector3(r.Center.x + 2.5f, 0.25f, r.Center.z + 1.0f),
                                new Vector3(0.8f, 0.5f, 0.6f), new Vector3(8f, 30f, 15f), "Rust");
                    break;

                case "H2":
                    // 上行廊：两侧有扶手与灯带
                    for (int i = 0; i < 5; i++)
                    {
                        float z = r.MinZ + 2f + i * 2.6f;
                        n += Box(r, root, $"扶手{i + 1}", new Vector3(r.MinX + 0.8f, 0.95f, z),
                                 new Vector3(0.12f, 0.12f, 2.0f), "Metal");
                    }
                    break;

                case "H3":
                    // 中转平台：空旷，中央有指示牌
                    n += Box(r, root, "指示牌", new Vector3(r.Center.x, 1.6f, r.Center.z),
                             new Vector3(0.15f, 1.2f, 2.4f), "Rust");
                    n += Box(r, root, "指示牌杆", new Vector3(r.Center.x, 0.6f, r.Center.z),
                             new Vector3(0.15f, 1.2f, 0.15f), "Metal");
                    // 两侧长椅
                    n += Box(r, root, "长椅1", new Vector3(r.Center.x - 4.5f, 0.45f, r.Center.z),
                             new Vector3(2.4f, 0.12f, 0.5f), "Metal");
                    n += Box(r, root, "长椅2", new Vector3(r.Center.x + 4.5f, 0.45f, r.Center.z),
                             new Vector3(2.4f, 0.12f, 0.5f), "Metal");
                    break;

                case "H4":
                    // 值班室：老基地的办公痕迹
                    n += Box(r, root, "值班桌", new Vector3(r.Center.x, 0.76f, r.Center.z),
                             new Vector3(2.0f, 0.12f, 1.0f), "Rust");
                    n += Box(r, root, "值班桌腿", new Vector3(r.Center.x, 0.38f, r.Center.z),
                             new Vector3(0.16f, 0.76f, 0.16f), "Metal");
                    n += Tipped(r, root, "倒下的椅", new Vector3(r.Center.x - 1.8f, 0.25f, r.Center.z + 0.8f),
                                new Vector3(0.5f, 0.5f, 0.5f), new Vector3(75f, 20f, 0f), "Metal");
                    n += Box(r, root, "文件柜", new Vector3(r.MinX + 1.2f, 1.1f, r.MinZ + 1.5f),
                             new Vector3(0.6f, 2.2f, 1.4f), "Metal");
                    break;

                case "H5":
                    // 仓库：堆叠的物资箱
                    for (int i = 0; i < 3; i++)
                        for (int j = 0; j < 2; j++)
                            n += Box(r, root, $"物资箱{i * 2 + j + 1}",
                                     new Vector3(r.MinX + 1.6f + i * 1.4f, 0.35f, r.MinZ + 1.6f + j * 1.8f),
                                     new Vector3(0.9f, 0.7f, 0.9f), "Rust");
                    break;

                case "H6":
                    // 升降井：中央的绞盘与支架
                    n += Box(r, root, "绞盘", new Vector3(r.Center.x, 1.2f, r.Center.z),
                             new Vector3(2.0f, 2.4f, 2.0f), "Metal");
                    n += Box(r, root, "绞盘顶", new Vector3(r.Center.x, 2.6f, r.Center.z),
                             new Vector3(2.4f, 0.2f, 2.4f), "Rust");
                    break;

                case "H7":
                    // 出口舱：核心视觉 —— 一扇巨大的舱门
                    n += Box(r, root, "出口舱门", new Vector3(r.Center.x, 2.4f, r.MaxZ - 0.4f),
                             new Vector3(5.0f, 4.8f, 0.2f), "Metal");
                    n += Box(r, root, "舱门框", new Vector3(r.Center.x, 2.4f, r.MaxZ - 0.5f),
                             new Vector3(6.0f, 5.4f, 0.12f), "Rust");
                    n += Box(r, root, "控制台", new Vector3(r.Center.x - 3.0f, 0.8f, r.MaxZ - 2.0f),
                             new Vector3(1.6f, 1.0f, 0.8f), "Metal");
                    // 地面标记（通往舱门的引导线）
                    n += Box(r, root, "引导线", new Vector3(r.Center.x, 0.03f, r.Center.z),
                             new Vector3(0.4f, 0.06f, 8f), "Bone");
                    break;

                case "H8":
                    // 旧舱室：被遗弃的居住空间
                    n += Box(r, root, "旧床", new Vector3(r.MinX + 1.2f, 0.28f, r.Center.z),
                             new Vector3(1.9f, 0.5f, 1.0f), "Metal");
                    n += Box(r, root, "旧床垫", new Vector3(r.MinX + 1.2f, 0.6f, r.Center.z),
                             new Vector3(1.8f, 0.16f, 0.92f), "Rust");
                    n += Box(r, root, "储物箱", new Vector3(r.MaxX - 1.5f, 0.35f, r.MaxZ - 1.5f),
                             new Vector3(0.8f, 0.7f, 0.8f), "Rust");
                    break;

                case "H9":
                    // 封存区：一排封条箱
                    for (int i = 0; i < 4; i++)
                        n += Box(r, root, $"封存箱{i + 1}",
                                 new Vector3(r.MinX + 1.4f + i * 1.6f, 0.5f, r.Center.z),
                                 new Vector3(0.9f, 1.0f, 1.4f), "Metal");
                    break;

                case "H10":
                    // 工具间
                    n += Box(r, root, "工具台", new Vector3(r.Center.x, 0.8f, r.Center.z),
                             new Vector3(2.0f, 0.12f, 0.9f), "Metal");
                    break;

                case "H11":
                    // 医疗舱
                    n += Box(r, root, "病床", new Vector3(r.Center.x, 0.45f, r.Center.z),
                             new Vector3(1.0f, 0.8f, 2.0f), "Metal");
                    n += Box(r, root, "器械柜", new Vector3(r.MinX + 1.2f, 1.0f, r.MinZ + 1.5f),
                             new Vector3(0.6f, 2.0f, 1.2f), "Rust");
                    break;
            }

            return n;
        }
    }
}
