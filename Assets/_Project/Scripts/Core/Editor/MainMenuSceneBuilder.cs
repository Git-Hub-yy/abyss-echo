using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// 创建主菜单场景（MainMenu.unity）。
    ///
    /// 场景内容极简：
    ///   · 一台相机（纯色背景，不渲染任何 3D）
    ///   · MainMenuUI 组件
    ///
    /// 为什么相机要存在：
    ///   没有相机时 Unity 会在 Game 视图显示 "No cameras rendering"，
    ///   并且部分平台会报错。相机只负责清屏。
    /// </summary>
    public static class MainMenuSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/MainMenu.unity";

        [MenuItem("ABYSS ECHO/S9 创建主菜单场景")]
        public static void Build()
        {
            // 新建空场景
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 相机
            var camGo = new GameObject("Main Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.03f, 0.04f, 0.06f);
            cam.orthographic = true;
            cam.cullingMask = 0;          // 不渲染任何物体，只负责清屏
            cam.fieldOfView = 60f;

            var camData = cam.GetUniversalAdditionalCameraData();
            if (camData != null) camData.renderPostProcessing = false;

            // 音频监听
            var listener = camGo.AddComponent<AudioListener>();

            // UI
            var uiGo = new GameObject("--- MENU ---");
            var menu = uiGo.AddComponent<AbyssEcho.UI.MainMenuUI>();
            menu.gameSceneName = "Level_Chapter1_Graybox";
            menu.gameTitle = "ABYSS ECHO";
            menu.gameSubtitle = "深渊回声";
            menu.versionText = "第一章 · 垂直切片 v0.9";

            // 保存
            EnsureFolder("Assets/_Project/Scenes");
            bool ok = EditorSceneManager.SaveScene(scene, ScenePath);

            AssetDatabase.Refresh();

            Debug.Log($"<b>[MainMenu]</b> 主菜单场景{(ok ? "创建成功" : "创建失败")}\n" +
                      $"  路径：{ScenePath}\n" +
                      $"  相机：{(cam != null ? "已创建（纯色清屏）" : "缺失")}\n" +
                      $"  音频监听：{(listener != null ? "已创建" : "缺失")}");

            if (!ok) EditorApplication.Exit(1);
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