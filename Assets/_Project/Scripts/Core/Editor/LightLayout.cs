using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    public static class LightLayout
    {
        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Ch2~Ch5 灯光布局（供选择闪烁灯）===");

            string[] scenes = {
                "Assets/_Project/Scenes/Level_Chapter2_Graybox.unity",
                "Assets/_Project/Scenes/Level_Chapter3_Graybox.unity",
                "Assets/_Project/Scenes/Level_Chapter4_Graybox.unity",
                "Assets/_Project/Scenes/Level_Chapter5_Graybox.unity",
            };

            foreach (var p in scenes)
            {
                EditorSceneManager.OpenScene(p, OpenSceneMode.Single);
                sb.AppendLine();
                sb.AppendLine($"### {Path.GetFileNameWithoutExtension(p)}");

                var root = GameObject.Find("--- ROOM_LIGHTS ---");
                Transform rt = root != null ? root.transform : null;

                var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
                sb.AppendLine($"  灯光总数 = {lights.Length}");

                int idx = 0;
                foreach (var l in lights)
                {
                    idx++;
                    string path = GetPath(l.transform);

                    // 分类
                    string kind = "Ceiling";
                    string n = l.gameObject.name;
                    if (n.Contains("Warning") || n.Contains("Emergency")) kind = "★Warning/Emergency";
                    else if (n.Contains("Door") || n.Contains("Gate")) kind = "Door";

                    sb.AppendLine($"    [{idx:D2}] {n,-32} {kind,-20} type={l.type,-11} int={l.intensity,6:F1} range={l.range,5:F1} color=({l.color.r:F2},{l.color.g:F2},{l.color.b:F2}) enabled={l.enabled}");
                }
            }

            string text = sb.ToString();
            Debug.Log(text);
            File.WriteAllText(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports/LightLayout.txt")), text, new UTF8Encoding(false));
        }

        private static string GetPath(Transform t)
        {
            string p = t.name;
            var cur = t.parent; int g = 0;
            while (cur != null && g++ < 20) { p = cur.name + "/" + p; cur = cur.parent; }
            return p;
        }
    }
}