using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// S29 三项新功能测试：
    ///   T1 第二章有 AudioManager
    ///   T2 暂停菜单有返回主菜单
    ///   T3 结局后有进入下一章
    /// </summary>
    public class AutoTest_S29 : MonoBehaviour
    {
        public bool runOnStart = true;

        private int _pass, _fail;
        private readonly StringBuilder _log = new StringBuilder();

        private void Start()
        {
            if (runOnStart) StartCoroutine(RunAll());
        }

        private IEnumerator RunAll()
        {
            _log.AppendLine("ABYSS ECHO · S29 新功能测试");
            _log.AppendLine("=====================================");
            yield return new WaitForSeconds(0.6f);

            // ── T1 音频 ──
            var audio = AbyssEcho.Audio.AudioManager.Instance;
            Check("T1 AudioManager 存在", audio != null,
                  audio != null
                    ? $"master={audio.masterVolume:F2} sfx={audio.sfxVolume:F2} ambient={audio.ambientVolume:F2}"
                    : "❌ Instance 为 null，所有音效都会被静默跳过");

            // ── T2 暂停菜单 ──
            var pause = FindFirstObjectByType<AbyssEcho.UI.PauseMenuUI>();
            Check("T2 暂停菜单存在", pause != null, pause != null ? pause.gameObject.name : "");

            if (pause != null)
            {
                // 用反射检查是否有 GoToMainMenu 方法（新加的功能）
                var m = pause.GetType().GetMethod("GoToMainMenu",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Check("T2 暂停菜单有返回主菜单方法", m != null,
                      m != null ? "已实现" : "❌ 未找到");
            }

            // ── T3 结局 ──
            var ending = FindFirstObjectByType<AbyssEcho.Narrative.ChapterEndingSystem>();
            Check("T3 结局系统存在", ending != null, ending != null ? ending.gameObject.name : "");

            if (ending != null)
            {
                var goNext = ending.GetType().GetMethod("GoToNextChapter",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var draw = ending.GetType().GetMethod("DrawPostEndingButtons",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

                Check("T3 结局系统有进入下一章方法", goNext != null, goNext != null ? "已实现" : "❌ 未找到");
                Check("T3 结局系统有按钮绘制方法", draw != null, draw != null ? "已实现" : "❌ 未找到");
            }

            Finish();
        }

        private void Check(string name, bool ok, string detail = "")
        {
            if (ok) { _pass++; _log.AppendLine($"[通过] {name} {detail}"); }
            else { _fail++; _log.AppendLine($"[失败] {name} {detail}"); }
        }

        private void Finish()
        {
            _log.AppendLine("=====================================");
            _log.AppendLine($"结果：通过 {_pass}，失败 {_fail}");
            string text = _log.ToString();
            Debug.Log("<b>[S29Test]</b>\n" + text);
            try
            {
                string dir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports"));
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "S29_AutoTestResult.txt"), text, new UTF8Encoding(false));
            }
            catch { }
            if (!Application.isEditor) Application.Quit(_fail == 0 ? 0 : 1);
        }
    }
}