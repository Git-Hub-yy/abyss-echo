using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>一次性工具：复制第三章的场景生成器来生成第五章。</summary>
    public static class ChapterFiveBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter5_Graybox.unity";
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player/PF_Player.prefab";

        [MenuItem("ABYSS ECHO/S39 生成第五章场景")]
        public static void Build()
        {
            var prev = AbyssEcho.LevelGen.ChapterLayout.Current;
            AbyssEcho.LevelGen.ChapterLayout.Current = AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter5;

            var rooms = AbyssEcho.LevelGen.ChapterLayout.GetRooms(
                AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter5);

            Debug.Log($"[Ch5Scene] 房间数 = {rooms.Count}");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // H 区是"上行、接近出口" —— 环境光比 G 区亮一些、偏冷白，
            //    给玩家"终于要出去了"的暗示（但雾更浓，远景依旧看不透）
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.24f, 0.26f, 0.30f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.03f, 0.04f, 0.05f);
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.040f;

            var root = new GameObject("--- LEVEL_CH5 ---");
            var graybox = new AbyssEcho.LevelGen.GrayboxBuilder();
            var builtRoot = graybox.Build(rooms, root.transform);
            int built = builtRoot != null ? builtRoot.childCount : 0;
            Debug.Log($"[Ch5Scene] 几何生成完成：{built} 个房间节点");

            CreatePlayer(AbyssEcho.LevelGen.ChapterFiveLayout.PlayerStart);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            // 构建列表：MainMenu, Ch1..Ch4
            var list = new List<EditorBuildSettingsScene>();
            foreach (var n in new[]
            {
                "Assets/_Project/Scenes/MainMenu.unity",
                "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity",
                "Assets/_Project/Scenes/Level_Chapter2_Graybox.unity",
                "Assets/_Project/Scenes/Level_Chapter3_Graybox.unity",
                "Assets/_Project/Scenes/Level_Chapter4_Graybox.unity",
                ScenePath,
            })
            {
                if (File.Exists(n)) list.Add(new EditorBuildSettingsScene(n, true));
            }
            EditorBuildSettings.scenes = list.ToArray();
            AssetDatabase.SaveAssets();

            Debug.Log($"<b>[Ch5Scene] 第五章场景已生成</b>：{built} 房间，{list.Count} 个场景在构建列表");
            AbyssEcho.LevelGen.ChapterLayout.Current = prev;
        }

        private static void CreatePlayer(Vector3 pos)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (prefab == null) { Debug.LogError("[Ch5Scene] 找不到玩家 Prefab"); return; }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (go == null) { Debug.LogError("[Ch5Scene] 实例化失败"); return; }

            var cam = go.GetComponentInChildren<Camera>(true);
            if (cam == null) Debug.LogError("[Ch5Scene] ⚠ 玩家没有相机");

            if (PrefabUtility.IsPartOfPrefabInstance(go))
                PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            var cc = go.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            go.transform.position = pos;
            if (cc != null) cc.enabled = true;

            Debug.Log($"[Ch5Scene] 玩家放到 {pos}，相机={(cam != null ? "有" : "无")}");
        }
    }
}