using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S47：给第 2~5 章的 Player 补 FearSystem。
    ///
    /// ══════════════════ 与 PlayerDeathSystem 的关键区别 ══════════════════
    ///
    ///   FearSystem.Awake() 的单例冲突处理是：
    ///       if (Instance != null && Instance != this) { Destroy(this); return; }
    ///
    ///   注意是 **Destroy(this)**（只销毁这个组件），
    ///   而 PlayerDeathSystem 是 Destroy(gameObject)（会毁掉整个 Player）。
    ///
    ///   所以 FearSystem 即使重复添加也不会毁掉玩家 —— 但仍然必须"先查再添"：
    ///   重复添加会留下一个失效的组件实例，污染场景数据。
    ///
    /// ══════════════════ 只补 FearSystem ══════════════════
    ///
    ///   不补 FearVisuals / Volume / PlayerFootsteps / FlickerLight。
    ///   不改 FearSystem.cs，不改任何现有组件参数。
    ///   第一章完全不动。
    /// </summary>
    public static class FearSystemBuilder
    {
        private const string SceneFmt = "Assets/_Project/Scenes/Level_Chapter{0}_Graybox.unity";

        [MenuItem("ABYSS ECHO/修复 v5.3/补 FearSystem - 第二章")]
        public static void BuildCh2() => BuildFor(2);

        [MenuItem("ABYSS ECHO/修复 v5.3/补 FearSystem - 第三章")]
        public static void BuildCh3() => BuildFor(3);

        [MenuItem("ABYSS ECHO/修复 v5.3/补 FearSystem - 第四章")]
        public static void BuildCh4() => BuildFor(4);

        [MenuItem("ABYSS ECHO/修复 v5.3/补 FearSystem - 第五章")]
        public static void BuildCh5() => BuildFor(5);

        public static void BuildFor(int chapterNum)
        {
            string path = string.Format(SceneFmt, chapterNum);
            if (!File.Exists(path)) { Fail(chapterNum, $"场景不存在：{path}"); return; }

            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Log(chapterNum, $"已打开 {path}");

            var motor = Object.FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
            if (motor == null) { Fail(chapterNum, "找不到 PlayerMotor"); return; }

            var player = motor.gameObject;
            Log(chapterNum, $"Player = {player.name}");

            // ── 防重复：先查 ──
            var existing = player.GetComponent<AbyssEcho.Horror.FearSystem>();

            if (existing != null)
            {
                Log(chapterNum, "已存在 FearSystem，跳过（不重复添加）");
                VerifyLog(chapterNum, player, existing, didAdd: false);
                return;
            }

            // ── 不存在 → 添加 ──
            var fear = player.AddComponent<AbyssEcho.Horror.FearSystem>();
            Log(chapterNum, "已添加 FearSystem（使用脚本默认值）");

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Log(chapterNum, saved ? "场景已保存" : "❌ 保存失败");

            VerifyLog(chapterNum, player, fear, didAdd: true);
        }

        private static void VerifyLog(int ch, GameObject player, AbyssEcho.Horror.FearSystem fear, bool didAdd)
        {
            var all = Object.FindObjectsByType<AbyssEcho.Horror.FearSystem>(FindObjectsSortMode.None);

            Log(ch, "---- 结果 ----");
            Log(ch, $"  FearSystem 总数 = {all.Length}");
            Log(ch, $"  挂载于 = {fear.gameObject.name}");
            Log(ch, $"  active={fear.gameObject.activeInHierarchy} enabled={fear.enabled}");
            Log(ch, $"  currentFear={fear.currentFear}");
            Log(ch, $"  recoveryRate={fear.recoveryRate}  darkRiseRate={fear.darkRiseRate}");
            Log(ch, $"  tense={fear.tenseThreshold} fear={fear.fearThreshold} panic={fear.panicThreshold}");
            Log(ch, $"  input={(fear.input != null ? fear.input.name : "NULL（脚本会自动补）")}");
            Log(ch, $"  flashlight={(fear.flashlight != null ? fear.flashlight.name : "NULL（脚本会自动补）")}");
            Log(ch, $"  本次是否新增 = {didAdd}");

            // 确认没碰别的系统
            Log(ch, $"  （未触碰）FearVisuals={Object.FindObjectsByType<AbyssEcho.Horror.FearVisuals>(FindObjectsSortMode.None).Length} " +
                    $"Volume={Object.FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsSortMode.None).Length} " +
                    $"Footsteps={Object.FindObjectsByType<AbyssEcho.Horror.PlayerFootsteps>(FindObjectsSortMode.None).Length} " +
                    $"Flicker={Object.FindObjectsByType<AbyssEcho.Horror.FlickerLight>(FindObjectsSortMode.None).Length} " +
                    $"Death={Object.FindObjectsByType<AbyssEcho.Horror.PlayerDeathSystem>(FindObjectsSortMode.None).Length}");

            Log(ch, "完成");
        }

        private static void Log(int ch, string msg)
        {
            string line = $"[FearBuilder] Ch{ch} | {msg}";
            Debug.Log(line);
            Write(line);
        }

        private static void Fail(int ch, string msg)
        {
            string line = $"[FearBuilder] Ch{ch} | ❌ {msg}";
            Debug.LogError(line);
            Write(line);
        }

        private static void Write(string line)
        {
            try
            {
                string dir = Path.GetFullPath(Path.Combine(
                    Path.GetDirectoryName(Application.dataPath), "../Reports"));
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "FearSystemBuilder_Result.txt"),
                                   line + "\n", new System.Text.UTF8Encoding(false));
            }
            catch { }
        }
    }
}