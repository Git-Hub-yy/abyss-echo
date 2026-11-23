using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// S30 录音音频测试：
    ///   T1 AudioManager 有 PlayTape 接口
    ///   T2 录音机能生成音频（长度 > 0）
    ///   T3 播放时不影响环境音
    ///   T4 播放后 StopTape 能停
    /// </summary>
    public class AutoTest_S30 : MonoBehaviour
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
            _log.AppendLine("ABYSS ECHO · S30 录音音频测试");
            _log.AppendLine("=====================================");

            AbyssEcho.Save.SaveSystem.Delete();
            yield return null;
            yield return new WaitForSeconds(0.6f);

            var am = AbyssEcho.Audio.AudioManager.Instance;
            Check("T1 AudioManager 存在", am != null, am != null ? "ok" : "❌ null");
            if (am == null) { Finish(); yield break; }

            // T2 生成音频
            var voice = AbyssEcho.Audio.ProceduralAudio.CreateTapeVoice("tape.1", 10f);
            var click = AbyssEcho.Audio.ProceduralAudio.CreateTapeClick();

            Check("T2 录音音频已生成",
                  voice != null && voice.length > 1f && click != null,
                  voice != null ? $"人声 {voice.length:F1}s / 咔哒 {click.length:F2}s" : "❌ null");

            if (voice == null) { Finish(); yield break; }

            // T3 播放不影响环境音
            am.PlayTape(voice, 0.75f);
            yield return new WaitForSeconds(0.5f);

            bool tapePlaying = am.IsTapePlaying;
            Check("T3 录音正在播放", tapePlaying, $"IsTapePlaying={tapePlaying}");

            // 播放录音期间，再播一个脚步声 —— 两者应该共存
            am.PlayFootstep(transform.position, false);
            yield return new WaitForSeconds(0.3f);

            bool stillPlaying = am.IsTapePlaying;
            Check("T4 播其它音效不打断录音", stillPlaying,
                  stillPlaying ? "录音仍在播放 ✅" : "❌ 录音被中断");

            // T5 停止
            am.StopTape();
            yield return new WaitForSeconds(0.2f);
            Check("T5 StopTape 能停止录音", !am.IsTapePlaying,
                  !am.IsTapePlaying ? "已停止" : "❌ 还在播");

            // T6 场景里的录音机确实有音频
            var tapes = FindObjectsByType<AbyssEcho.Puzzle.TapeRecorder>(FindObjectsSortMode.None);
            Check("T6 场景中有录音机", tapes.Length > 0, $"共 {tapes.Length} 台");

            if (tapes.Length > 0)
            {
                var t = tapes[0];
                var f = typeof(AbyssEcho.Puzzle.TapeRecorder).GetField("_voiceClip",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var clip = f?.GetValue(t) as AudioClip;
                Check("T7 录音机已生成专属音频",
                      clip != null && clip.length > 1f,
                      clip != null ? $"{t.name} 音频 {clip.length:F1}s" : "❌ 未生成");
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
            Debug.Log("<b>[S30Test]</b>\n" + text);
            try
            {
                string dir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports"));
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "S30_AutoTestResult.txt"), text, new UTF8Encoding(false));
            }
            catch { }
            if (!Application.isEditor) Application.Quit(_fail == 0 ? 0 : 1);
        }
    }
}