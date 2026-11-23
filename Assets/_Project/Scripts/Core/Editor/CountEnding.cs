using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace AbyssEcho.Core.EditorTools {
    public static class CountEnding {
        public static void Run() {
            var sb = new StringBuilder();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_Chapter4_Graybox.unity", OpenSceneMode.Single);
            var all = Object.FindObjectsByType<AbyssEcho.Narrative.ChapterEndingSystem>(FindObjectsSortMode.None);
            sb.AppendLine($"Ch4 ChapterEndingSystem 数量 = {all.Length}");
            foreach (var e in all) {
                var tcF = typeof(AbyssEcho.Narrative.ChapterEndingSystem).GetField("triggerCenter");
                var tc = tcF != null ? (Vector3)tcF.GetValue(e) : Vector3.zero;
                sb.AppendLine($"  @{e.gameObject.name}  active={e.gameObject.activeInHierarchy}  triggerCenter={tc}");
            }
            Debug.Log(sb.ToString());
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\CountEnding_Ch4.txt", sb.ToString(), new UTF8Encoding(false));
        }
    }
}