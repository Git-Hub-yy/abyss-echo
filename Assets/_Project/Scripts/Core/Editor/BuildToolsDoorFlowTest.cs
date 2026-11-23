using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>构建第一道门端到端测试包。</summary>
    public static class BuildToolsDoorFlowTest
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity";

        public static void Build()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            DisableAll<AbyssEcho.DebugTools.ScreenshotTool>();
            DisableAll<AbyssEcho.DebugTools.LevelScreenshotTool>();
            DisableAll<AbyssEcho.DebugTools.S4VisualCheck>();
            DisableAll<AbyssEcho.DebugTools.S5VisualCheck>();
            DisableAll<AbyssEcho.DebugTools.S7VisualCheck>();
            DisableAll<AbyssEcho.DebugTools.S8VisualCheck>();
            DisableAll<AbyssEcho.DebugTools.MonsterVisualCheck>();
            DisableAll<AbyssEcho.DebugTools.PropVisualCheck>();
            DisableAll<AbyssEcho.DebugTools.AutoTest_S4>();
            DisableAll<AbyssEcho.DebugTools.AutoTest_S4B>();
            DisableAll<AbyssEcho.DebugTools.AutoTest_S5>();
            DisableAll<AbyssEcho.DebugTools.AutoTest_S5Monster>();
            DisableAll<AbyssEcho.DebugTools.AutoTest_S7>();
            DisableAll<AbyssEcho.DebugTools.AutoTest_S8>();
            DisableAll<AbyssEcho.DebugTools.AutoTest_S10>();
            DisableAll<AbyssEcho.DebugTools.AutoTest_S12>();
            DisableAll<AbyssEcho.DebugTools.AutoTest_S13>();
            DisableAll<AbyssEcho.DebugTools.AutoTest_S14>();
            DisableAll<AbyssEcho.DebugTools.AutoTest_S15>();
            DisableAll<AbyssEcho.DebugTools.AutoTest_Death>();
            DisableAll<AbyssEcho.DebugTools.PlayerAudit>();
            DisableAll<AbyssEcho.DebugTools.RuntimeTracer>();
            DisableAll<AbyssEcho.DebugTools.RealWalkTest>();
            DisableAll<AbyssEcho.DebugTools.UIKeyDiag>();
            DisableAll<AbyssEcho.DebugTools.UIDiag>();
            DisableAll<AbyssEcho.DebugTools.DoorLockCheck>();
            DisableAll<AbyssEcho.DebugTools.HorrorDiag>();
            DisableAll<AbyssEcho.DebugTools.DoorwayPassabilityDiag>();
            DisableAll<AbyssEcho.DebugTools.DoorwayMeasure>();
            DisableAll<AbyssEcho.DebugTools.WalkThroughTest>();
            DisableAll<AbyssEcho.DebugTools.ChapterWalkTest>();
            DisableAll<AbyssEcho.DebugTools.AutoTest_DoorFlow>();

            var card = UnityEngine.Object.FindFirstObjectByType<AbyssEcho.UI.ChapterTitleCard>();
            if (card != null) card.enabled = false;

            var help = UnityEngine.Object.FindFirstObjectByType<AbyssEcho.UI.ControlsHelpUI>();
            if (help != null) help.showOnFirstEnter = false;

            var test = UnityEngine.Object.FindFirstObjectByType<AbyssEcho.DebugTools.AutoTest_DoorFlow>();
            if (test == null)
            {
                var go = new GameObject("--- DOORTEST ---");
                test = go.AddComponent<AbyssEcho.DebugTools.AutoTest_DoorFlow>();
            }
            test.runOnStart = true;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            string root = Path.GetDirectoryName(Path.GetFullPath(Application.dataPath));
            string outDir = Path.GetFullPath(Path.Combine(root, "../Builds/DoorFlowTest"));
            Directory.CreateDirectory(outDir);

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = Path.Combine(outDir, "DoorFlowTest.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });

            Debug.Log("[DoorFlowTest] build: " + report.summary.result);
            if (report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }

        private static void DisableAll<T>() where T : Component
        {
            foreach (var t in UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None))
            {
                var field = t.GetType().GetField("runOnStart");
                if (field != null) field.SetValue(t, false);
            }
        }
    }
}