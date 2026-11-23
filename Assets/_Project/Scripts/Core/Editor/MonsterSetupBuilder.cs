using System.Collections.Generic;
using AbyssEcho.Monster;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S5 第二部分装配：怪物实例 + 材质 + 导航网格 + 巡逻点。
    ///
    /// 怪物形象：Animated Human（CC0）做成"黑色剪影"——
    ///   恐怖游戏里看不清的轮廓比写实怪物更有效，
    ///   同时避免低模角色在近景暴露细节不足的问题。
    /// </summary>
    public static class MonsterSetupBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity";
        private const string ModelPath = "Assets/_Project/Art/Models/Monster/Monster_Base.fbx";
        private const string RootName = "--- MONSTER ---";
        private const string MatFolder = "Assets/_Project/Art/Materials/Monster";

        [MenuItem("ABYSS ECHO/S5 布置怪物")]
        public static void Setup()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("[MonsterSetup] 请先打开 Level_Chapter1_Graybox 场景");
                return;
            }

            var old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);

            var root = new GameObject(RootName);

            // ① 导航网格（怪物用 NavMeshAgent 移动，必须先烘焙）
            // 导航网格改由 NavMeshBaker 用 NavMeshSurface 烘焙（可持久化到场景）
            bool navBaked = GameObject.Find("--- NAVMESH ---") != null;

            // ② 怪物实例
            var monster = CreateMonster(root.transform, navBaked);

            // ③ 巡逻点（沿 B 区宿舍走廊 + C 区主厅）
            var patrols = CreatePatrolPoints(root.transform);
            if (monster != null && patrols.Count > 0)
            {
                var ai = monster.GetComponent<AbyssEcho.Monster.MonsterAI>();
                if (ai != null) ai.patrolPoints = patrols.ToArray();
            }

            // ④ 激活触发器（玩家进入 B 区后怪物开始巡逻）
            CreateActivationTrigger(root.transform, monster);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            Debug.Log("<b>[MonsterSetup] 怪物已布置</b>\n" +
                      $"  · 怪物实例：{(monster != null ? "已创建" : "失败")}\n" +
                      $"  · 导航网格：{(navBaked ? "已烘焙" : "未烘焙 ⚠")}\n" +
                      $"  · 巡逻点：{patrols.Count} 个");
        }

        // ────────────────────────── 导航网格 ──────────────────────────

        /// <summary>
        /// 烘焙 NavMesh。
        ///
        /// 注意：NavMeshBuilder（运行时烘焙 API）在批处理模式下可用，
        /// 但只能收集场景中已启用的 Renderer/BoxCollider。
        /// 关卡几何是 Cube + BoxCollider，可以直接作为可行走表面。
        /// </summary>
        private static bool BakeNavMesh()
        {
            try
            {
                var settings = new NavMeshBuildSettings
                {
                    agentTypeID = 0,
                    agentRadius = 0.4f,
                    agentHeight = 1.9f,
                    agentSlope = 45f,
                    agentClimb = 0.4f,
                    minRegionArea = 1f
                };

                // 收集场景几何
                var sources = new List<NavMeshBuildSource>();
                var bounds = new Bounds(Vector3.zero, Vector3.one * 200f);

                var markers = new List<NavMeshBuildMarkup>();
                NavMeshBuilder.CollectSources(
                    bounds,
                    ~0,                          // 全部层
                    NavMeshCollectGeometry.PhysicsColliders,
                    0,                           // 默认 area
                    markers,
                    sources);

                if (sources.Count == 0)
                {
                    Debug.LogWarning("[MonsterSetup] 未收集到可行走表面，导航网格未能烘焙");
                    return false;
                }

                var data = NavMeshBuilder.BuildNavMeshData(
                    settings, sources, bounds, Vector3.zero, Quaternion.identity);

                if (data == null) return false;

                const string navPath = "Assets/_Project/Scenes/NavMesh_Chapter1.asset";
                var existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(navPath);
                if (existing != null) AssetDatabase.DeleteAsset(navPath);
                AssetDatabase.CreateAsset(data, navPath);
                AssetDatabase.SaveAssets();

                // 挂到场景（NavMeshDataInstance）
                NavMesh.AddNavMeshData(data);

                Debug.Log($"[MonsterSetup] 导航网格已烘焙：{sources.Count} 个表面 → {navPath}");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[MonsterSetup] 导航网格烘焙失败：" + e.Message);
                return false;
            }
        }

        // ────────────────────────── 怪物实例 ──────────────────────────

        private static GameObject CreateMonster(Transform parent, bool navOk)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (fbx == null)
            {
                Debug.LogError("[MonsterSetup] 找不到模型：" + ModelPath);
                return null;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            if (go == null) go = Object.Instantiate(fbx);

            go.name = "Monster";
            go.transform.SetParent(parent, false);

            // 位置：B2 宿舍走廊西端（远离玩家起点，但玩家到 B 区后会遇到）
            go.transform.position = new Vector3(-13f, 0f, 10.9f);
            go.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

            // 解包，确保组件写入场景
            if (PrefabUtility.IsPartOfPrefabInstance(go))
                PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            // 材质：黑色剪影
            ApplySilhouetteMaterial(go);

            // 导航代理
            var agent = go.GetComponent<NavMeshAgent>();
            if (agent == null) agent = go.AddComponent<NavMeshAgent>();
            agent.radius = 0.4f;
            agent.height = 1.9f;
            agent.baseOffset = 0f;
            agent.speed = 1.15f;
            agent.angularSpeed = 240f;
            agent.acceleration = 12f;
            agent.stoppingDistance = 0.6f;
            agent.autoBraking = true;

            // 动画器
            var animator = go.GetComponent<Animator>();
            if (animator == null) animator = go.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // 碰撞体（用于视线遮挡与抓取判定）
            var col = go.GetComponent<Collider>();
            if (col == null)
            {
                var capsule = go.AddComponent<CapsuleCollider>();
                capsule.center = new Vector3(0f, 0.95f, 0f);
                capsule.height = 1.9f;
                capsule.radius = 0.35f;
            }

            // AI
            var ai = go.AddComponent<AbyssEcho.Monster.MonsterAI>();
            ai.animator = animator;
            ai.sightRange = 16f;
            ai.sightAngle = 110f;
            ai.patrolSpeed = 1.15f;
            ai.chaseSpeed = 3.4f;

            EditorUtility.SetDirty(go);
            EditorUtility.SetDirty(ai);
            return go;
        }

        /// <summary>把模型材质替换为深色剪影材质。</summary>
        private static void ApplySilhouetteMaterial(GameObject go)
        {
            EnsureFolder(MatFolder);
            string path = MatFolder + "/M_Monster_Silhouette.mat";

            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                mat = new Material(shader) { name = "M_Monster_Silhouette" };
                // 极深的颜色，几乎纯黑但有微弱反射，避免完全看不出体积
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.02f, 0.02f, 0.025f));
                mat.color = new Color(0.02f, 0.02f, 0.025f);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.15f);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.35f);

                // 微弱自发光，保证在纯黑环境里能看到轮廓
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", new Color(0.03f, 0.035f, 0.05f));
                }

                AssetDatabase.CreateAsset(mat, path);
            }

            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = true;
            }
        }

        // ────────────────────────── 巡逻点 ──────────────────────────

        private static List<Transform> CreatePatrolPoints(Transform parent)
        {
            var list = new List<Transform>();
            var holder = new GameObject("PatrolPoints");
            holder.transform.SetParent(parent, false);

            // 沿 B2 宿舍走廊 + 餐厅
            var points = new (string name, Vector3 pos)[]
            {
                ("P1_B2西",   new Vector3(-13f, 0f, 10.9f)),
                ("P2_B2中",   new Vector3(-2f, 0f, 10.9f)),
                ("P3_B2东",   new Vector3(9f, 0f, 10.9f)),
                ("P4_餐厅",   new Vector3(0f, 0f, 3f)),
                ("P5_B1通道", new Vector3(0f, 0f, 7f)),
            };

            foreach (var (name, pos) in points)
            {
                var go = new GameObject(name);
                go.transform.SetParent(holder.transform, false);
                go.transform.position = pos;
                list.Add(go.transform);
            }

            return list;
        }

        // ────────────────────────── 激活触发器 ──────────────────────────

        private static void CreateActivationTrigger(Transform parent, GameObject monster)
        {
            if (monster == null) return;

            var go = new GameObject("MonsterActivate_B1");
            go.transform.SetParent(parent, false);
            // 放在 B1 餐厅入口：玩家进入 B 区后怪物开始活动
            go.transform.position = new Vector3(0f, 1f, 6f);

            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(10f, 3f, 2f);

            var trigger = go.AddComponent<MonsterActivationTrigger>();
            trigger.monster = monster.GetComponent<AbyssEcho.Monster.MonsterAI>();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int idx = path.LastIndexOf('/');
            string parent = path.Substring(0, idx);
            string leaf = path.Substring(idx + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }

}
