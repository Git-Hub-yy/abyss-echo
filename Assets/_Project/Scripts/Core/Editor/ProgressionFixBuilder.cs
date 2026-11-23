using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S53：修复章节流程 + 怪物位置 + 挂载目标提示 HUD。
    ///
    /// ══════════════════ 三件事 ══════════════════
    ///
    ///   ① 挂 ObjectiveHUD 到 Player（Ch3/Ch4/Ch5）
    ///      让玩家能看到本章目标与下一步提示。
    ///
    ///   ② 修正卡在墙里的 Mimic
    ///      诊断实测：Ch4 的 Mimic_G6、Mimic_G5 位置与墙体碰撞体重叠。
    ///      把它们的初始位置推移到最近的可行走点。
    ///
    ///   ③ 幂等：已存在则不重复添加
    /// </summary>
    public static class ProgressionFixBuilder
    {
        private const string SceneFmt = "Assets/_Project/Scenes/Level_Chapter{0}_Graybox.unity";

        [MenuItem("ABYSS ECHO/修复 v5.9/流程与怪物 - 第三章")]
        public static void BuildCh3() => BuildFor(3);

        [MenuItem("ABYSS ECHO/修复 v5.9/流程与怪物 - 第四章")]
        public static void BuildCh4() => BuildFor(4);

        [MenuItem("ABYSS ECHO/修复 v5.9/流程与怪物 - 第五章")]
        public static void BuildCh5() => BuildFor(5);

        public static void BuildFor(int chapterNum)
        {
            string path = string.Format(SceneFmt, chapterNum);
            if (!File.Exists(path)) { Fail(chapterNum, $"场景不存在：{path}"); return; }

            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Log(chapterNum, $"已打开 {path}");

            int changes = 0;

            // ══ ① 挂 ObjectiveHUD ══
            var motor = Object.FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
            if (motor == null) { Fail(chapterNum, "找不到 PlayerMotor"); return; }

            var player = motor.gameObject;
            var hud = player.GetComponent<AbyssEcho.UI.ObjectiveHUD>();
            if (hud == null)
            {
                hud = player.AddComponent<AbyssEcho.UI.ObjectiveHUD>();
                Log(chapterNum, "  + 已添加 ObjectiveHUD @ Player");
                changes++;
            }
            else
            {
                Log(chapterNum, "  ObjectiveHUD 已存在，跳过");
            }

            // ══ ② 修正卡墙的 Mimic ══
            var mimics = Object.FindObjectsByType<AbyssEcho.Horror.MimicStalker>(FindObjectsSortMode.None);
            Log(chapterNum, $"  MimicStalker 数量 = {mimics.Length}");

            foreach (var m in mimics)
            {
                Vector3 p = m.transform.position;

                // 检查当前位置是否与墙体重叠
                var overlaps = Physics.OverlapSphere(p + Vector3.up * 0.9f, 0.45f,
                                                     ~0, QueryTriggerInteraction.Ignore);
                bool inWall = false;
                foreach (var o in overlaps)
                {
                    if (o == null || o.isTrigger) continue;
                    if (o.transform.IsChildOf(m.transform)) continue;
                    inWall = true;
                    break;
                }

                if (!inWall)
                {
                    Log(chapterNum, $"    {m.gameObject.name} @ {p} 位置正常");
                    continue;
                }

                // 找最近的可行走点
                Vector3 safe = FindNearestWalkable(p);
                Log(chapterNum, $"    ⚠️ {m.gameObject.name} @ {p} 在墙里 → 移到 {safe}");

                m.transform.position = safe;

                // 同步 _homePosition（重生兜底用）
                var fl = typeof(AbyssEcho.Horror.MimicStalker).GetField("_homePosition",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (fl != null) fl.SetValue(m, safe);

                EditorUtility.SetDirty(m);
                changes++;
            }

            // ══ 保存 ══
            if (changes > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                bool ok = EditorSceneManager.SaveScene(scene, path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Log(chapterNum, ok ? $"场景已保存（{changes} 项改动）" : "❌ 保存失败");
            }
            else
            {
                Log(chapterNum, "无改动");
            }

            // ══ 验证 ══
            Log(chapterNum, "---- 验证 ----");
            var hud2 = Object.FindFirstObjectByType<AbyssEcho.UI.ObjectiveHUD>();
            Log(chapterNum, $"  ObjectiveHUD = {(hud2 != null ? "@" + hud2.gameObject.name : "❌")}");

            var ending = Object.FindFirstObjectByType<AbyssEcho.Narrative.ChapterEndingSystem>();
            if (ending != null)
            {
                var tcF = typeof(AbyssEcho.Narrative.ChapterEndingSystem).GetField("triggerCenter");
                var tsF = typeof(AbyssEcho.Narrative.ChapterEndingSystem).GetField("triggerSize");
                Vector3 tc = (Vector3)tcF.GetValue(ending);
                Vector3 ts = (Vector3)tsF.GetValue(ending);
                Vector3 endPt = AbyssEcho.LevelGen.ChapterLayout.GetChapterEndPoint(
                    (AbyssEcho.LevelGen.ChapterLayout.Chapter)chapterNum);

                Log(chapterNum, $"  结局触发中心 = {tc}  尺寸 = {ts}");
                Log(chapterNum, $"  章节终点 = {endPt}");
                bool covers = Mathf.Abs(endPt.x - tc.x) <= ts.x * 0.5f + 0.5f &&
                              Mathf.Abs(endPt.z - tc.z) <= ts.z * 0.5f + 0.5f;
                Log(chapterNum, $"  触发区覆盖终点 = {(covers ? "✅" : "❌")}");
            }

            Log(chapterNum, "完成");
        }

        /// <summary>从给定点向外螺旋搜索最近的可行走位置。</summary>
        private static Vector3 FindNearestWalkable(Vector3 origin)
        {
            // 先试原地微调
            for (float r = 0.5f; r <= 6f; r += 0.5f)
            {
                for (int i = 0; i < 16; i++)
                {
                    float ang = i * 22.5f * Mathf.Deg2Rad;
                    Vector3 cand = origin + new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);

                    // 无墙
                    var ov = Physics.OverlapSphere(cand + Vector3.up * 0.9f, 0.45f,
                                                   ~0, QueryTriggerInteraction.Ignore);
                    bool bad = false;
                    foreach (var o in ov)
                    {
                        if (o == null || o.isTrigger) continue;
                        bad = true; break;
                    }
                    if (bad) continue;

                    // 有地面
                    if (!Physics.Raycast(cand + Vector3.up * 0.5f, Vector3.down, 3f,
                                         ~0, QueryTriggerInteraction.Ignore))
                        continue;

                    return cand;
                }
            }
            return origin;   // 实在找不到，保持原位
        }

        private static void Log(int ch, string msg)
        {
            string line = $"[ProgFix] Ch{ch} | {msg}";
            Debug.Log(line);
            Write(line);
        }

        private static void Fail(int ch, string msg)
        {
            string line = $"[ProgFix] Ch{ch} | ❌ {msg}";
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
                File.AppendAllText(Path.Combine(dir, "ProgressionFix_Result.txt"),
                                   line + "\n", new System.Text.UTF8Encoding(false));
            }
            catch { }
        }
    }
}