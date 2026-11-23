using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S4 交互系统装配：把检测器、背包、准星 UI 挂到玩家身上。
    ///
    /// 为什么单独一个 Builder：
    ///   场景每次重建都会丢失手动添加的组件，
    ///   用代码装配可以保证"重建场景后交互系统依然完整"。
    ///
    /// 幂等：已存在的组件不会重复添加。
    /// </summary>
    public static class InteractionSetupBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity";

        [MenuItem("ABYSS ECHO/S4 装配交互系统")]
        public static void Setup()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("[InteractionSetup] 请先打开 Level_Chapter1_Graybox 场景");
                return;
            }

            // ⚠️ S18 修复：不再按名字找玩家。
            //    玩家预制体的根对象名是 "CameraPivot"，第二章直接实例化预制体，
            //    所以 GameObject.Find("Player") 返回 null，导致整个装配静默失败。
            //    改为按组件查找，并统一重命名为 "Player"。
            var motor = Object.FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
            if (motor == null)
            {
                Debug.LogError("[InteractionSetup] 场景中找不到 PlayerMotor");
                return;
            }

            var player = motor.gameObject;
            if (player.name != "Player")
            {
                Debug.Log($"[InteractionSetup] 玩家对象名为 \"{player.name}\"，重命名为 \"Player\"");
                player.name = "Player";
            }

            // ① 相机上挂交互检测器
            var cam = player.GetComponentInChildren<Camera>(true);
            if (cam == null) { Debug.LogError("[InteractionSetup] 玩家没有相机"); return; }

            var detector = cam.GetComponent<AbyssEcho.Interaction.InteractionDetector>();
            if (detector == null) detector = cam.gameObject.AddComponent<AbyssEcho.Interaction.InteractionDetector>();

            // ② 玩家根节点挂背包
            var inv = player.GetComponent<AbyssEcho.Inventory.PlayerInventory>();
            if (inv == null) inv = player.AddComponent<AbyssEcho.Inventory.PlayerInventory>();

            // ③ 准星 UI（挂在 DEBUG 对象下，若没有则新建）
            var debugRoot = GameObject.Find("--- DEBUG ---");
            if (debugRoot == null) debugRoot = new GameObject("--- DEBUG ---");

            var crosshair = debugRoot.GetComponent<AbyssEcho.UI.CrosshairUI>();
            if (crosshair == null) crosshair = debugRoot.AddComponent<AbyssEcho.UI.CrosshairUI>();
            crosshair.detector = detector;

            // ④ 叙事 UI（阅读线索 / 日志）
            var narrativeUI = debugRoot.GetComponent<AbyssEcho.UI.NarrativeUI>();
            if (narrativeUI == null) narrativeUI = debugRoot.AddComponent<AbyssEcho.UI.NarrativeUI>();

            // ⑤ 门禁键盘 UI
            var keypadUI = debugRoot.GetComponent<AbyssEcho.UI.KeypadUI>();
            if (keypadUI == null) keypadUI = debugRoot.AddComponent<AbyssEcho.UI.KeypadUI>();

            // ⑥ 日志系统（单例，挂在 DEBUG 下即可）
            var journal = debugRoot.GetComponent<AbyssEcho.Narrative.JournalSystem>();
            if (journal == null) journal = debugRoot.AddComponent<AbyssEcho.Narrative.JournalSystem>();

            EditorUtility.SetDirty(cam.gameObject);
            EditorUtility.SetDirty(player);
            EditorUtility.SetDirty(debugRoot);
            EditorUtility.SetDirty(detector);
            EditorUtility.SetDirty(inv);
            EditorUtility.SetDirty(crosshair);
            EditorUtility.SetDirty(narrativeUI);
            EditorUtility.SetDirty(keypadUI);
            EditorUtility.SetDirty(journal);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);   // 保存到当前场景，避免跨章污染
            AssetDatabase.SaveAssets();

            Debug.Log("<b>[InteractionSetup] 交互系统已装配</b>\n" +
                      "  · InteractionDetector → Main Camera\n" +
                      "  · PlayerInventory → Player\n" +
                      "  · CrosshairUI → --- DEBUG ---\n" +
                      "  · NarrativeUI → --- DEBUG ---\n" +
                      "  · KeypadUI → --- DEBUG ---\n" +
                      "  · JournalSystem → --- DEBUG ---");
        }
    }
}