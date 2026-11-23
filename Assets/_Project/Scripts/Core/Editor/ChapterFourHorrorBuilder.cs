using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S38：第四章恐怖内容。
    ///
    /// G 区的恐惧与其他章不同：
    ///   第一/二章 = 被追踪
    ///   第三章 = 被观测
    ///   第四章 = **被同化**（这里不是基地，是某种东西的内部）
    ///
    /// 所以这里用：
    ///   · 模仿者（延续第三章，但更多、更近）
    ///   · 密恐型恐怖触发器（走进某处环境光突然压暗）
    ///   · 核心腔的强恐惧场
    /// </summary>
    public static class ChapterFourHorrorBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter4_Graybox.unity";
        private const string ModelPath = "Assets/_Project/Art/Models/Monster/Monster_Base.fbx";
        private const string MatPath = "Assets/_Project/Art/Materials/Monster/M_Monster_Silhouette.mat";
        private const string RootName = "--- HORROR_CH4 ---";

        [MenuItem("ABYSS ECHO/S38 布置第四章恐怖内容")]
        public static void RunAll()
        {
            var prev = AbyssEcho.LevelGen.ChapterLayout.Current;
            AbyssEcho.LevelGen.ChapterLayout.Current = AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter4;

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid()) { Debug.LogError("[Ch4Horror] 无法打开场景"); return; }

            var old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);

            var root = new GameObject(RootName);
            var rooms = CollectRooms();

            int mimics = 0;
            mimics += SpawnMimic(root.transform, rooms, "G3", "Mimic_G3", new Vector3(3.5f, 0f, 0f));
            mimics += SpawnMimic(root.transform, rooms, "G5", "Mimic_G5", new Vector3(4.0f, 0f, 2.0f));
            mimics += SpawnMimic(root.transform, rooms, "G6", "Mimic_G6", new Vector3(-4.0f, 0f, -2.0f));

            int triggers = 0;
            triggers += MakeTrigger(root.transform, rooms, "G2", "Horror_G2", 0.4f);
            triggers += MakeTrigger(root.transform, rooms, "G3", "Horror_G3", 0.6f);
            triggers += MakeTrigger(root.transform, rooms, "G7", "Horror_G7", 0.8f);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log($"<b>[Ch4Horror]</b> 第四章恐怖内容：模仿者 {mimics} 个，触发器 {triggers} 个");
            AbyssEcho.LevelGen.ChapterLayout.Current = prev;
        }

        private class RoomInfo
        {
            public string Id;
            public Vector3 Center;
            public float SizeX, SizeZ, Height;
        }

        private static Dictionary<string, RoomInfo> CollectRooms()
        {
            var d = new Dictionary<string, RoomInfo>();
            foreach (var r in AbyssEcho.LevelGen.ChapterLayout.GetRooms(
                         AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter4))
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

        private static int SpawnMimic(Transform parent, Dictionary<string, RoomInfo> rooms,
                                      string roomId, string name, Vector3 offset)
        {
            if (!rooms.TryGetValue(roomId, out var r)) return 0;

            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (fbx == null) { Debug.LogWarning("[Ch4Horror] 找不到怪物模型"); return 0; }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            if (go == null) go = Object.Instantiate(fbx);
            go.name = name;
            go.transform.SetParent(parent, false);

            Vector3 pos = r.Center + offset;
            float mx = r.SizeX * 0.5f - 1.2f;
            float mz = r.SizeZ * 0.5f - 1.2f;
            pos.x = Mathf.Clamp(pos.x, r.Center.x - mx, r.Center.x + mx);
            pos.z = Mathf.Clamp(pos.z, r.Center.z - mz, r.Center.z + mz);
            pos.y = 0f;
            go.transform.position = pos;

            if (PrefabUtility.IsPartOfPrefabInstance(go))
                PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            if (mat != null)
            {
                foreach (var rend in go.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = new Material[rend.sharedMaterials.Length];
                    for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                    rend.sharedMaterials = mats;
                }
            }

            var col = go.GetComponent<Collider>();
            if (col == null)
            {
                var cap = go.AddComponent<CapsuleCollider>();
                cap.center = new Vector3(0f, 0.95f, 0f);
                cap.height = 1.9f;
                cap.radius = 0.35f;
            }

            var mimic = go.AddComponent<AbyssEcho.Horror.MimicStalker>();
            // G 区空间更紧凑：靠近速度略快、消失距离更近，压迫感更强
            mimic.sightAngle = 38f;
            mimic.sightRange = 24f;
            mimic.approachSpeed = 1.7f;
            mimic.approachStartDistance = 17f;
            mimic.vanishDistance = 5.0f;
            mimic.attackDistance = 2.0f;
            mimic.enableAttack = true;
            mimic.maxVisibleTime = 2.0f;
            mimic.respawnDelay = 5f;
            mimic.logStateChanges = true;

            Debug.Log($"[Ch4Horror] {name} 放在 {pos}");
            return 1;
        }

        private static int MakeTrigger(Transform parent, Dictionary<string, RoomInfo> rooms,
                                       string roomId, string name, float delay)
        {
            if (!rooms.TryGetValue(roomId, out var r)) return 0;

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(r.Center.x, 1.0f, r.Center.z);
            go.transform.localScale = new Vector3(r.SizeX * 0.65f, 2.0f, r.SizeZ * 0.65f);

            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;

            var t = go.AddComponent<AbyssEcho.Horror.HorrorTrigger>();
            t.delay = delay;
            t.maxFearToTrigger = 0.9f;
            t.oneShot = true;
            t.playStinger = true;
            t.fearJump = 0.18f;
            t.dimAmbient = true;
            t.dimAmbientTo = 0.12f;
            t.dimDuration = 1.8f;

            return 1;
        }
    }
}