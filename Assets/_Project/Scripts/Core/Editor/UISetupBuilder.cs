using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S7 UI 装配：物品栏、暂停菜单、自动存档。
    ///
    /// 与其它 Builder 一致：幂等、代码布置、场景重建后自动恢复。
    /// </summary>
    public static class UISetupBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity";

        [MenuItem("ABYSS ECHO/S7 装配 UI 与存档")]
        public static void Setup()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("[UISetup] 请先打开 Level_Chapter1_Graybox 场景");
                return;
            }

            var debugRoot = GameObject.Find("--- DEBUG ---");
            if (debugRoot == null) debugRoot = new GameObject("--- DEBUG ---");

            // ① 物品栏 UI
            var inv = debugRoot.GetComponent<AbyssEcho.UI.InventoryUI>();
            if (inv == null) inv = debugRoot.AddComponent<AbyssEcho.UI.InventoryUI>();

            // ② 暂停菜单
            var pause = debugRoot.GetComponent<AbyssEcho.UI.PauseMenuUI>();
            if (pause == null) pause = debugRoot.AddComponent<AbyssEcho.UI.PauseMenuUI>();

            // ③ 自动存档（挂在 DEBUG 下即可，无场景依赖）
            var autosave = debugRoot.GetComponent<AbyssEcho.Save.AutoSaveManager>();
            if (autosave == null) autosave = debugRoot.AddComponent<AbyssEcho.Save.AutoSaveManager>();
            autosave.enableAutoSave = true;
            autosave.showSaveToast = true;

            // ④ 第一章结局
            var ending = debugRoot.GetComponent<AbyssEcho.Narrative.ChapterEndingSystem>();
            if (ending == null) ending = debugRoot.AddComponent<AbyssEcho.Narrative.ChapterEndingSystem>();
            // 触发区中心取玩家站立时的身高中点（约 y=0.9），半高 2m 覆盖整个可站立范围
            ending.triggerCenter = AbyssEcho.LevelGen.ChapterOneLayout.ChapterEndPoint + Vector3.up * 0.9f;
            ending.triggerSize = new Vector3(5f, 4f, 4f);

            // ⑤ 开场章节标题卡
            var titleCard = debugRoot.GetComponent<AbyssEcho.UI.ChapterTitleCard>();
            if (titleCard == null) titleCard = debugRoot.AddComponent<AbyssEcho.UI.ChapterTitleCard>();
            titleCard.onlyFirstTime = false;   // 开发阶段每次都播，便于验证


            // ⑥ 运行时追踪（排查用，可随时关闭）
            var tracer = debugRoot.GetComponent<AbyssEcho.DebugTools.RuntimeTracer>();
            if (tracer == null) tracer = debugRoot.AddComponent<AbyssEcho.DebugTools.RuntimeTracer>();
            tracer.enableTracing = true;

            // ⑦ 操作说明面板（首次进入自动显示，按 H 可随时查看）
            var controls = debugRoot.GetComponent<AbyssEcho.UI.ControlsHelpUI>();
            if (controls == null) controls = debugRoot.AddComponent<AbyssEcho.UI.ControlsHelpUI>();
            controls.showOnFirstEnter = true;
            controls.autoShowDuration = 9f;

            EditorUtility.SetDirty(debugRoot);
            EditorUtility.SetDirty(controls);
            EditorUtility.SetDirty(tracer);
            EditorUtility.SetDirty(inv);
            EditorUtility.SetDirty(pause);
            EditorUtility.SetDirty(autosave);
            EditorUtility.SetDirty(ending);
            EditorUtility.SetDirty(titleCard);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);   // 保存到当前场景，不写死路径
            Debug.Log("[UISetup] 已保存到 " + scene.path + "");
            AssetDatabase.SaveAssets();

            Debug.Log("<b>[UISetup] UI 与存档系统已装配</b>\n" +
                      "  · InventoryUI → --- DEBUG ---（Tab 打开）\n" +
                      "  · PauseMenuUI → --- DEBUG ---（Esc 打开）\n" +
                      "  · AutoSaveManager → --- DEBUG ---\n" +
                      "  · ChapterEndingSystem → --- DEBUG ---（终点触发）\n" +
                      "  · ChapterTitleCard → --- DEBUG ---（开场标题）");
        }
    }
}
