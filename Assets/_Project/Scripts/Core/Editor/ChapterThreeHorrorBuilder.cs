using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S33：把模仿者（MimicStalker）布置到第三章。
    ///
    /// 出现位置按设计文档：
    ///   · F2 中央楼梯廊  —— 第一次（远处一个背影）
    ///   · F3 旧档案室    —— 第二次（更近）
    ///   · F5 起居区      —— 第三次（就在门口）
    ///
    /// 为什么用 3 个独立实例而不是 1 个：
    ///   一个实例在整个 F 区乱跑的话，玩家可能永远遇不到它；
    ///   固定在 3 个关键房间，才能保证"每次看完信息就撞见它"的节奏。
    /// </summary>
    public static class ChapterThreeHorrorBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter3_Graybox.unity";
        private const string ModelPath = "Assets/_Project/Art/Models/Monster/Monster_Base.fbx";
        private const string MatPath = "Assets/_Project/Art/Materials/Monster/M_Monster_Silhouette.mat";
        private const string RootName = "--- HORROR_CH3 ---";

        [MenuItem("ABYSS ECHO/S33 布置第三章模仿者")]
        public static void RunAll()
        {
            var prev = AbyssEcho.LevelGen.ChapterLayout.Current;
            AbyssEcho.LevelGen.ChapterLayout.Current = AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter3;

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid()) { Debug.LogError("[Ch3Horror] 无法打开场景"); return; }

            var old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);

            var root = new GameObject(RootName);
            var rooms = CollectRooms();

            int made = 0;
            made += Spawn(root.transform, rooms, "F2", "Mimic_F2", new Vector3(0f, 0f, 4.5f));
            made += Spawn(root.transform, rooms, "F3", "Mimic_F3", new Vector3(3.0f, 0f, -1.0f));
            made += Spawn(root.transform, rooms, "F5", "Mimic_F5", new Vector3(3.2f, 0f, 0f));

            // 恐怖触发器：进入这些房间时压低环境光（复用第一章验证过的 HorrorTrigger）
            int triggers = 0;
            triggers += MakeTrigger(root.transform, rooms, "F2", "Horror_F2", 0.3f);
            triggers += MakeTrigger(root.transform, rooms, "F5", "Horror_F5", 0.4f);
            triggers += MakeTrigger(root.transform, rooms, "F9", "Horror_F9", 0.5f);

            // 镜沼倒影（S34）：F7 水面上的倒影系统
            int mirror = MakeMirror(root.transform, rooms);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log($"<b>[Ch3Horror]</b> 第三章恐怖内容布置完成：模仿者 {made} 个，触发器 {triggers} 个，倒影 {mirror} 个");
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

        private static int Spawn(Transform parent, Dictionary<string, RoomInfo> rooms,
                                 string roomId, string name, Vector3 offsetFromCenter)
        {
            if (!rooms.TryGetValue(roomId, out var r))
            {
                Debug.LogWarning($"[Ch3Horror] 找不到房间 {roomId}，跳过 {name}");
                return 0;
            }

            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (fbx == null)
            {
                Debug.LogWarning("[Ch3Horror] 找不到怪物模型，跳过");
                return 0;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            if (go == null) go = Object.Instantiate(fbx);
            go.name = name;
            go.transform.SetParent(parent, false);

            // 放在房间中心 + 偏移，强制落在房间范围内
            Vector3 pos = r.Center + offsetFromCenter;
            float mx = r.SizeX * 0.5f - 1.0f;
            float mz = r.SizeZ * 0.5f - 1.0f;
            pos.x = Mathf.Clamp(pos.x, r.Center.x - mx, r.Center.x + mx);
            pos.z = Mathf.Clamp(pos.z, r.Center.z - mz, r.Center.z + mz);
            pos.y = 0f;
            go.transform.position = pos;

            if (PrefabUtility.IsPartOfPrefabInstance(go))
                PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            // 剪影材质
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

            // 碰撞体（射线遮挡检测需要）
            var col = go.GetComponent<Collider>();
            if (col == null)
            {
                var cap = go.AddComponent<CapsuleCollider>();
                cap.center = new Vector3(0f, 0.95f, 0f);
                cap.height = 1.9f;
                cap.radius = 0.35f;
            }

            // 模仿者组件
            var mimic = go.AddComponent<AbyssEcho.Horror.MimicStalker>();
            mimic.sightAngle = 35f;
            mimic.sightRange = 25f;
            mimic.approachSpeed = 1.5f;
            mimic.approachStartDistance = 18f;
            mimic.vanishDistance = 5.5f;
            mimic.attackDistance = 2.2f;
            mimic.enableAttack = true;
            mimic.maxVisibleTime = 2.5f;
            mimic.respawnDelay = 6f;
            mimic.logStateChanges = true;

            Debug.Log($"[Ch3Horror] {name} 放在 {pos}");
            return 1;
        }

        private static int MakeMirror(Transform parent, Dictionary<string, RoomInfo> rooms)
        {
            if (!rooms.TryGetValue("F7", out var r))
            {
                Debug.LogWarning("[Ch3Horror] 找不到 F7 镜沼，跳过倒影");
                return 0;
            }

            var go = new GameObject("Mirror_F7");
            go.transform.SetParent(parent, false);

            // 水面在 F7 中心，比房间中心略低（0.12 米）
            var center = new Vector3(r.Center.x, 0.12f, r.Center.z);
            go.transform.position = center;

            var mirror = go.AddComponent<AbyssEcho.Horror.MirrorReflection>();
            mirror.waterCenter = center;
            mirror.waterSize = new Vector2(13f, 11f);
            mirror.normalDelay = 0.3f;
            mirror.laggedDelay = 1.5f;
            mirror.normalRange = 12f;
            mirror.lookDownAngle = 35f;
            mirror.anomalyWatchTime = 3f;
            mirror.logStageChanges = true;

            Debug.Log($"[Ch3Horror] 镜沼倒影 @ {center}");
            return 1;
        }

        private static int MakeTrigger(Transform parent, Dictionary<string, RoomInfo> rooms,
                                       string roomId, string name, float delay)
        {
            if (!rooms.TryGetValue(roomId, out var r)) return 0;

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            // 触发器覆盖房间中心的一部分（不要铺满，避免玩家一进门就被触发）
            go.transform.position = new Vector3(r.Center.x, 1.0f, r.Center.z);
            go.transform.localScale = new Vector3(r.SizeX * 0.7f, 2.0f, r.SizeZ * 0.7f);

            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;

            var t = go.AddComponent<AbyssEcho.Horror.HorrorTrigger>();
            t.delay = delay;
            t.maxFearToTrigger = 0.85f;
            t.oneShot = true;
            t.playStinger = true;
            t.fearJump = 0.12f;
            t.dimAmbient = true;
            t.dimAmbientTo = 0.16f;
            t.dimDuration = 1.5f;

            Debug.Log($"[Ch3Horror] 触发器 {name} @ {r.Id}");
            return 1;
        }
    }
}