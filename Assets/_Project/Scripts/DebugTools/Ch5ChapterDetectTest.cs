using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// S77：验证「结局触发点按场景校正」修复。
    ///
    /// 测试思路：
    ///   T1 故意把 ChapterLayout.Current 设成 Chapter4
    ///   T2 重新加载第五章场景，让 ChapterEndingSystem.Awake() 跑一遍
    ///   T3 检查触发点是否被校正为 H7 中心 (0, 0.9, 54)
    ///   T4 检查 H6 位置 (z=39) 是否已不在触发区内
    ///
    /// 用 -ch5detect 命令行参数启动。
    /// </summary>
    public class Ch5ChapterDetectTest : MonoBehaviour
    {
        private readonly StringBuilder _log = new StringBuilder();
        private int _pass, _fail;

        private void Start()
        {
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            _log.AppendLine("══════ 第五章结局触发点校正验证 ══════");

            // ── T1：故意设成错章节 ──
            AbyssEcho.LevelGen.ChapterLayout.Current = AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter4;
            _log.AppendLine();
            _log.AppendLine("──── T1 故意设为 Chapter4 ────");
            _log.AppendLine($"  ChapterLayout.Current = {AbyssEcho.LevelGen.ChapterLayout.Current}");

            // ── T2：等一帧，然后重新加载场景让 Awake 重跑 ──
            _log.AppendLine();
            _log.AppendLine("──── T2 重新加载第五章场景 ────");
            string cur = SceneManager.GetActiveScene().name;
            _log.AppendLine($"  当前场景 = {cur}");

            // 不重新加载（避免丢测试对象），改为手动调用 Awake 里的校正方法
            var ending = FindFirstObjectByType<AbyssEcho.Narrative.ChapterEndingSystem>();
            if (ending == null)
            {
                _log.AppendLine("  ❌ 找不到 ChapterEndingSystem");
                Dump(); yield break;
            }

            var t = typeof(AbyssEcho.Narrative.ChapterEndingSystem);
            var ensureM = t.GetMethod("EnsureChapterMatchesScene",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var applyM = t.GetMethod("ApplyChapterConfig",
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (ensureM == null || applyM == null)
            {
                _log.AppendLine($"  ❌ 找不到方法 ensure={ensureM != null} apply={applyM != null}");
                Dump(); yield break;
            }

            // 调用校正
            ensureM.Invoke(ending, null);
            yield return null;
            applyM.Invoke(ending, null);
            yield return null;

            _log.AppendLine($"  校正后 ChapterLayout.Current = {AbyssEcho.LevelGen.ChapterLayout.Current}");
            Check("T2 章节被校正为Chapter5",
                  AbyssEcho.LevelGen.ChapterLayout.Current == AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter5,
                  $"actual={AbyssEcho.LevelGen.ChapterLayout.Current}");

            // ── T3：检查触发点 ──
            _log.AppendLine();
            _log.AppendLine("──── T3 触发点 ────");
            var tcF = t.GetField("triggerCenter", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            var tsF = t.GetField("triggerSize", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Vector3 tc = tcF != null ? (Vector3)tcF.GetValue(ending) : Vector3.zero;
            Vector3 ts = tsF != null ? (Vector3)tsF.GetValue(ending) : Vector3.zero;

            _log.AppendLine($"  triggerCenter = {tc}");
            _log.AppendLine($"  triggerSize   = {ts}");
            _log.AppendLine($"  触发范围 Z[{tc.z - ts.z / 2:F1}, {tc.z + ts.z / 2:F1}]");

            // S78：触发区移到出口门处（控制台 Z=57.8 之后），Z≈59
            bool centerOk = Mathf.Abs(tc.z - 59f) < 0.6f;
            Check("T3a 触发点在出口门处 (Z≈59)", centerOk, $"z={tc.z:F2}");

            // 关键：控制台必须在触发区之前，否则玩家没机会选结局
            bool consoleBefore = tc.z - ts.z / 2f > 57.8f;
            Check("T3b 控制台(Z=57.8)在触发区之前", consoleBefore,
                  $"触发区起点 z={tc.z - ts.z / 2f:F1}");

            // ── T4：H6 位置不应在触发区内 ──
            _log.AppendLine();
            _log.AppendLine("──── T4 H6 位置不在触发区 ────");
            float zMin = tc.z - ts.z / 2f;
            float zMax = tc.z + ts.z / 2f;
            bool h6Outside = !(39f >= zMin && 39f <= zMax);
            _log.AppendLine($"  H6 位置 z=39  触发区 Z[{zMin:F1}, {zMax:F1}]  → {(h6Outside ? "不在区内 ✅" : "仍在区内 ❌")}");
            Check("T4 H6(Z=39) 不在触发区", h6Outside, $"range=[{zMin:F1},{zMax:F1}]");

            // H7 入口(Z=48)不应触发
            bool h7EntranceOutside = !(48f >= zMin && 48f <= zMax);
            Check("T4b H7 入口(Z=48) 不触发", h7EntranceOutside, $"range=[{zMin:F1},{zMax:F1}]");

            // 出口门(Z=59.6)应在触发区附近
            bool exitCovered = 59.6f >= zMin - 0.5f && 59.6f <= zMax + 0.5f;
            Check("T4c 出口门(Z=59.6) 在触发区附近", exitCovered, $"range=[{zMin:F1},{zMax:F1}]");

            Dump();
        }

        private void Check(string name, bool ok, string detail)
        {
            if (ok) { _pass++; _log.AppendLine($"[通过] {name}  {detail}"); }
            else { _fail++; _log.AppendLine($"[失败] {name}  {detail}"); }
        }

        private void Dump()
        {
            _log.AppendLine();
            _log.AppendLine("=====================================");
            _log.AppendLine($"通过 {_pass}，失败 {_fail}");
            string text = _log.ToString();
            Debug.Log("<b>[Ch5ChapterDetectTest]</b>\n" + text);
            try
            {
                Directory.CreateDirectory(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports");
                File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Ch5ChapterDetectTest.txt",
                                  text, new UTF8Encoding(false));
            }
            catch { }
            if (!Application.isEditor) Application.Quit(_fail == 0 ? 0 : 1);
        }
    }
}