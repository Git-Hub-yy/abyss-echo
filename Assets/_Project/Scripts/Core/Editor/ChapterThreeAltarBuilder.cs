using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S35：把供奉台系统装进第三章场景。
    ///
    /// 做完的事：
    ///   · 给 F9 祭室的「供奉台」挂 OfferingAltar 脚本
    ///   · 关联通往 F7 的门（如果还没有就先建一个）
    ///   · 放置可拾取的录音带（F5 起居区）和灯泡（F4 工作间）
    ///   · 建「纪念品架」—— 因为第二章的录音带是固定的，
    ///     这里放一个可以真正拿走的录音带作为供奉选项
    /// </summary>
    public static class ChapterThreeAltarBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter3_Graybox.unity";
        private const string RootName = "--- ALTAR_CH3 ---";

        [MenuItem("ABYSS ECHO/S35 装配第三章供奉台")]
        public static void RunAll()
        {
            var prev = AbyssEcho.LevelGen.ChapterLayout.Current;
            AbyssEcho.LevelGen.ChapterLayout.Current = AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter3;

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid()) { Debug.LogError("[Ch3Altar] 无法打开场景"); return; }

            var old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);

            var root = new GameObject(RootName);
            var rooms = CollectRooms();

            int n = 0;
            n += AttachAltar(root.transform, rooms);
            n += PlaceTape(root.transform, rooms);
            n += PlaceBulb(root.transform, rooms);
            n += PlaceFuseChip(root.transform, rooms);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log($"<b>[Ch3Altar]</b> 供奉台系统装配完成：{n} 项");
            AbyssEcho.LevelGen.ChapterLayout.Current = prev;
        }

        private class RoomInfo
        {
            public string Id;
            public Vector3 Center;
            public float SizeX, SizeZ, Height;
        }

        private static System.Collections.Generic.Dictionary<string, RoomInfo> CollectRooms()
        {
            var d = new System.Collections.Generic.Dictionary<string, RoomInfo>();
            foreach (var r in AbyssEcho.LevelGen.ChapterLayout.GetRooms(
                         AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter3))
            {
                d[r.Id] = new RoomInfo
                {
                    Id = r.Id,
                    Center = new Vector3(r.Center.x, 0f, r.Center.z),
                    SizeX = r.SizeX,
                    SizeZ = r.SizeZ,
                    Height = r.Height
                };
            }
            return d;
        }

        /// <summary>给 F9 的供奉台挂上 OfferingAltar。</summary>
        private static int AttachAltar(Transform parent, System.Collections.Generic.Dictionary<string, RoomInfo> rooms)
        {
            // 找场景里已有的「供奉台」（由 ChapterThreeProps 生成）
            GameObject altarGo = null;
            var propsRoot = GameObject.Find("--- PROPS_CH3 ---");
            if (propsRoot != null)
            {
                foreach (var t in propsRoot.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "供奉台") { altarGo = t.gameObject; break; }
                }
            }

            if (altarGo == null)
            {
                Debug.LogWarning("[Ch3Altar] 找不到「供奉台」道具，创建一个临时台子");
                altarGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                altarGo.name = "供奉台";
                altarGo.transform.SetParent(parent, false);
                altarGo.transform.position = new Vector3(0f, 0.5f, 44f);
                altarGo.transform.localScale = new Vector3(1.4f, 1.0f, 1.4f);
            }

            // 供品标记（放上去后显示的小方块）
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "供品标记";
            marker.transform.SetParent(altarGo.transform, false);
            marker.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            marker.transform.localScale = new Vector3(0.3f, 0.06f, 0.22f);
            var col = marker.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            var mr = marker.GetComponent<Renderer>();
            mr.sharedMaterial = MakeMarkerMaterial();
            marker.SetActive(false);

            var altar = altarGo.GetComponent<AbyssEcho.Puzzle.OfferingAltar>();
            if (altar == null) altar = altarGo.AddComponent<AbyssEcho.Puzzle.OfferingAltar>();

            altar.itemForEndingA = AbyssEcho.Inventory.ItemIds.Tape;
            altar.itemForEndingC = AbyssEcho.Inventory.ItemIds.Fuse;
            altar.offeringMarker = marker.transform;
            altar.promptNeedItem = "石台是空的 —— 把一样东西放上去";
            altar.promptAlready = "已经放上去了";

            // 关联通往 F7 的门（如果有 DoorInteractable 名字含 F15/F7）
            var doors = Object.FindObjectsByType<AbyssEcho.Interaction.DoorInteractable>(FindObjectsSortMode.None);
            foreach (var d in doors)
            {
                if (d.name.Contains("F15_F7"))
                {
                    altar.doorToUnlock = d;
                    Debug.Log($"[Ch3Altar] 供奉台关联门 {d.name}");
                    break;
                }
            }

            Debug.Log($"[Ch3Altar] 供奉台已装配 @ {altarGo.transform.position}");
            return 1;
        }

        private static Material MakeMarkerMaterial()
        {
            const string folder = "Assets/_Project/Art/Materials/Ch3";
            if (!AssetDatabase.IsValidFolder(folder))
            {
                if (!AssetDatabase.IsValidFolder("Assets/_Project/Art"))
                    AssetDatabase.CreateFolder("Assets/_Project", "Art");
                if (!AssetDatabase.IsValidFolder("Assets/_Project/Art/Materials"))
                    AssetDatabase.CreateFolder("Assets/_Project/Art", "Materials");
                if (!AssetDatabase.IsValidFolder(folder))
                    AssetDatabase.CreateFolder("Assets/_Project/Art/Materials", "Ch3");
            }

            string path = folder + "/M_Ch3_Offering.mat";
            var exist = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (exist != null) return exist;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(shader) { name = "M_Ch3_Offering" };
            var c = new Color(0.85f, 0.72f, 0.42f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            mat.color = c;
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", c * 1.6f);
            }
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        /// <summary>在 F5 起居区放一个可拾取的录音带（结局 A 的供品）。</summary>
        private static int PlaceTape(Transform parent, System.Collections.Generic.Dictionary<string, RoomInfo> rooms)
        {
            if (!rooms.TryGetValue("F5", out var r)) return 0;
            return MakePickup(parent, "Pickup_Tape", "录音带",
                              new Vector3(r.Center.x + 0.4f, 0.86f, r.Center.z),
                              AbyssEcho.Inventory.ItemIds.Tape, "物品.录音带",
                              new Vector3(0.22f, 0.05f, 0.14f), new Color(0.55f, 0.35f, 0.20f));
        }

        /// <summary>在 F4 工作间放灯泡（修灯玩法用）。</summary>
        private static int PlaceBulb(Transform parent, System.Collections.Generic.Dictionary<string, RoomInfo> rooms)
        {
            if (!rooms.TryGetValue("F4", out var r)) return 0;
            return MakePickup(parent, "Pickup_Bulb", "灯泡",
                              new Vector3(r.Center.x, 0.9f, r.Center.z),
                              AbyssEcho.Inventory.ItemIds.Bulb, "物品.灯泡",
                              new Vector3(0.1f, 0.14f, 0.1f), new Color(0.9f, 0.9f, 0.8f));
        }

        /// <summary>在 F3 档案室放一段保险丝（结局 C 的供品）。</summary>
        private static int PlaceFuseChip(Transform parent, System.Collections.Generic.Dictionary<string, RoomInfo> rooms)
        {
            if (!rooms.TryGetValue("F3", out var r)) return 0;
            return MakePickup(parent, "Pickup_Fuse", "保险丝",
                              new Vector3(r.Center.x - 1.5f, 0.9f, r.Center.z + 1.5f),
                              AbyssEcho.Inventory.ItemIds.Fuse, "物品.保险丝",
                              new Vector3(0.12f, 0.14f, 0.12f), new Color(0.75f, 0.70f, 0.55f));
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

            var mat = MakePickupMaterial(itemId, color);
            go.GetComponent<Renderer>().sharedMaterial = mat;

            var pickup = go.AddComponent<AbyssEcho.Interaction.PickupItem>();
            pickup.itemId = itemId;
            pickup.displayName = display;

            Debug.Log($"[Ch3Altar] 放置拾取物 {name}（{display}）@ {pos}");
            return 1;
        }

        private static Material MakePickupMaterial(string itemId, Color c)
        {
            const string folder = "Assets/_Project/Art/Materials/Ch3";
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
                mat.SetColor("_EmissionColor", c * 0.5f);
            }
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }
    }
}