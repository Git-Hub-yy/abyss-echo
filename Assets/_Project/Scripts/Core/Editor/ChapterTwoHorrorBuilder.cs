using System.Collections.Generic;
using AbyssEcho.Horror;
using AbyssEcho.Monster;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S13 第二章恐怖与怪物装配。
    ///
    /// 内容：
    ///   · 深水触发区（D2 大厅）
    ///   · 怪物巡逻点（D 区主通道 + E 区）
    ///   · 恐怖触发器（4 个）
    ///   · 怪物激活触发器
    ///
    /// 复用原则：
    ///   怪物 AI、恐怖触发器、音频系统全部复用第一章的实现，
    ///   只换位置与参数——不重新发明。
    /// </summary>
    public static class ChapterTwoHorrorBuilder
    {
        private const string RootName = "--- CH2_HORROR ---";

        [MenuItem("ABYSS ECHO/S13 布置第二章恐怖系统")]
        public static void Build()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("[Ch2Horror] 请先打开场景");
                return;
            }

            var old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);

            var root = new GameObject(RootName);
            var rooms = CollectRooms();

            int water = CreateWaterZone(root.transform, rooms);
            int monster = CreateMonster(root.transform, rooms);
            int triggers = CreateHorrorTriggers(root.transform, rooms);
            int activate = CreateActivationTrigger(root.transform, rooms);

            Debug.Log($"[Ch2Horror] 布置完成\n" +
                      $"  · 深水区：{water}\n" +
                      $"  · 怪物：{monster}\n" +
                      $"  · 恐怖触发：{triggers}");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        // ────────────────────────── 深水区 ──────────────────────────

        private static int CreateWaterZone(Transform parent, Dictionary<string, RoomInfo> rooms)
        {
            if (!rooms.TryGetValue("D2", out var d2)) return 0;

            var go = new GameObject("WaterZone_D2");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(d2.Center.x, 0.5f, d2.Center.z);

            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;
            // 比房间略小，避免玩家站在门口时误判
            col.size = new Vector3(d2.SizeX - 2.0f, 1.2f, d2.SizeZ - 2.0f);

            var zone = go.AddComponent<DeepWaterZone>();
            zone.waterDepth = 0.5f;
            zone.speedMultiplier = 0.65f;
            zone.noiseMultiplier = 2.2f;
            zone.playSplashAudio = true;
            zone.logEnterExit = false;

            EditorUtility.SetDirty(zone);
            return 1;
        }

        // ────────────────────────── 怪物 ──────────────────────────

        private static int CreateMonster(Transform parent, Dictionary<string, RoomInfo> rooms)
        {
            // 复用第一章的怪物模型
            const string ModelPath = "Assets/_Project/Art/Models/Monster/Monster_Base.fbx";
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (fbx == null)
            {
                Debug.LogWarning("[Ch2Horror] 找不到怪物模型，跳过");
                return 0;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            if (go == null) go = Object.Instantiate(fbx);
            go.name = "Monster_Ch2";
            go.transform.SetParent(parent, false);

            // 初始位置：D2 大厅北侧
            Vector3 spawn = rooms.TryGetValue("D2", out var d2)
                ? new Vector3(d2.Center.x, 0f, d2.Center.z + 4f)
                : new Vector3(0f, 0f, 11f);
            go.transform.position = spawn;

            if (PrefabUtility.IsPartOfPrefabInstance(go))
                PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            // 剪影材质（复用第一章的材质）
            var mat = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Project/Art/Materials/Monster/M_Monster_Silhouette.mat");
            if (mat != null)
            {
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = new Material[r.sharedMaterials.Length];
                    for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                    r.sharedMaterials = mats;
                }
            }

            var agent = go.GetComponent<NavMeshAgent>();
            if (agent == null) agent = go.AddComponent<NavMeshAgent>();
            agent.radius = 0.4f;
            agent.height = 1.9f;
            agent.speed = 1.15f;
            agent.angularSpeed = 240f;
            agent.acceleration = 12f;
            agent.stoppingDistance = 0.6f;

            var animator = go.GetComponent<Animator>();
            if (animator == null) animator = go.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var col = go.GetComponent<Collider>();
            if (col == null)
            {
                var capsule = go.AddComponent<CapsuleCollider>();
                capsule.center = new Vector3(0f, 0.95f, 0f);
                capsule.height = 1.9f;
                capsule.radius = 0.35f;
            }

            var ai = go.AddComponent<MonsterAI>();
            ai.animator = animator;
            ai.sightRange = 14f;      // 第二章视野略短（更暗）
            ai.sightAngle = 100f;
            ai.patrolSpeed = 1.1f;
            ai.chaseSpeed = 3.2f;
            ai.hearingRangeWalk = 10f;
            ai.hearingRangeRun = 20f;  // 第二章走廊更大，听觉更远

            // 巡逻点：沿 D 区主通道
            var holder = new GameObject("PatrolPoints_Ch2");
            holder.transform.SetParent(parent, false);

            var pts = new List<Transform>();
            var coords = new[]
            {
                ("P1_D2南", new Vector3(0f, 0f, 6f)),
                ("P2_D2中", new Vector3(0f, 0f, 11f)),
                ("P3_D2北", new Vector3(0f, 0f, 16f)),
                ("P4_D5", new Vector3(0f, 0f, 24f)),
                ("P5_E1", new Vector3(0f, 0f, 35f)),
            };

            foreach (var (name, pos) in coords)
            {
                var p = new GameObject(name);
                p.transform.SetParent(holder.transform, false);
                p.transform.position = pos;
                pts.Add(p.transform);
            }

            ai.patrolPoints = pts.ToArray();

            EditorUtility.SetDirty(go);
            EditorUtility.SetDirty(ai);
            return 1;
        }

        // ────────────────────────── 恐怖触发器 ──────────────────────────

        private static int CreateHorrorTriggers(Transform parent, Dictionary<string, RoomInfo> rooms)
        {
            int n = 0;

            // ① D2 大厅入口：第一声"回声"
            if (rooms.TryGetValue("D2", out var d2))
            {
                CreateTrigger(parent, "Horror_D2_Echo", 
                    new Vector3(d2.Center.x, 1f, d2.Center.z - d2.SizeZ * 0.4f),
                    new Vector3(6f, 3f, 2f), 0.22f, true, 0);
                n++;
            }

            // ② D4 档案室：翻档案时的动静
            if (rooms.TryGetValue("D4", out var d4))
            {
                CreateTrigger(parent, "Horror_D4_Whisper",
                    new Vector3(d4.Center.x, 1f, d4.Center.z),
                    new Vector3(5f, 3f, 4f), 0.18f, false, 3);
                n++;
            }

            // ③ D5 生活区：看到生活痕迹（恐惧 + 不安）
            if (rooms.TryGetValue("D5", out var d5))
            {
                CreateTrigger(parent, "Horror_D5_Traces",
                    new Vector3(d5.Center.x, 1f, d5.Center.z),
                    new Vector3(8f, 3f, 5f), 0.15f, false, 0);
                n++;
            }

            // ④ E1 实验室：高潮惊吓
            if (rooms.TryGetValue("E1", out var e1))
            {
                CreateTrigger(parent, "Horror_E1_Climax",
                    new Vector3(e1.Center.x, 1f, e1.Center.z),
                    new Vector3(10f, 4f, 6f), 0.45f, true, 6);
                n++;
            }

            return n;
        }

        private static void CreateTrigger(Transform parent, string name, Vector3 pos, Vector3 size,
                                          float fearJump, bool playStinger, int footstepCount)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;

            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = size;

            var t = go.AddComponent<HorrorTrigger>();
            t.fearJump = fearJump;
            t.playStinger = playStinger;
            t.footstepCount = footstepCount;
            t.oneShot = true;
            t.delay = 0.5f;

            EditorUtility.SetDirty(t);
        }

        // ────────────────────────── 激活触发器 ──────────────────────────

        private static int CreateActivationTrigger(Transform parent, Dictionary<string, RoomInfo> rooms)
        {
            if (!rooms.TryGetValue("D2", out var d2)) return 0;

            var go = new GameObject("Ch2_MonsterActivate");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(d2.Center.x, 1f, d2.Center.z - 3f);

            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(8f, 3f, 2f);

            var trig = go.AddComponent<MonsterActivationTrigger>();
            var monster = Object.FindFirstObjectByType<MonsterAI>();

            // 只绑定本章的怪物
            var ch2Monster = GameObject.Find("Monster_Ch2");
            if (ch2Monster != null)
                trig.monster = ch2Monster.GetComponent<MonsterAI>();
            else if (monster != null)
                trig.monster = monster;

            trig.oneShot = true;

            EditorUtility.SetDirty(trig);
            return 1;
        }

        // ────────────────────────── 房间数据 ──────────────────────────

        private struct RoomInfo
        {
            public string Id;
            public Vector3 Center;
            public float SizeX;
            public float SizeZ;
            public float MaxZ => Center.z + SizeZ * 0.5f;
        }

        private static Dictionary<string, RoomInfo> CollectRooms()
        {
            var result = new Dictionary<string, RoomInfo>();
            foreach (var r in AbyssEcho.LevelGen.ChapterTwoLayout.Build())
            {
                result[r.Id] = new RoomInfo
                {
                    Id = r.Id,
                    Center = new Vector3(r.Center.x, 0f, r.Center.z),
                    SizeX = r.SizeX,
                    SizeZ = r.SizeZ
                };
            }
            return result;
        }
    }
}