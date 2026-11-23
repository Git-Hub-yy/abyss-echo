using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S38：第四章 G 区道具布置。
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
    public static class ChapterFourProps
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter4_Graybox.unity";
        private const string RootName = "--- PROPS_CH4 ---";

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

            string folder = "Assets/_Project/Art/Materials/Ch4";
            EnsureFolder(folder);
            string path = $"{folder}/M_Ch4_{key}.mat";

            var exist = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (exist != null) { _cache[key] = exist; return exist; }

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(shader) { name = "M_Ch4_" + key };
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

        [MenuItem("ABYSS ECHO/S38 布置第四章道具")]
        public static void Run()
        {
            var prev = AbyssEcho.LevelGen.ChapterLayout.Current;
            AbyssEcho.LevelGen.ChapterLayout.Current = AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter4;

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid()) { Debug.LogError("[Ch4Props] 无法打开场景"); return; }

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

            Debug.Log($"<b>[Ch4Props]</b> 第四章道具布置完成，共 {n} 件");
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
                         AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter4))
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
                case "G1":
                    // 入口腔：这里是第三章掉下来的地方，有坠落痕迹
                    n += Box(r, root, "坠落残骸", new Vector3(r.Center.x, 0.2f, r.Center.z - 1.5f),
                               new Vector3(2.4f, 0.4f, 2.0f), "Metal");
                    n += Tipped(r, root, "断裂的舱门", new Vector3(r.Center.x + 2.0f, 0.3f, r.Center.z + 1.0f),
                                new Vector3(1.6f, 0.12f, 2.2f), new Vector3(22f, 35f, 8f), "Rust");
                    break;

                case "G2":
                    // 环形廊：血管状结构沿墙爬
                    for (int i = 0; i < 6; i++)
                    {
                        float x = r.MinX + 3f + i * 3.6f;
                        n += Box(r, root, $"血管{i + 1}", new Vector3(x, 1.2f, r.MinZ + 0.5f),
                                 new Vector3(3.0f, 0.35f, 0.35f), "Vein");
                    }
                    // 地面肉膜
                    n += Box(r, root, "肉膜", new Vector3(r.Center.x, 0.03f, r.Center.z),
                               new Vector3(r.SizeX - 3f, 0.06f, r.SizeZ - 1.2f), "FleshDark");
                    break;

                case "G3":
                    // 消化囊：中央有个大肉瘤
                    n += Box(r, root, "肉瘤", new Vector3(r.Center.x, 1.0f, r.Center.z),
                               new Vector3(3.0f, 2.0f, 3.0f), "Flesh");
                    n += Tipped(r, root, "被吞掉的仪器", new Vector3(r.Center.x + 3.0f, 0.5f, r.Center.z),
                                new Vector3(1.2f, 1.0f, 0.8f), new Vector3(15f, 40f, 25f), "Metal");
                    break;

                case "G4":
                    // 膜层：半透明的膜面
                    //
                    // ⚠️ 膜面是贴墙的薄片（厚 0.15 米），不会挡住 2.4 米宽的门洞，
                    //    但 SafePos 按"最长边 / 2"算半径，会误判成挡路并整个丢弃。
                    //    这里直接用 BoxAt（不检查），位置固定在墙内侧 0.6 米。
                    // 贴在**北墙**（G4 只有西门和北门，西门在 x=MinX）；
                    // 原来贴西墙会正好挡住西门。
                    //
                    // G4 的门：西门(x=MinX) + 北门(z=MaxZ)。
                    // 所以膜面只能贴**南墙**和**东墙**，否则挡门。
                    n += BoxAt(root, "膜面1", new Vector3(r.Center.x, r.Height * 0.5f, r.MinZ + 0.5f),
                               new Vector3(r.SizeX - 2f, r.Height - 0.8f, 0.15f), "Flesh");
                    n += BoxAt(root, "膜面2", new Vector3(r.MaxX - 0.5f, r.Height * 0.5f, r.Center.z),
                               new Vector3(0.15f, r.Height - 0.8f, r.SizeZ - 2f), "Flesh");
                    break;

                case "G5":
                    // 组织层：肉质的柱子
                    for (int i = 0; i < 4; i++)
                    {
                        float x = r.MinX + 2.5f + i * 2.4f;
                        n += Box(r, root, $"肉柱{i + 1}", new Vector3(x, 1.8f, r.MinZ + 2.0f),
                                 new Vector3(0.7f, 3.6f, 0.7f), "Flesh");
                    }
                    break;

                case "G6":
                    // 神经束：大量细长条
                    for (int i = 0; i < 8; i++)
                    {
                        float z = r.MinZ + 1.5f + i * 1.0f;
                        n += Box(r, root, $"神经{i + 1}", new Vector3(r.Center.x, 2.6f, z),
                                   new Vector3(r.SizeX - 2f, 0.12f, 0.12f), "Vein");
                    }
                    break;

                case "G7":
                    // 核心腔：中央核心 + 环状结构
                    n += Box(r, root, "核心", new Vector3(r.Center.x, 2.2f, r.Center.z),
                               new Vector3(4.0f, 4.4f, 4.0f), "Flesh");
                    n += Box(r, root, "核心光晕", new Vector3(r.Center.x, 4.6f, r.Center.z),
                               new Vector3(1.2f, 0.3f, 1.2f), "Bone");
                    for (int i = 0; i < 4; i++)
                    {
                        float ang = i * 90f * Mathf.Deg2Rad;
                        float rad = 6.5f;
                        n += Box(r, root, $"环柱{i + 1}",
                                   new Vector3(r.Center.x + Mathf.Cos(ang) * rad, 1.6f,
                                               r.Center.z + Mathf.Sin(ang) * rad),
                                   new Vector3(0.5f, 3.2f, 0.5f), "Bone");
                    }
                    break;

                case "G8":
                    // 淋巴池：地面深色液面
                    // ⚠️ 淋巴液面是贴地薄层（高 0.16 米），玩家踩得过去，
                    //    但 SafePos 判定它"很大"→直接丢弃。这里手动创建并去掉碰撞体。
                    {
                        var lymph = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        lymph.name = "淋巴液面";
                        lymph.transform.SetParent(root, false);
                        lymph.transform.position = new Vector3(r.Center.x, 0.08f, r.Center.z);
                        lymph.transform.localScale = new Vector3(r.SizeX - 2f, 0.16f, r.SizeZ - 2f);
                        lymph.GetComponent<Renderer>().sharedMaterial = GetMat("FleshDark");
                        var lc = lymph.GetComponent<Collider>();
                        if (lc != null) Object.DestroyImmediate(lc);
                        n++;
                    }
                    break;

                case "G9":
                    // 骨壁：成排的肋骨状结构
                    for (int i = 0; i < 5; i++)
                    {
                        float z = r.MinZ + 1.2f + i * 1.0f;
                        n += Box(r, root, $"肋骨{i + 1}", new Vector3(r.Center.x, 1.4f, z),
                                   new Vector3(r.SizeX - 2f, 0.22f, 0.22f), "Bone");
                    }
                    break;

                case "G10":
                    // 旧观测点：老基地残留
                    //
                    // ⚠️ G10 只有 6x5，门在南墙（G2 那一侧）。
                    //    观测窗原本贴南墙，检查器判定它挡住了门洞。
                    //    现在贴到**北墙**（离门最远），并且保存高度只到 1.8（不挡视线）。
                    n += Box(r, root, "观测窗残框", new Vector3(r.Center.x, 1.5f, r.MinZ + 0.5f),
                               new Vector3(2.0f, 1.6f, 0.12f), "Metal");
                    n += Tipped(r, root, "倒塌的椅子", new Vector3(r.Center.x + 1.5f, 0.25f, r.Center.z),
                                new Vector3(0.5f, 0.5f, 0.5f), new Vector3(70f, 25f, 0f), "Metal");
                    break;

                case "G11":
                    // 样本库：一排排样本盒
                    for (int i = 0; i < 4; i++)
                        for (int j = 0; j < 2; j++)
                            n += Box(r, root, $"样本箱{i * 2 + j + 1}",
                                     new Vector3(r.MinX + 1.4f + i * 1.2f, 0.35f, r.MinZ + 1.5f + j * 1.5f),
                                     new Vector3(0.8f, 0.7f, 0.8f), "Bone");
                    break;
            }

            return n;
        }
    }
}
