using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S49：给第 2~5 章的 Player 补 PlayerFootsteps。
    ///
    /// ══════════════════ PlayerFootsteps 的关键特性 ══════════════════
    ///
    ///   1. **没有自己的 AudioSource**
    ///      它调用 AudioManager.Instance.PlayFootstep(pos, metal) → PlayAt()
    ///      → 从 AudioManager 的 _spatialPool 取一个空闲 AudioSource。
    ///      所以不需要在 Player 上配 AudioSource。
    ///
    ///   2. **只有一个 Inspector 引用：motor**
    ///      Awake() 里会自动 GetComponent<PlayerMotor>() 兜底。
    ///      所以即使留空也能工作，但还是显式赋值更清晰。
    ///
    ///   3. **其余全是数值参数**，用脚本默认值与第一章一致：
    ///      walkInterval=0.52  runInterval=0.34  crouchInterval=0.85
    ///      onMetalSurface=true  autoDetectSurface=false
    ///
    /// ══════════════════ 只补 PlayerFootsteps ══════════════════
    ///
    ///   不补 FlickerLight，不改任何脚本，第一章完全不动。
    /// </summary>
    public static class FootstepsBuilder
    {
        private const string SceneFmt = "Assets/_Project/Scenes/Level_Chapter{0}_Graybox.unity";

        [MenuItem("ABYSS ECHO/修复 v5.5/补 PlayerFootsteps - 第二章")]
        public static void BuildCh2() => BuildFor(2);

        [MenuItem("ABYSS ECHO/修复 v5.5/补 PlayerFootsteps - 第三章")]
        public static void BuildCh3() => BuildFor(3);

        [MenuItem("ABYSS ECHO/修复 v5.5/补 PlayerFootsteps - 第四章")]
        public static void BuildCh4() => BuildFor(4);

        [MenuItem("ABYSS ECHO/修复 v5.5/补 PlayerFootsteps - 第五章")]
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
            var existing = player.GetComponent<AbyssEcho.Horror.PlayerFootsteps>();

            if (existing != null)
            {
                Log(chapterNum, "已存在 PlayerFootsteps，检查引用");
                bool fixedRef = false;
                if (existing.motor != motor) { existing.motor = motor; EditorUtility.SetDirty(existing); fixedRef = true; }
                if (fixedRef) Log(chapterNum, "  已修正 motor 引用");

                if (fixedRef)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene, path);
                    AssetDatabase.SaveAssets();
                }
                else Log(chapterNum, "  引用正确，无需改动");

                VerifyLog(chapterNum, existing, didAdd: false);
                return;
            }

            // ── 添加 ──
            var fs = player.AddComponent<AbyssEcho.Horror.PlayerFootsteps>();
            fs.motor = motor;   // 唯一引用
            // 其余参数保持脚本默认值（与第一章一致）

            Log(chapterNum, "已添加 PlayerFootsteps（参数用脚本默认值，motor 指向 Player）");

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Log(chapterNum, saved ? "场景已保存" : "❌ 保存失败");

            VerifyLog(chapterNum, fs, didAdd: true);
        }

        private static void VerifyLog(int ch, AbyssEcho.Horror.PlayerFootsteps fs, bool didAdd)
        {
            var all = Object.FindObjectsByType<AbyssEcho.Horror.PlayerFootsteps>(FindObjectsSortMode.None);
            var am = Object.FindFirstObjectByType<AbyssEcho.Audio.AudioManager>();

            Log(ch, "---- 结果 ----");
            Log(ch, $"  PlayerFootsteps 总数 = {all.Length}");
            Log(ch, $"  挂载于 = {fs.gameObject.name}");
            Log(ch, $"  active={fs.gameObject.activeInHierarchy} enabled={fs.enabled}");
            Log(ch, $"  motor = {(fs.motor != null ? fs.motor.gameObject.name : "NULL")}");
            Log(ch, $"  walkInterval={fs.walkInterval} runInterval={fs.runInterval} crouchInterval={fs.crouchInterval}");
            Log(ch, $"  onMetalSurface={fs.onMetalSurface} autoDetectSurface={fs.autoDetectSurface}");
            Log(ch, $"  AudioManager = {(am != null ? am.gameObject.name : "❌ 无")}（脚步通过它播放，组件本身不需要 AudioSource）");
            Log(ch, $"  本次是否新增 = {didAdd}");

            Log(ch, $"  （未触碰）FlickerLight={Object.FindObjectsByType<AbyssEcho.Horror.FlickerLight>(FindObjectsSortMode.None).Length} " +
                    $"FearSystem={Object.FindObjectsByType<AbyssEcho.Horror.FearSystem>(FindObjectsSortMode.None).Length} " +
                    $"FearVisuals={Object.FindObjectsByType<AbyssEcho.Horror.FearVisuals>(FindObjectsSortMode.None).Length} " +
                    $"Volume={Object.FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsSortMode.None).Length} " +
                    $"Death={Object.FindObjectsByType<AbyssEcho.Horror.PlayerDeathSystem>(FindObjectsSortMode.None).Length}");

            Log(ch, "完成");
        }

        private static void Log(int ch, string msg)
        {
            string line = $"[StepBuilder] Ch{ch} | {msg}";
            Debug.Log(line);
            Write(line);
        }

        private static void Fail(int ch, string msg)
        {
            string line = $"[StepBuilder] Ch{ch} | ❌ {msg}";
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
                File.AppendAllText(Path.Combine(dir, "FootstepsBuilder_Result.txt"),
                                   line + "\n", new System.Text.UTF8Encoding(false));
            }
            catch { }
        }
    }
}