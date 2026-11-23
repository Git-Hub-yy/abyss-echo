using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S46：把第 3~5 章所有 MimicStalker 的 vanishDistance 调整为 1.0。
    ///
    /// ══════════════════ 为什么要改 ══════════════════
    ///
    ///   上一批（S45）修好了"攻击判定不可达"，但留下一个玩法问题：
    ///
    ///     vanishDistance(5.5) > attackDistance(2.2)
    ///
    ///   怪物从远处走近时，还没到 2.2 米的攻击距离，
    ///   就先撞上 5.5 米的消失阈值 → Vanish() 换位置。
    ///   结果：**怪物永远不会主动追上来杀死玩家**，
    ///        只有玩家自己走到 2.2 米以内才会被抓住。
    ///
    ///   把 vanishDistance 降到 1.0 之后：
    ///     1.0 < 2.2，攻击判定会先触发，怪物真的会追上来。
    ///     距离进入 1.0 米以内才消失（此时早已被攻击）。
    ///
    /// ══════════════════ 只改这一个参数 ══════════════════
    ///
    ///   attackDistance 保持不变（Ch3: 2.2 / Ch4: 2.0 / Ch5: 2.0）
    ///   approachSpeed / respawnDelay / sightAngle / maxVisibleTime 等全部不动
    ///   脚本 MimicStalker.cs 本批**不需要改动**（S45 的判定顺序已经是攻击优先）
    ///
    /// ══════════════════ 每章独立进程 ══════════════════
    ///
    ///   沿用项目惯例：同一进程反复 OpenScene 会导致场景数据串味。
    /// </summary>
    public static class MimicChaseBuilder
    {
        private const string SceneFmt = "Assets/_Project/Scenes/Level_Chapter{0}_Graybox.unity";
        private const float TargetVanishDistance = 1.0f;

        [MenuItem("ABYSS ECHO/修复 v5.2/Mimic 追击 - 第三章")]
        public static void BuildCh3() => BuildFor(3);

        [MenuItem("ABYSS ECHO/修复 v5.2/Mimic 追击 - 第四章")]
        public static void BuildCh4() => BuildFor(4);

        [MenuItem("ABYSS ECHO/修复 v5.2/Mimic 追击 - 第五章")]
        public static void BuildCh5() => BuildFor(5);

        public static void BuildFor(int chapterNum)
        {
            string path = string.Format(SceneFmt, chapterNum);
            if (!File.Exists(path)) { Fail(chapterNum, $"场景不存在：{path}"); return; }

            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Log(chapterNum, $"已打开 {path}");

            var mimics = Object.FindObjectsByType<AbyssEcho.Horror.MimicStalker>(FindObjectsSortMode.None);
            if (mimics.Length == 0) { Fail(chapterNum, "场景中没有 MimicStalker"); return; }

            Log(chapterNum, $"找到 {mimics.Length} 个 MimicStalker");

            int changed = 0;

            foreach (var m in mimics)
            {
                float oldVanish = m.vanishDistance;
                float oldAttack = m.attackDistance;

                if (Mathf.Approximately(oldVanish, TargetVanishDistance))
                {
                    Log(chapterNum, $"  {m.gameObject.name}: vanishDistance 已是 {TargetVanishDistance}，跳过");
                    continue;
                }

                m.vanishDistance = TargetVanishDistance;
                changed++;

                Log(chapterNum, $"  {m.gameObject.name}: vanishDistance {oldVanish:F1} → {TargetVanishDistance:F1}  " +
                                $"(attackDistance 保持 {oldAttack:F1})");

                // 打上脏标记，确保能保存
                EditorUtility.SetDirty(m);
            }

            if (changed == 0)
            {
                Log(chapterNum, "本次没有任何改动（可能已经改过）");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Log(chapterNum, saved ? $"场景已保存（改动 {changed} 个）" : "❌ 保存失败");

            VerifyLog(chapterNum, mimics);
        }

        private static void VerifyLog(int ch, AbyssEcho.Horror.MimicStalker[] mimics)
        {
            Log(ch, "---- 结果 ----");
            int ok = 0;
            foreach (var m in mimics)
            {
                bool attackFirst = m.attackDistance > m.vanishDistance;
                if (attackFirst) ok++;
                Log(ch, $"  {m.gameObject.name}: vanish={m.vanishDistance:F1} attack={m.attackDistance:F1} " +
                        $"enableAttack={m.enableAttack} → 攻击先于消失={attackFirst}");
            }
            Log(ch, $"  满足 attackDistance > vanishDistance 的数量 = {ok} / {mimics.Length}");
            Log(ch, "完成");
        }

        private static void Log(int ch, string msg)
        {
            string line = $"[MimicChase] Ch{ch} | {msg}";
            Debug.Log(line);
            Write(line);
        }

        private static void Fail(int ch, string msg)
        {
            string line = $"[MimicChase] Ch{ch} | ❌ {msg}";
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
                File.AppendAllText(Path.Combine(dir, "MimicChaseBuilder_Result.txt"),
                                   line + "\n", new System.Text.UTF8Encoding(false));
            }
            catch { }
        }
    }
}