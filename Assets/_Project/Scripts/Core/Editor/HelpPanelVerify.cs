using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace AbyssEcho.Core.EditorTools {
    public static class HelpPanelVerify {
        public static void Run() {
            var sb = new StringBuilder();
            sb.AppendLine("=== 帮助面板功能验证 ===");
            sb.AppendLine();
            var scenes = new (int ch, string p)[] {
                (1,"Assets/_Project/Scenes/Level_Chapter1_Graybox.unity"),
                (2,"Assets/_Project/Scenes/Level_Chapter2_Graybox.unity"),
                (3,"Assets/_Project/Scenes/Level_Chapter3_Graybox.unity"),
                (4,"Assets/_Project/Scenes/Level_Chapter4_Graybox.unity"),
                (5,"Assets/_Project/Scenes/Level_Chapter5_Graybox.unity"),
            };
            foreach (var (ch, p) in scenes) {
                if (!File.Exists(p)) continue;
                EditorSceneManager.OpenScene(p, OpenSceneMode.Single);
                var peace = Object.FindObjectsByType<AbyssEcho.DebugTools.PeaceModeManager>(FindObjectsSortMode.None);
                var help = Object.FindObjectsByType<AbyssEcho.UI.ControlsHelpUI>(FindObjectsSortMode.None);
                var ending = Object.FindObjectsByType<AbyssEcho.Narrative.ChapterEndingSystem>(FindObjectsSortMode.None);
                sb.AppendLine($"第{ch}章:");
                sb.AppendLine($"  PeaceModeManager = {peace.Length} 个");
                foreach (var pm in peace) {
                    var keyF = typeof(AbyssEcho.DebugTools.PeaceModeManager).GetField("toggleKey");
                    sb.AppendLine($"    {pm.gameObject.name}  toggleKey={keyF?.GetValue(pm)}");
                }
                sb.AppendLine($"  ControlsHelpUI = {help.Length} 个");
                sb.AppendLine($"  ChapterEndingSystem = {ending.Length} 个");
            }
            Debug.Log(sb.ToString());
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\HelpPanelVerify.txt", sb.ToString(), new UTF8Encoding(false));
        }
    }
}