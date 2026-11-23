using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// 生成第二章关卡场景（独立场景文件）。
    ///
    /// ⚠️ 为什么需要这个工具（重要教训）：
    ///   在 S10 我把 ChapterLayout 做成了"章节可切换的统一入口"，
    ///   ChapterOneSceneBuilder 也确实改成调用 ChapterLayout.GetRooms()。
    ///   但**构建脚本从来没有把 Current 切到第二章过**——
    ///   所以每次生成的都是第一章几何。
    ///   而 S12/S13 的交互物、怪物、灯光都按第二章坐标放置，
    ///   结果它们漂浮在第一章房间之外的空地上，玩家根本看不到。
    ///
    ///   修复思路：第二章要有**独立场景**，
    ///   ① 切到第二章 → ② 生成几何 → ③ 保存为 Level_Chapter2_Graybox.unity
    ///   之后再跑 S12/S13 的布置器，把内容放进这个场景。
    /// </summary>
    public static class ChapterTwoSceneBuilder
    {
        public const string Chapter2ScenePath = "Assets/_Project/Scenes/Level_Chapter2_Graybox.unity";

        [MenuItem("ABYSS ECHO/S14B 生成第二章场景")]
        public static void Build()
        {
            var prevChapter = AbyssEcho.LevelGen.ChapterLayout.Current;

            // ① 切到第二章
            AbyssEcho.LevelGen.ChapterLayout.Current = AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter2;
            Debug.Log("[Ch2Scene] 切换到第二章");

            var rooms = AbyssEcho.LevelGen.ChapterLayout.GetRooms();
            Debug.Log($"[Ch2Scene] 房间数：{rooms.Count}");

            // ② 布局校验
            var check = AbyssEcho.LevelGen.LayoutValidator.Validate(rooms);
            Debug.Log("[Ch2Scene] " + AbyssEcho.LevelGen.LayoutValidator.Format(check));

            if (!check.Passed)
            {
                Debug.LogError("[Ch2Scene] 布局校验未通过，停止生成");
                AbyssEcho.LevelGen.ChapterLayout.Current = prevChapter;
                EditorApplication.Exit(1);
                return;
            }

            // ③ 材质库
            MaterialLibraryBuilder.Build();

            // ④ 新建场景
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 环境光（第二章更暗）
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.10f, 0.12f, 0.16f);

            var lightGo = new GameObject("Directional Light");
            var dirLight = lightGo.AddComponent<Light>();
            dirLight.type = LightType.Directional;
            dirLight.intensity = 0.20f;
            dirLight.color = new Color(0.45f, 0.60f, 0.85f);
            lightGo.transform.rotation = Quaternion.Euler(52f, -28f, 0f);

            // ⑤ 灰盒几何
            var genGo = new GameObject("--- GRAYBOX ---");
            var gen = genGo.AddComponent<AbyssEcho.LevelGen.GrayboxBuilder>();
            var envRoot = gen.Build(rooms, genGo.transform);
            Debug.Log($"[Ch2Scene] 几何：{(envRoot != null ? envRoot.name : "null")}");

            // ⑥ 玩家
            CreatePlayer(AbyssEcho.LevelGen.ChapterLayout.GetPlayerStart());

            // ⑦ 保存
            EnsureFolder("Assets/_Project/Scenes");
            bool saved = EditorSceneManager.SaveScene(scene, Chapter2ScenePath);
            AssetDatabase.Refresh();

            // ⑧ 加进构建列表（放在第一章后面）
            AddToBuildSettings();

            AbyssEcho.LevelGen.ChapterLayout.Current = prevChapter;

            Debug.Log($"<b>[Ch2Scene] {(saved ? "第二章场景生成成功" : "保存失败")}</b>\n  {Chapter2ScenePath}");
            if (!saved) EditorApplication.Exit(1);
        }

        // ────────────────────────── 玩家 ──────────────────────────

        private static void CreatePlayer(Vector3 pos)
        {
            const string playerPrefabPath = "Assets/_Project/Prefabs/Player/PF_Player.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(playerPrefabPath);

            if (prefab == null)
            {
                Debug.LogError("[Ch2Scene] 找不到玩家 Prefab：" + playerPrefabPath);
                return;
            }

            var playerGo = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (playerGo == null)
            {
                Debug.LogError("[Ch2Scene] InstantiatePrefab 返回 null");
                return;
            }

            // 校验相机存在（S3B 曾因相机缺失导致截图全黑）
            var cam = playerGo.GetComponentInChildren<Camera>(true);
            if (cam == null) Debug.LogError("[Ch2Scene] ⚠ 玩家没有相机！");

            // 解包，确保组件写入场景
            if (PrefabUtility.IsPartOfPrefabInstance(playerGo))
            {
                PrefabUtility.UnpackPrefabInstance(playerGo,
                    PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            }

            var cc = playerGo.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            playerGo.transform.position = pos;
            if (cc != null) cc.enabled = true;

            Debug.Log($"[Ch2Scene] 玩家放到 {pos}，相机={(cam != null ? "有" : "无")}");
        }

        // ────────────────────────── 构建列表 ──────────────────────────

        private static void AddToBuildSettings()
        {
            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(
                EditorBuildSettings.scenes);

            bool exists = false;
            foreach (var s in list)
                if (s.path == Chapter2ScenePath) { exists = true; break; }

            if (!exists)
            {
                list.Add(new EditorBuildSettingsScene(Chapter2ScenePath, true));
                EditorBuildSettings.scenes = list.ToArray();
                Debug.Log("[Ch2Scene] 已加入构建列表");
            }
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