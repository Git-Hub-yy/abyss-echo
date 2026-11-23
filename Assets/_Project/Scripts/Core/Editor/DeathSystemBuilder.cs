using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// v5.1 第一批修复：给第 2~5 章的 Player 补 PlayerDeathSystem。
    ///
    /// ══════════════════ 本工具只做一件事 ══════════════════
    ///
    ///   在指定章节场景的 Player 上确保存在一个 PlayerDeathSystem。
    ///
    ///   不改 FearSystem / FearVisuals / Volume / PlayerFootsteps / FlickerLight，
    ///   不改任何现有组件参数，不改第一章，不改怪物 AI。
    ///
    /// ══════════════════ 为什么必须"先查再添" ══════════════════
    ///
    ///   PlayerDeathSystem.Awake() 里有：
    ///       if (Instance != null && Instance != this) { Destroy(gameObject); return; }
    ///
    ///   注意是 Destroy(gameObject) —— 销毁的是**整个 GameObject**。
    ///   如果同一场景里出现两个 PlayerDeathSystem，
    ///   第二个会把自己的宿主 GameObject（也就是 Player）整个删掉。
    ///   所以 AddComponent 之前必须先 GetComponent 检查。
    ///
    /// ══════════════════ 为什么要每章独立进程 ══════════════════
    ///
    ///   本项目已踩过的坑：同一进程里反复 EditorSceneManager.OpenScene
    ///   会导致场景数据串味（跑第二章报第一章的东西）。
    ///   所以每个 BuildChN 都是独立的 executeMethod，由外部逐次调用。
    /// </summary>
    public static class DeathSystemBuilder
    {
        private const string SceneFmt = "Assets/_Project/Scenes/Level_Chapter{0}_Graybox.unity";

        [MenuItem("ABYSS ECHO/修复 v5.1/补 DeathSystem - 第二章")]
        public static void BuildCh2() => BuildFor(2);

        [MenuItem("ABYSS ECHO/修复 v5.1/补 DeathSystem - 第三章")]
        public static void BuildCh3() => BuildFor(3);

        [MenuItem("ABYSS ECHO/修复 v5.1/补 DeathSystem - 第四章")]
        public static void BuildCh4() => BuildFor(4);

        [MenuItem("ABYSS ECHO/修复 v5.1/补 DeathSystem - 第五章")]
        public static void BuildCh5() => BuildFor(5);

        /// <summary>给单章补组件。绝不允许一章内调用多次。</summary>
        public static void BuildFor(int chapterNum)
        {
            string path = string.Format(SceneFmt, chapterNum);

            if (!File.Exists(path))
            {
                Fail(chapterNum, $"场景文件不存在：{path}");
                return;
            }

            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Log(chapterNum, $"已打开 {path}");

            // ── 1. 找 Player ──
            //   PlayerDeathSystem 挂在 Player 根节点上，而 PlayerMotor 就在根节点，
            //   所以用 PlayerMotor 反查最可靠（不依赖名字）。
            var motor = Object.FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
            if (motor == null)
            {
                Fail(chapterNum, "场景中找不到 PlayerMotor，无法定位 Player");
                return;
            }

            var player = motor.gameObject;
            Log(chapterNum, $"Player = {player.name}  activeSelf={player.activeSelf}");

            // ── 2. 防重复：先查 ──
            var existing = player.GetComponent<AbyssEcho.Horror.PlayerDeathSystem>();

            if (existing != null)
            {
                // 已存在 → 什么都不做（绝不重复 AddComponent）
                Log(chapterNum, "已存在 PlayerDeathSystem，跳过（不重复添加）");
                VerifyOnly(chapterNum, scene, player, existing, didAdd: false);
                return;
            }

            // ── 3. 不存在 → 添加 ──
            var death = player.AddComponent<AbyssEcho.Horror.PlayerDeathSystem>();
            Log(chapterNum, "已添加 PlayerDeathSystem（使用脚本默认值，不修改任何字段）");

            // ── 4. 保存 ──
            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Log(chapterNum, saved ? "场景已保存" : "❌ 场景保存失败");

            VerifyOnly(chapterNum, scene, player, death, didAdd: true);
        }

        /// <summary>添加/确认之后就地把结果打出来，便于事后核对。</summary>
        private static void VerifyOnly(int chapterNum, UnityEngine.SceneManagement.Scene scene,
                                       GameObject player, AbyssEcho.Horror.PlayerDeathSystem death, bool didAdd)
        {
            // 重新从场景里统计（确保落盘的确实是 1 个）
            var all = Object.FindObjectsByType<AbyssEcho.Horror.PlayerDeathSystem>(FindObjectsSortMode.None);

            Log(chapterNum, $"---- 结果 ----");
            Log(chapterNum, $"  PlayerDeathSystem 总数 = {all.Length}");
            Log(chapterNum, $"  挂载于 = {death.gameObject.name}");
            Log(chapterNum, $"  active={death.gameObject.activeInHierarchy} enabled={death.enabled}");
            Log(chapterNum, $"  fadeDuration={death.fadeDuration}");
            Log(chapterNum, $"  deathTitle='{death.deathTitle}'");
            Log(chapterNum, $"  deathSubtitle='{death.deathSubtitle}'");
            Log(chapterNum, $"  restartHint='{death.restartHint}'");
            Log(chapterNum, $"  titleFontSize={death.titleFontSize} textFontSize={death.textFontSize}");
            Log(chapterNum, $"  本次是否新增 = {didAdd}");

            // 顺便确认没有碰到别的系统
            Log(chapterNum, $"  （未触碰）FearSystem={Object.FindObjectsByType<AbyssEcho.Horror.FearSystem>(FindObjectsSortMode.None).Length} " +
                            $"FearVisuals={Object.FindObjectsByType<AbyssEcho.Horror.FearVisuals>(FindObjectsSortMode.None).Length} " +
                            $"Volume={Object.FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsSortMode.None).Length} " +
                            $"Footsteps={Object.FindObjectsByType<AbyssEcho.Horror.PlayerFootsteps>(FindObjectsSortMode.None).Length} " +
                            $"Flicker={Object.FindObjectsByType<AbyssEcho.Horror.FlickerLight>(FindObjectsSortMode.None).Length}");

            Log(chapterNum, "完成");
        }

        private static void Log(int ch, string msg)
        {
            string line = $"[DeathBuilder] Ch{ch} | {msg}";
            Debug.Log(line);
            AppendResult(line);
        }

        private static void Fail(int ch, string msg)
        {
            string line = $"[DeathBuilder] Ch{ch} | ❌ 失败：{msg}";
            Debug.LogError(line);
            AppendResult(line);
        }

        /// <summary>把结果追加写入 Reports/DeathSystemBuilder_Result.txt，方便查看。</summary>
        private static void AppendResult(string line)
        {
            try
            {
                string dir = Path.GetFullPath(Path.Combine(
                    Path.GetDirectoryName(Application.dataPath), "../Reports"));
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "DeathSystemBuilder_Result.txt"),
                                   line + "\n", new System.Text.UTF8Encoding(false));
            }
            catch { }
        }
    }
}