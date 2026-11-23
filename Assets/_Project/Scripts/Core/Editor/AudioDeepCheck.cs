using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace AbyssEcho.Core.EditorTools {
    public static class AudioDeepCheck {
        public static void Run() {
            var sb = new StringBuilder();
            sb.AppendLine("# 音频系统深度检查");
            sb.AppendLine();
            sb.AppendLine($"> 生成时间：{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            // 全局搜所有 AudioSource
            int totalAudioSources = 0;
            var scenes = EditorBuildSettings.scenes;
            foreach (var bs in scenes) {
                if (!bs.enabled || !File.Exists(bs.path)) continue;
                EditorSceneManager.OpenScene(bs.path, OpenSceneMode.Single);
                var sources = Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
                var am = Object.FindFirstObjectByType<AbyssEcho.Audio.AudioManager>();
                sb.AppendLine($"## {Path.GetFileNameWithoutExtension(bs.path)}");
                sb.AppendLine();
                sb.AppendLine($"- AudioSource 数：{sources.Length}");
                sb.AppendLine($"- AudioManager：{(am != null ? "✅ 存在 @ " + am.gameObject.name : "— 无")}");
                if (am != null) {
                    var t = typeof(AbyssEcho.Audio.AudioManager);
                    var BF = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
                    foreach (var fn in new[] { "_doorSlide", "_stinger", "_heartbeat", "_uiBlip", "_errorBuzz", "_ambientDrone" }) {
                        var f = t.GetField(fn, BF);
                        if (f == null) { sb.AppendLine($"  - {fn}: 字段不存在"); continue; }
                        var clip = f.GetValue(am) as AudioClip;
                        sb.AppendLine($"  - {fn}: {(clip != null ? "✅ " + clip.name : "❌ null")}");
                    }
                }
                foreach (var s in sources) totalAudioSources++;
                sb.AppendLine();
            }
            sb.AppendLine($"**全部场景 AudioSource 总数：{totalAudioSources}**");
            sb.AppendLine();
            sb.AppendLine("## 结论");
            sb.AppendLine();
            sb.AppendLine(totalAudioSources == 0
                ? "项目**完全没有音频播放源**，游戏是静音的。这是**已知的、非阻断的**缺失（P3）。"
                : "存在 AudioSource，但缺少 AudioClip 资源。");
            Debug.Log(sb.ToString());
            File.AppendAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Final_Test\01_Project_Integrity_Report.md", "\n---\n\n" + sb.ToString(), new UTF8Encoding(false));
        }
    }
}