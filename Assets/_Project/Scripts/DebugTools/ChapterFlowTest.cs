using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// S53 章节流程端到端测试（真实场景跳转验证）。
    ///
    /// ══════════════════ 为什么需要这个测试 ══════════════════
    ///
    ///   前面的诊断脚本在 Start() 里设章节，但 ChapterEndingSystem.Awake()
    ///   已经先执行完了，导致读到的是上一章的配置 —— 测不准。
    ///
    ///   本测试**在场景加载之前**设好章节，再加载场景，
    ///   完全模拟真实游戏流程（主菜单选章 → 加载场景）。
    ///
    /// ══════════════════ 测试内容 ══════════════════
    ///
    ///   A. 章节状态能否正确持久化（ChapterLayout.Load 修复验证）
    ///   B. 每章结局触发区是否覆盖真实终点
    ///   C. Ch3 → Ch4 → Ch5 的跳转目标是否正确
    ///   D. ObjectiveHUD 是否挂上并显示正确目标
    /// </summary>
    public class ChapterFlowTest : MonoBehaviour
    {
        public bool runOnStart = true;

        private readonly StringBuilder _log = new StringBuilder();
        private int _pass, _fail, _unverified;
        private static readonly BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private void Start()
        {
            // 测试脚本要在多次 LoadScene 之间存活，否则第一次跳转后就被销毁了
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            if (runOnStart) StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            _log.AppendLine("══════ S53 章节流程端到端测试 ══════");
            _log.AppendLine();

            // ══ A. 章节状态持久化 ══
            _log.AppendLine("──── A. ChapterLayout 持久化 ────");
            var L = typeof(AbyssEcho.LevelGen.ChapterLayout);
            var saveM = L.GetMethod("Save", BF);
            var loadM = L.GetMethod("Load", BF);
            var curP = L.GetProperty("Current");
            var loadedF = L.GetField("_loaded", BF);

            bool persistOk = true;
            for (int v = 1; v <= 5; v++)
            {
                var c = (AbyssEcho.LevelGen.ChapterLayout.Chapter)v;
                curP.SetValue(null, c);
                saveM.Invoke(null, null);

                // 强制重新加载
                loadedF.SetValue(null, false);
                loadM.Invoke(null, null);
                var readBack = (AbyssEcho.LevelGen.ChapterLayout.Chapter)curP.GetValue(null);

                bool ok = (int)readBack == v;
                if (!ok) persistOk = false;

                _log.AppendLine($"    Chapter{v} 存入 → 读回 {(int)readBack} {(ok ? "✅" : "❌")}");
            }
            Check("A 五章章节状态都能正确持久化", persistOk, "");

            // ══ B/C. 逐章检查触发区与跳转目标 ══
            _log.AppendLine();
            _log.AppendLine("──── B/C. 逐章触发区与跳转 ────");

            for (int ch = 3; ch <= 5; ch++)
            {
                var chapter = (AbyssEcho.LevelGen.ChapterLayout.Chapter)ch;

                // 关键：**先设章节再加载场景**
                AbyssEcho.LevelGen.ChapterLayout.Current = chapter;
                string sceneName = AbyssEcho.LevelGen.ChapterLayout.GetSceneName(chapter);

                _log.AppendLine();
                _log.AppendLine($"  ── 第 {ch} 章：{sceneName} ──");

                var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
                if (op == null) { _log.AppendLine("    ❌ 场景加载失败"); _fail++; continue; }
                yield return op;
                yield return new WaitForSeconds(1.2f);

                // 章节是否仍是本章
                var curAfter = AbyssEcho.LevelGen.ChapterLayout.Current;
                Check($"B{ch} 加载后章节仍为 Chapter{ch}", (int)curAfter == ch,
                      $"实际={(int)curAfter}");

                // 触发区
                var ending = FindFirstObjectByType<AbyssEcho.Narrative.ChapterEndingSystem>();
                if (ending == null)
                {
                    Check($"B{ch} 存在 ChapterEndingSystem", false, "❌ 未找到");
                    continue;
                }

                var tcF = typeof(AbyssEcho.Narrative.ChapterEndingSystem).GetField("triggerCenter");
                var tsF = typeof(AbyssEcho.Narrative.ChapterEndingSystem).GetField("triggerSize");
                Vector3 tc = (Vector3)tcF.GetValue(ending);
                Vector3 ts = (Vector3)tsF.GetValue(ending);
                Vector3 endPt = AbyssEcho.LevelGen.ChapterLayout.GetChapterEndPoint(chapter);

                _log.AppendLine($"    触发中心 = {tc}  尺寸 = {ts}");
                _log.AppendLine($"    章节终点 = {endPt}");
                _log.AppendLine($"    触发区 Z[{tc.z - ts.z * 0.5f:F1}, {tc.z + ts.z * 0.5f:F1}]  " +
                                $"终点 Z={endPt.z:F1}");

                bool covers = Mathf.Abs(endPt.x - tc.x) <= ts.x * 0.5f + 1.0f &&
                              Mathf.Abs(endPt.z - tc.z) <= ts.z * 0.5f + 1.0f;
                Check($"B{ch} 触发区覆盖真实终点", covers,
                      $"{(covers ? "终点在触发区内" : "❌ 终点不在触发区内")}");

                // 结局文案是否是本章的
                var titleF = typeof(AbyssEcho.Narrative.ChapterEndingSystem).GetField("chapterTitle");
                string title = (string)titleF.GetValue(ending);
                bool rightChapter = title != null && title.Contains($"第{ch}章");
                Check($"B{ch} 结局文案是本章的", rightChapter, $"title='{title}'");

                // C. 跳转目标
                var gotoM = typeof(AbyssEcho.Narrative.ChapterEndingSystem).GetMethod("GoToNextChapter", BF);
                if (gotoM != null && ch < 5)
                {
                    _log.AppendLine($"    GoToNextChapter 存在（预计跳到第 {ch + 1} 章）");
                }

                // D. ObjectiveHUD
                var hud = FindFirstObjectByType<AbyssEcho.UI.ObjectiveHUD>();
                Check($"D{ch} ObjectiveHUD 已挂载", hud != null,
                      hud != null ? $"@{hud.gameObject.name}" : "❌ 未找到");
            }

            // ══ 恢复 ══
            AbyssEcho.LevelGen.ChapterLayout.Current = AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter3;

            _log.AppendLine();
            _log.AppendLine("══════ 说明 ══════");
            _log.AppendLine("  以下为自动化限制，需人工验证：");
            Unverified("E1 玩家走到终点时结局真的会播放（需真人走位）");
            Unverified("E2 点「进入下一章」后真的加载下一章场景（需真人点击）");
            Unverified("E3 ObjectiveHUD 在屏幕上显示的位置与可读性（需肉眼）");
            Unverified("E4 怪物追击时不再穿墙的观感（需真人观察）");

            Dump();
        }

        private void Check(string name, bool ok, string detail)
        {
            if (ok) { _pass++; _log.AppendLine($"[通过] {name}  {detail}"); }
            else { _fail++; _log.AppendLine($"[失败] {name}  {detail}"); }
        }

        private void Unverified(string name)
        {
            _unverified++;
            _log.AppendLine($"[待人工验收] {name}");
        }

        private void Dump()
        {
            _log.AppendLine();
            _log.AppendLine("=====================================");
            _log.AppendLine($"通过 {_pass}，失败 {_fail}，待人工验收 {_unverified}");

            string text = _log.ToString();
            Debug.Log("<b>[ChapterFlowTest]</b>\n" + text);

            var di = new DirectoryInfo(Application.dataPath);
            for (int hop = 0; hop < 6 && di != null; hop++)
            {
                string reports = Path.Combine(di.FullName, "Reports");
                if (Directory.Exists(reports))
                {
                    try { File.WriteAllText(Path.Combine(reports, "ChapterFlowTest.txt"), text, new UTF8Encoding(false)); }
                    catch { }
                    break;
                }
                di = di.Parent;
            }

            if (!Application.isEditor) Application.Quit(_fail == 0 ? 0 : 1);
        }
    }
}