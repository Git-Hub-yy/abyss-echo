using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S55：给五章场景的 Player 挂上 PeaceModeManager。
    ///
    /// 为什么挂在 Player 上：
    ///   · Player 是每章场景都存在的对象
    ///   · 生命周期与场景一致，切章后自动重建，不会残留旧实例
    ///   · 不需要改任何场景结构
    /// </summary>
    public static class PeaceModeBuilder
    {
        private const string SceneFmt = "Assets/_Project/Scenes/Level_Chapter{0}_Graybox.unity";

        [MenuItem("ABYSS ECHO/修复 v6.1/和平模式 - 第一章")]
        public static void BuildCh1() => BuildFor(1);

        [MenuItem("ABYSS ECHO/修复 v6.1/和平模式 - 第二章")]
        public static void BuildCh2() => BuildFor(2);

        [MenuItem("ABYSS ECHO/修复 v6.1/和平模式 - 第三章")]
        public static void BuildCh3() => BuildFor(3);

        [MenuItem("ABYSS ECHO/修复 v6.1/和平模式 - 第四章")]
        public static void BuildCh4() => BuildFor(4);

        [MenuItem("ABYSS ECHO/修复 v6.1/和平模式 - 第五章")]
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

            // 防重复
            var existing = player.GetComponent<AbyssEcho.DebugTools.PeaceModeManager>();
            if (existing != null)
            {
                Log(chapterNum, "PeaceModeManager 已存在，跳过");
            }
            else
            {
                var pm = player.AddComponent<AbyssEcho.DebugTools.PeaceModeManager>();
                pm.toggleKey = KeyCode.M;
                pm.startEnabled = false;
                pm.toastDuration = 2f;
                EditorUtility.SetDirty(pm);

                EditorSceneManager.MarkSceneDirty(scene);
                bool ok = EditorSceneManager.SaveScene(scene, path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Log(chapterNum, ok ? "+ 已添加 PeaceModeManager @ Player，场景已保存" : "❌ 保存失败");
            }

            // 验证
            var check = Object.FindFirstObjectByType<AbyssEcho.DebugTools.PeaceModeManager>();
            Log(chapterNum, $"---- 验证 ----");
            Log(chapterNum, $"  PeaceModeManager = {(check != null ? "@" + check.gameObject.name : "❌")}");
            if (check != null)
            {
                Log(chapterNum, $"  按键 = {check.toggleKey}  默认开启 = {check.startEnabled}  提示时长 = {check.toastDuration}s");
            }

            // 统计怪物
            var mimics = Object.FindObjectsByType<AbyssEcho.Horror.MimicStalker>(FindObjectsSortMode.None);
            var mons = Object.FindObjectsByType<AbyssEcho.Monster.MonsterAI>(FindObjectsSortMode.None);
            Log(chapterNum, $"  MimicStalker={mimics.Length}  MonsterAI={mons.Length}（均受和平模式保护）");

            Log(chapterNum, "完成");
        }

        private static void Log(int ch, string msg)
        {
            string line = $"[Peace] Ch{ch} | {msg}";
            Debug.Log(line);
            Write(line);
        }

        private static void Fail(int ch, string msg)
        {
            string line = $"[Peace] Ch{ch} | ❌ {msg}";
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
                File.AppendAllText(Path.Combine(dir, "PeaceMode_Result.txt"),
                                   line + "\n", new System.Text.UTF8Encoding(false));
            }
            catch { }
        }
    }
}