using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S44：构建单章走查测试包（不污染正式场景）。
    ///
    /// ══════════════════════ 两次失败后的最终方案 ══════════════════════
    ///
    ///  失败 1：一个进程里打开 5 个场景 → 数据串味
    ///          （跑第二章却报第一章的墙体名）
    ///
    ///  失败 2：改成每章独立进程，但脚本直接写进**正式场景**并保存
    ///          → 正式场景被污染，必须再写清理工具
    ///
    ///  本版：
    ///    ① 把正式场景**复制**到临时目录（Assets/_WalkTemp/）
    ///    ② 只往副本里挂走查脚本
    ///    ③ 用副本构建
    ///    ④ 构建完成后删除临时目录
    ///
    ///  这样正式场景永远干净。
    /// </summary>
    public static class BuildToolsMimicAttack
    {
        private const string TempFolder = "Assets/_WalkTemp";

        [MenuItem("ABYSS ECHO/修复 v5.1/Mimic攻击测试（第一章）")]
        public static void BuildCh1() => BuildFor(1);

        [MenuItem("ABYSS ECHO/修复 v5.1/Mimic攻击测试（第二章）")]
        public static void BuildCh2() => BuildFor(2);

        [MenuItem("ABYSS ECHO/修复 v5.1/Mimic攻击测试（第三章）")]
        public static void BuildCh3() => BuildFor(3);

        [MenuItem("ABYSS ECHO/修复 v5.1/Mimic攻击测试（第四章）")]
        public static void BuildCh4() => BuildFor(4);

        [MenuItem("ABYSS ECHO/修复 v5.1/Mimic攻击测试（第五章）")]
        public static void BuildCh5() => BuildFor(5);

        private static void BuildFor(int chapterNum)
        {
            string srcScene = $"Assets/_Project/Scenes/Level_Chapter{chapterNum}_Graybox.unity";
            if (!File.Exists(srcScene))
            {
                Debug.LogError($"[Walkthrough] 找不到场景 {srcScene}");
                EditorApplication.Exit(1);
                return;
            }

            // 清理旧的临时目录
            if (AssetDatabase.IsValidFolder(TempFolder))
                AssetDatabase.DeleteAsset(TempFolder);

            AssetDatabase.CreateFolder("Assets", "_WalkTemp");

            string tempScene = $"{TempFolder}/Walk_Ch{chapterNum}.unity";
            if (!AssetDatabase.CopyAsset(srcScene, tempScene))
            {
                Debug.LogError($"[Walkthrough] 复制场景失败 {srcScene}");
                EditorApplication.Exit(1);
                return;
            }

            AssetDatabase.Refresh();

            // 在副本里挂走查脚本
            var scene = EditorSceneManager.OpenScene(tempScene, OpenSceneMode.Single);

            var old = GameObject.Find("--- MIMICATTACK ---");
            if (old != null) Object.DestroyImmediate(old);

            var go = new GameObject("--- MIMICATTACK ---");
            var probe = go.AddComponent<AbyssEcho.DebugTools.MimicAttackTest>();
            probe.runOnStart = true;
            probe.forceChapter = chapterNum;   // ★ 强制章节，不依赖 PlayerPrefs
               // ★ 强制章节，不依赖 PlayerPrefs

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            // 构建到独立目录
            // ⚠️ UnityProject/Assets 的上一级是 UnityProject，
            //    而我们要输出到 ABYSS_ECHO/Builds，所以再往上一级。
            string projectRoot = Path.GetDirectoryName(Path.GetFullPath(Application.dataPath));
            string repoRoot = Path.GetDirectoryName(projectRoot);
            string outDir = Path.GetFullPath(Path.Combine(repoRoot, "Builds", $"MimicAttack_Ch{chapterNum}"));
            Directory.CreateDirectory(outDir);

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { tempScene },
                locationPathName = Path.Combine(outDir, $"MimicAttackCh{chapterNum}.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });

            Debug.Log($"[Walkthrough] Ch{chapterNum} build: {report.summary.result}");
            bool ok = report.summary.result == BuildResult.Succeeded;

            // 清理临时资源（无论成败）
            try
            {
                AssetDatabase.DeleteAsset(TempFolder);
                AssetDatabase.Refresh();
            }
            catch { }

            if (!ok) EditorApplication.Exit(1);
        }
    }
}