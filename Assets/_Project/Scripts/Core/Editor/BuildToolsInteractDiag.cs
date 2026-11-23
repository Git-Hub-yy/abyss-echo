using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>构建交互链路诊断包。</summary>
    public static class BuildToolsInteractDiag
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity";

        public static void Build()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            string[] types =
            {
                "ScreenshotTool", "LevelScreenshotTool", "S4VisualCheck", "S5VisualCheck",
                "S7VisualCheck", "S8VisualCheck", "MonsterVisualCheck", "PropVisualCheck",
                "AutoTest_S4", "AutoTest_S4B", "AutoTest_S5", "AutoTest_S5Monster",
                "AutoTest_S7", "AutoTest_S8", "AutoTest_S10", "AutoTest_S12",
                "AutoTest_S13", "AutoTest_S14", "AutoTest_S15", "AutoTest_Death",
                "AutoTest_DoorFlow", "PlayerAudit", "RuntimeTracer", "RealWalkTest",
                "UIKeyDiag", "UIDiag", "DoorLockCheck", "HorrorDiag",
                "DoorwayPassabilityDiag", "DoorwayMeasure", "WalkThroughTest",
                "ChapterWalkTest", "InteractDiag", "RayProbeDiag", "InteractFlowDiag", "PlacementCheck", "DoorOpenTest"
            };

            foreach (var t in types)
            {
                var full = "AbyssEcho.DebugTools." + t;
                foreach (var mb in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                {
                    if (mb == null) continue;
                    if (mb.GetType().FullName != full) continue;
                    var f = mb.GetType().GetField("runOnStart");
                    if (f != null) f.SetValue(mb, false);
                }
            }

            var card = UnityEngine.Object.FindFirstObjectByType<AbyssEcho.UI.ChapterTitleCard>();
            if (card != null) card.enabled = false;

            var help = UnityEngine.Object.FindFirstObjectByType<AbyssEcho.UI.ControlsHelpUI>();
            if (help != null) help.showOnFirstEnter = false;

            var diag = UnityEngine.Object.FindFirstObjectByType<AbyssEcho.DebugTools.DoorOpenTest>();
            if (diag == null)
            {
                var go = new GameObject("--- INTERACTDIAG ---");
                diag = go.AddComponent<AbyssEcho.DebugTools.DoorOpenTest>();
            }
            diag.runOnStart = true;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            string root = Path.GetDirectoryName(Path.GetFullPath(Application.dataPath));
            string outDir = Path.GetFullPath(Path.Combine(root, "../Builds/InteractDiag"));
            Directory.CreateDirectory(outDir);

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = Path.Combine(outDir, "InteractDiag.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });

            Debug.Log("[InteractDiag] build: " + report.summary.result);
            if (report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}