using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    public static class FlickerDetail
    {
        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== 各章 FlickerLight 详细清单（含房间路径）===");

            string[] scenes = {
                "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity",
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

                var fls = Object.FindObjectsByType<AbyssEcho.Horror.FlickerLight>(FindObjectsSortMode.None);
                sb.AppendLine($"  总数 = {fls.Length}");
                sb.AppendLine($"  {"#",-3} {"灯光名",-24} {"房间",-24} {"base",-6} {"mode",-8} {"int",-6} {"enabled",-8}");
                sb.AppendLine($"  {new string('-', 95)}");

                int i = 0;
                foreach (var f in fls)
                {
                    i++;
                    var lt = f.GetComponent<Light>();
                    string room = f.transform.parent != null ? f.transform.parent.name : "-";
                    sb.AppendLine($"  {i,-3} {f.gameObject.name,-24} {room,-24} {f.baseIntensity,-6:F1} {f.mode,-8} " +
                                  $"{(lt != null ? lt.intensity.ToString("F1") : "?"),-6} {(lt != null ? lt.enabled.ToString() : "?"),-8}");
                }
            }

            string text = sb.ToString();
            Debug.Log(text);
            File.WriteAllText(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports/FlickerDetail.txt")), text, new UTF8Encoding(false));
        }
    }
}