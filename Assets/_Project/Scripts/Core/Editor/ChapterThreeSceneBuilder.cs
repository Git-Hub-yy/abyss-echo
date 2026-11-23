using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S31：生成第三章场景。
    ///
    /// 只用两步：① 清空场景 ② GrayboxBuilder 生成几何。
    /// 灯光 / 道具 / 交互物在后续阶段（S32）单独生成，
    /// 避免一个方法里做太多事导致失败难以定位。
    /// </summary>
    public static class ChapterThreeSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter3_Graybox.unity";

        [MenuItem("ABYSS ECHO/S31 生成第三章场景")]
        public static void Build()
        {
            // 保证章节枚举指向第三章（后续 Builder 读 GetRooms() 时依赖它）
            var prev = AbyssEcho.LevelGen.ChapterLayout.Current;
            AbyssEcho.LevelGen.ChapterLayout.Current =
                AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter3;

            var rooms = AbyssEcho.LevelGen.ChapterLayout.GetRooms(
                AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter3);

            Debug.Log($"[Ch3Scene] 房间数 = {rooms.Count}");

            // 新建空场景
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 环境光（与第二章一致的低亮度深蓝）
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.20f, 0.24f, 0.28f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.02f, 0.04f, 0.06f);
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.035f;

            // 几何（GrayboxBuilder.Build 是实例方法，返回生成内容的 Transform）
            var root = new GameObject("--- LEVEL_CH3 ---");
            var graybox = new AbyssEcho.LevelGen.GrayboxBuilder();
            var builtRoot = graybox.Build(rooms, root.transform);
            int built = builtRoot != null ? builtRoot.childCount : 0;
            Debug.Log($"[Ch3Scene] 几何生成完成：{built} 个房间节点");

            // 玩家（与第一章/第二章完全同源，避免手感不一致）
            CreatePlayer(AbyssEcho.LevelGen.ChapterThreeLayout.PlayerStart);

            // 保存
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            // 加入构建列表（保持 MainMenu, Ch1, Ch2, Ch3 的顺序）
            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>();
            foreach (var n in new[]
            {
                "Assets/_Project/Scenes/MainMenu.unity",
                "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity",
                "Assets/_Project/Scenes/Level_Chapter2_Graybox.unity",
                ScenePath,
            })
            {
                if (File.Exists(n))
                    list.Add(new EditorBuildSettingsScene(n, true));
            }
            EditorBuildSettings.scenes = list.ToArray();

            AssetDatabase.SaveAssets();

            Debug.Log("<b>[Ch3Scene] 第三章场景已生成</b>\n" +
                      $"  房间：{built}\n" +
                      $"  路径：{ScenePath}\n" +
                      $"  构建列表：{list.Count} 个场景");

            // 恢复章节（避免影响其它工具）
            AbyssEcho.LevelGen.ChapterLayout.Current = prev;
        }

        /// <summary>
        /// 创建玩家。与第二章同一套做法：
        ///   实例化 → 校验相机 → 完全解包 → 摆位置。
        /// 解包很关键：不解包的话后续 Builder 改的组件不会写进场景。
        /// </summary>
        private static void CreatePlayer(Vector3 pos)
        {
            const string playerPrefabPath = "Assets/_Project/Prefabs/Player/PF_Player.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(playerPrefabPath);

            if (prefab == null)
            {
                Debug.LogError("[Ch3Scene] 找不到玩家 Prefab：" + playerPrefabPath);
                return;
            }

            var playerGo = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (playerGo == null)
            {
                Debug.LogError("[Ch3Scene] InstantiatePrefab 返回 null");
                return;
            }

            var cam = playerGo.GetComponentInChildren<Camera>(true);
            if (cam == null) Debug.LogError("[Ch3Scene] ⚠ 玩家没有相机！");

            if (PrefabUtility.IsPartOfPrefabInstance(playerGo))
            {
                PrefabUtility.UnpackPrefabInstance(playerGo,
                    PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            }

            var cc = playerGo.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            playerGo.transform.position = pos;
            if (cc != null) cc.enabled = true;

            Debug.Log($"[Ch3Scene] 玩家放到 {pos}，相机={(cam != null ? "有" : "无")}");
        }
    }
}