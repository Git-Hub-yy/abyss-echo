using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S43：第二章拾取物（补上"物品栏在第二章用不上"的缺口）。
    ///
    /// ══════════════════════ 设计 ══════════════════════
    ///
    ///  第二章原来只有交互物（水泵、频率终端、录音机、便条），
    ///  一个可拾取物品都没有 —— 玩家按 Tab 打开物品栏会是空的。
    ///
    ///  这里补两样，且都要**有用途**，不能只是摆设：
    ///
    ///    ① 录音带（D6_2 卧房）
    ///       可以带走。带到第三章的祭室可以作为供奉物（→ 结局 A）。
    ///       ← 这是打通第二、三章的关键物品
    ///
    ///    ② 工作手套（D3 泵房）
    ///       在 D3 泵房捡到，让"抽水泵"的启动更合理。
    ///       同时在物品栏里给玩家一个"我准备过了"的心理暗示。
    ///
    /// ══════════════════════ 安全规则 ══════════════════════
    ///
    ///  拾取物必须避开：
    ///    · 门洞通行区（用与道具相同的 SafePos 逻辑）
    ///    · 已有关键交互物（水泵、录音机、终端、便条）的 1.2 米范围内
    /// </summary>
    public static class ChapterTwoPickups
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter2_Graybox.unity";
        private const string RootName = "--- PICKUPS_CH2 ---";

        [MenuItem("ABYSS ECHO/S43 布置第二章拾取物")]
        public static void RunAll()
        {
            var prev = AbyssEcho.LevelGen.ChapterLayout.Current;
            AbyssEcho.LevelGen.ChapterLayout.Current = AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter2;

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError("[Ch2Pickups] 无法打开第二章场景");
                EditorApplication.Exit(1);
                return;
            }

            var old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);

            var root = new GameObject(RootName);
            var rooms = CollectRooms();

            // 收集已有关键交互物的位置，避免拾取物压在它们身上
            var occupied = new List<Vector3>();

            foreach (var p in Object.FindObjectsByType<AbyssEcho.Interaction.PickupItem>(FindObjectsSortMode.None))
                occupied.Add(p.transform.position);
            foreach (var t in Object.FindObjectsByType<AbyssEcho.Puzzle.TapeRecorder>(FindObjectsSortMode.None))
                occupied.Add(t.transform.position);
            foreach (var w in Object.FindObjectsByType<AbyssEcho.Puzzle.WaterPumpSwitch>(FindObjectsSortMode.None))
                occupied.Add(w.transform.position);
            foreach (var fc in Object.FindObjectsByType<AbyssEcho.Puzzle.FrequencyConsole>(FindObjectsSortMode.None))
                occupied.Add(fc.transform.position);

            int n = 0;
            n += PlaceTape(root.transform, rooms, occupied);
            n += PlaceGlove(root.transform, rooms, occupied);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log($"<b>[Ch2Pickups]</b> 第二章拾取物布置完成，共 {n} 件");
            AbyssEcho.LevelGen.ChapterLayout.Current = prev;
        }

        private class RoomInfo
        {
            public string Id;
            public Vector3 Center;
            public float SizeX, SizeZ;
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
                         AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter2))
            {
                var info = new RoomInfo
                {
                    Id = r.Id,
                    Center = new Vector3(r.Center.x, 0f, r.Center.z),
                    SizeX = r.SizeX,
                    SizeZ = r.SizeZ
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

        /// <summary>找一个不挡门洞、不压已有关键物的安全位置。</summary>
        private static Vector3? SafePos(RoomInfo r, Vector3 desired,
                                        List<Vector3> occupied,
                                        float doorClearance = 2.2f,
                                        float occupiedClearance = 1.2f)
        {
            // ⚠️ D6 卧房只有 5x4 米，门 + 录音机几乎占满，
            //    所以候选点要覆盖整个房间（步长小、范围大），
            //    并把边距从 0.8 收到 0.5。
            var candidates = new List<Vector3> { desired };
            for (float ox = -2.0f; ox <= 2.0f; ox += 0.5f)
                for (float oz = -1.6f; oz <= 1.6f; oz += 0.4f)
                    candidates.Add(desired + new Vector3(ox, 0f, oz));

            foreach (var c in candidates)
            {
                // 必须在房间内（留 0.5 米边距）
                if (c.x < r.MinX + 0.5f || c.x > r.MaxX - 0.5f) continue;
                if (c.z < r.MinZ + 0.5f || c.z > r.MaxZ - 0.5f) continue;

                // 不挡门洞
                bool blocked = false;
                foreach (var d in r.Doorways)
                {
                    float dx = c.x - d.x, dz = c.z - d.z;
                    if (Mathf.Sqrt(dx * dx + dz * dz) < doorClearance) { blocked = true; break; }
                }
                if (blocked) continue;

                // 不压已有关键交互物
                bool overlapped = false;
                foreach (var o in occupied)
                {
                    float dx = c.x - o.x, dz = c.z - o.z;
                    if (Mathf.Sqrt(dx * dx + dz * dz) < occupiedClearance) { overlapped = true; break; }
                }
                if (overlapped) continue;

                return c;
            }
            return null;
        }

        /// <summary>录音带（D6_2 卧房，与录音机同屋但错开）。</summary>
        private static int PlaceTape(Transform parent, Dictionary<string, RoomInfo> rooms, List<Vector3> occupied)
        {
            if (!rooms.TryGetValue("D6_2", out var r))
            {
                Debug.LogWarning("[Ch2Pickups] 找不到 D6_2 卧房");
                return 0;
            }

            var desired = new Vector3(r.Center.x + 1.0f, 0.85f, r.Center.z + 0.8f);
            var safe = SafePos(r, new Vector3(desired.x, 0f, desired.z), occupied);
            if (!safe.HasValue)
            {
                Debug.LogWarning("[Ch2Pickups] 录音带找不到安全位置，跳过");
                return 0;
            }

            return MakePickup(parent, "Pickup_Tape_Ch2", "录音带",
                              new Vector3(safe.Value.x, desired.y, safe.Value.z),
                              AbyssEcho.Inventory.ItemIds.Tape, "录音带",
                              new Vector3(0.22f, 0.05f, 0.14f),
                              new Color(0.55f, 0.35f, 0.20f));
        }

        /// <summary>工作手套（D3 泵房）。</summary>
        private static int PlaceGlove(Transform parent, Dictionary<string, RoomInfo> rooms, List<Vector3> occupied)
        {
            if (!rooms.TryGetValue("D3", out var r))
            {
                Debug.LogWarning("[Ch2Pickups] 找不到 D3 泵房");
                return 0;
            }

            var desired = new Vector3(r.Center.x + 1.5f, 0.85f, r.Center.z - 1.0f);
            var safe = SafePos(r, new Vector3(desired.x, 0f, desired.z), occupied);
            if (!safe.HasValue)
            {
                Debug.LogWarning("[Ch2Pickups] 工作手套找不到安全位置，跳过");
                return 0;
            }

            return MakePickup(parent, "Pickup_Glove_Ch2", "工作手套",
                              new Vector3(safe.Value.x, desired.y, safe.Value.z),
                              AbyssEcho.Inventory.ItemIds.Glove, "工作手套",
                              new Vector3(0.26f, 0.10f, 0.16f),
                              new Color(0.62f, 0.50f, 0.32f));
        }

        private static int MakePickup(Transform parent, string name, string display,
                                      Vector3 pos, string itemId, string label,
                                      Vector3 size, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = size;

            go.GetComponent<Renderer>().sharedMaterial = MakeMaterial(itemId, color);

            var pickup = go.AddComponent<AbyssEcho.Interaction.PickupItem>();
            pickup.itemId = itemId;
            pickup.displayName = display;

            Debug.Log($"[Ch2Pickups] 放置 {name}（{display}）@ {pos}");
            return 1;
        }

        private static Material MakeMaterial(string itemId, Color c)
        {
            const string folder = "Assets/_Project/Art/Materials/Ch2";
            if (!AssetDatabase.IsValidFolder(folder))
            {
                if (!AssetDatabase.IsValidFolder("Assets/_Project/Art/Materials"))
                    AssetDatabase.CreateFolder("Assets/_Project/Art", "Materials");
                if (!AssetDatabase.IsValidFolder(folder))
                    AssetDatabase.CreateFolder("Assets/_Project/Art/Materials", "Ch2");
            }

            string safe = itemId.Replace(".", "_");
            string path = $"{folder}/M_Pickup_{safe}.mat";

            var exist = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (exist != null) return exist;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(shader) { name = "M_Pickup_" + safe };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            mat.color = c;
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", c * 0.6f);
            }
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }
    }
}