using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    public static class DebugComponentList
    {
        public static void Run()
        {
            var sb = new StringBuilder();
            foreach (var path in new[] {
                "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity",
                "Assets/_Project/Scenes/Level_Chapter2_Graybox.unity",
                "Assets/_Project/Scenes/Level_Chapter3_Graybox.unity" })
            {
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                sb.AppendLine($"=== {Path.GetFileName(path)} ===");
                var all = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
                int n = 0;
                foreach (var mb in all)
                {
                    if (mb == null) continue;
                    var t = mb.GetType();
                    if (t.Namespace != null && t.Namespace.Contains("DebugTools"))
                    {
                        var f = t.GetField("runOnStart");
                        string v = f != null ? f.GetValue(mb).ToString() : "(无字段)";
                        sb.AppendLine($"  {t.Name} @ {mb.gameObject.name}  runOnStart={v} enabled={mb.enabled}");
                        n++;
                    }
                }
                sb.AppendLine($"  共 {n} 个 DebugTools 组件");
                sb.AppendLine();
            }
            string text = sb.ToString();
            Debug.Log(text);
            File.WriteAllText(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports/DebugComponentList.txt")), text, new UTF8Encoding(false));
        }
    }
}