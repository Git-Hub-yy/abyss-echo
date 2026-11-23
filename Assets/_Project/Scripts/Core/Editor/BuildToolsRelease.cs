using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S9 发布打包：生成可交付的 Windows 玩家版本。
    ///
    /// ⚠️⚠️ 关键教训（v0.9 发布版的致命 bug）⚠️⚠️
    ///   之前的版本**没有禁用调试脚本**，导致发布版里：
    ///     · AutoTest_S8 会把玩家瞬移到终点 → 一进游戏就触发结局
    ///     · AutoTest_Stage2 会把玩家传送到走廊各处
    ///     · 各种截图工具会到处传送相机
    ///   玩家看到的现象是"没怎么动就到大结局"。
    ///
    ///   本版本强制禁用场景中**所有**带 runOnStart 字段的组件，
    ///   并做了三重保险：
    ///     ① 逐个类型禁用（明确列表）
    ///     ② 反射兜底：扫描场景里所有 MonoBehaviour，凡有 runOnStart 的一律置 false
    ///     ③ 打包后验证：输出仍处于启用状态的调试脚本数量，非 0 则视为失败
    /// </summary>
    public static class BuildToolsRelease
    {
        private const string MainMenuScene = "Assets/_Project/Scenes/MainMenu.unity";
        private const string GameScene = "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity";
        private const string GameScene2 = "Assets/_Project/Scenes/Level_Chapter2_Graybox.unity";
        private const string GameScene3 = "Assets/_Project/Scenes/Level_Chapter3_Graybox.unity";
        private const string GameScene4 = "Assets/_Project/Scenes/Level_Chapter4_Graybox.unity";
        private const string GameScene5 = "Assets/_Project/Scenes/Level_Chapter5_Graybox.unity";
        private const string OutputDir = "../Builds/Release/v5.0";

        [MenuItem("ABYSS ECHO/S9 打包发布版")]
        public static void Build()
        {
            var sw = Stopwatch.StartNew();

            // ── 前置检查 ──
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuScene) == null)
            {
                Debug.LogError("[Release] 找不到主菜单场景，请先执行「S9 创建主菜单场景」");
                EditorApplication.Exit(1);
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScene) == null)
            {
                Debug.LogError("[Release] 找不到关卡场景");
                EditorApplication.Exit(1);
                return;
            }

            // ── 打开关卡场景，禁用所有调试脚本 ──
            var gameScene = EditorSceneManager.OpenScene(GameScene, OpenSceneMode.Single);
            int disabled = DisableAllDebugScripts();

            // 保存（必须保存，否则打包用的还是旧的 runOnStart=true）
            EditorSceneManager.MarkSceneDirty(gameScene);
            bool saved = EditorSceneManager.SaveScene(gameScene, GameScene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Release] 调试脚本已禁用 {disabled} 个，场景保存={(saved ? "成功" : "失败")}");

            // ── 验证：重新打开确认已持久化 ──
            // ⚠️ S27 事故修复（玩家实测"第二章水位下降后就莫名退出游戏"）：
            //
            //   原来这里**只验证了第一章场景**（GameScene），
            //   第二章（GameScene2）里的 AutoTest_S26 从来没被检查过。
            //   它带着 runOnStart=True 进了正式版，进第二章就自动跑测试
            //   并调用 Application.Quit() —— 玩家看到的就是"游戏自己退出"。
            //
            //   现在两个场景都验证，任何一个有问题都终止打包。
            foreach (var scenePath in new[] { GameScene, GameScene2, GameScene3, GameScene4, GameScene5 })
            {
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

                // ① runOnStart 为 true 的调试脚本
                int stillEnabled = CountEnabledDebugScripts();
                if (stillEnabled > 0)
                {
                    Debug.LogError($"[Release] ❌ {Path.GetFileName(scenePath)} 仍有 {stillEnabled} 个" +
                                   "调试脚本处于启用状态，终止打包！");
                    EditorApplication.Exit(1);
                    return;
                }

                // ② 任何 DebugTools 组件残留（它们会调用 Application.Quit）
                int risky = CountQuitRiskyComponents();
                if (risky > 0)
                {
                    Debug.LogError($"[Release] ❌ {Path.GetFileName(scenePath)} 有 {risky} 个调试组件残留" +
                                   "（会调用 Application.Quit 导致游戏自动退出），终止打包！\n" +
                                   "请先运行 ABYSS ECHO/工具：清理场景中的测试组件");
                    EditorApplication.Exit(1);
                    return;
                }

                Debug.Log($"[Release] ✅ {Path.GetFileName(scenePath)} 验证通过：无调试脚本残留");
            }

            // ── 保证构建列表正确 ──
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MainMenuScene, true),
                new EditorBuildSettingsScene(GameScene, true),
                new EditorBuildSettingsScene(GameScene2, true),
                new EditorBuildSettingsScene(GameScene3, true),
                new EditorBuildSettingsScene(GameScene4, true),
                new EditorBuildSettingsScene(GameScene5, true)
            };

            // ── 输出目录 ──
            string projectRoot = Path.GetDirectoryName(Path.GetFullPath(Application.dataPath));
            string outDir = Path.GetFullPath(Path.Combine(projectRoot, OutputDir));
            Directory.CreateDirectory(outDir);

            string exeName = "ABYSS ECHO.exe";
            string exePath = Path.Combine(outDir, exeName);

            // ── 构建 ──
            var options = new BuildPlayerOptions
            {
                scenes = new[] { MainMenuScene, GameScene, GameScene2, GameScene3, GameScene4, GameScene5 },
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None        // 正式版：不带 Development
            };

            Debug.Log("[Release] 开始打包（正式版）…");
            var report = BuildPipeline.BuildPlayer(options);
            sw.Stop();

            var summary = report.summary;
            string msg = $"结果：{summary.result}\n" +
                         $"耗时：{sw.Elapsed.TotalSeconds:F1} 秒\n" +
                         $"大小：{summary.totalSize / (1024f * 1024f):F1} MB\n" +
                         $"错误：{summary.totalErrors}\n" +
                         $"输出：{exePath}\n" +
                         $"已禁用调试脚本：{disabled} 个";

            Debug.Log("<b>[Release] 打包完成</b>\n" + msg);

            try
            {
                string reportDir = Path.GetFullPath(Path.Combine(projectRoot, "../Reports"));
                Directory.CreateDirectory(reportDir);
                File.WriteAllText(Path.Combine(reportDir, "S9_ReleaseBuild.txt"),
                    "ABYSS ECHO · 发布版打包\n" +
                    "=====================================\n" +
                    msg + "\n\n" +
                    "场景顺序：\n" +
                    $"  0. {MainMenuScene}\n" +
                    $"  1. {GameScene}\n" +
                    $"  2. {GameScene2}\n" +
                $"  3. {GameScene3}\n" +
                $"  4. {GameScene4}\n" +
                $"  5. {GameScene5}\n\n" +
                    "安全检查：\n" +
                    "  · 已禁用场景内全部调试脚本（runOnStart=false 且已保存）\n" +
                    "  · 打包前二次验证通过\n" +
                    "=====================================\n" +
                    $"时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}\n",
                    new System.Text.UTF8Encoding(false));
            }
            catch { }

            if (summary.result != BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }

        // ────────────────────────── 调试脚本禁用 ──────────────────────────

        /// <summary>已知的调试脚本类型（显式列表，便于追溯）。</summary>
        private static readonly Type[] KnownDebugTypes = BuildDebugTypeList();

        private static Type[] BuildDebugTypeList()
        {
            var list = new List<Type>();
            void Add(string fullName)
            {
                var t = Type.GetType(fullName + ", Assembly-CSharp");
                if (t != null) list.Add(t);
            }

            Add("AbyssEcho.DebugTools.ScreenshotTool");
            Add("AbyssEcho.DebugTools.ChapterWalkTest");
            Add("AbyssEcho.DebugTools.LightingVerifier");
            Add("AbyssEcho.DebugTools.BlackScreenDiagnoser");
            Add("AbyssEcho.DebugTools.LevelScreenshotTool");
            Add("AbyssEcho.DebugTools.S4VisualCheck");
            Add("AbyssEcho.DebugTools.S5VisualCheck");
            Add("AbyssEcho.DebugTools.S7VisualCheck");
            Add("AbyssEcho.DebugTools.S8VisualCheck");
            Add("AbyssEcho.DebugTools.MonsterVisualCheck");
            Add("AbyssEcho.DebugTools.PropVisualCheck");
            Add("AbyssEcho.DebugTools.AutoTest_Stage2");
            Add("AbyssEcho.DebugTools.AutoTest_S4");
            Add("AbyssEcho.DebugTools.AutoTest_S4B");
            Add("AbyssEcho.DebugTools.AutoTest_S5");
            Add("AbyssEcho.DebugTools.AutoTest_S5Monster");
            Add("AbyssEcho.DebugTools.AutoTest_S7");
            Add("AbyssEcho.DebugTools.AutoTest_S8");
            Add("AbyssEcho.DebugTools.AutoTest_Death");
            Add("AbyssEcho.DebugTools.EndingTriggerDiagnostic");
            Add("AbyssEcho.DebugTools.EndingReproTest");
            Add("AbyssEcho.DebugTools.EndingKeyRepro");
            Add("AbyssEcho.DebugTools.DeepEndingDiag");
            Add("AbyssEcho.DebugTools.DoorBlockTest");
            return list.ToArray();
        }

        /// <summary>禁用所有调试脚本（显式列表 + 反射兜底）。</summary>
        private static int DisableAllDebugScripts()
        {
            int count = 0;

            // ① 显式列表
            foreach (var t in KnownDebugTypes)
            {
                var comps = UnityEngine.Object.FindObjectsByType(t, FindObjectsSortMode.None);
                foreach (var obj in comps)
                {
                    if (obj is Component comp && SetRunOnStart(comp, false)) { count++; EditorUtility.SetDirty(comp); }
                }
            }

            // ② 反射兜底：扫描场景里所有 MonoBehaviour
            var all = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            foreach (var mb in all)
            {
                if (mb == null) continue;
                if (SetRunOnStart(mb, false)) { count++; EditorUtility.SetDirty(mb); }
            }

            // ③ 关闭调试 HUD（FPS/位置/体力那个黑框，正式版不该显示）
            //    注意：PlayerDebugHUD 用的是 showHud 字段而不是 runOnStart，
            //    所以上面两轮都抓不到它。
            var huds = UnityEngine.Object.FindObjectsByType<AbyssEcho.DebugTools.PlayerDebugHUD>(FindObjectsSortMode.None);
            foreach (var hud in huds)
            {
                if (hud != null && hud.showHud)
                {
                    hud.showHud = false;
                    EditorUtility.SetDirty(hud);
                    count++;
                    Debug.Log("[Release] 已关闭调试 HUD: " + hud.gameObject.name);
                }
            }

            return count;
        }

        /// <summary>统计仍处于启用状态的调试脚本数量。</summary>
        private static int CountEnabledDebugScripts()
        {
            int count = 0;
            var all = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            foreach (var mb in all)
            {
                if (mb == null) continue;
                var f = mb.GetType().GetField("runOnStart");
                if (f == null || f.FieldType != typeof(bool)) continue;
                if (f.GetValue(mb) is bool b && b) count++;
            }
            return count;
        }

        /// <summary>
        /// S27：检查场景里是否还挂着任何"会自己退出游戏"的组件。
        ///
        /// 判定方式：组件的类型名以 AutoTest_ / Diag / Check / Test 等结尾，
        /// 且所在命名空间是 AbyssEcho.DebugTools —— 这类脚本几乎都会在结束时调用 Application.Quit()。
        /// 只要它们在场景里存在且 enabled，就有风险，直接判定为失败。
        /// </summary>
        private static int CountQuitRiskyComponents()
        {
            int count = 0;
            var all = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            foreach (var mb in all)
            {
                if (mb == null || !mb.enabled) continue;
                var t = mb.GetType();
                if (t.Namespace == null || !t.Namespace.Contains("DebugTools")) continue;

                // PlayerDebugHUD 只画 HUD，不退出，允许存在（打包时会关掉 showHud）
                if (t.Name == "PlayerDebugHUD") continue;

                Debug.LogError($"[Release] ❌ 场景里仍有调试组件：{t.Name} @ {mb.gameObject.name} " +
                               "（这类脚本会调用 Application.Quit）");
                count++;
            }
            return count;
        }

        /// <summary>把组件的 runOnStart 设为指定值；返回是否发生了改变。</summary>
        private static bool SetRunOnStart(Component c, bool value)
        {
            if (c == null) return false;
            var f = c.GetType().GetField("runOnStart");
            if (f == null || f.FieldType != typeof(bool)) return false;

            bool cur = f.GetValue(c) is bool b && b;
            if (cur == value) return false;

            f.SetValue(c, value);
            return true;
        }
    }
}
