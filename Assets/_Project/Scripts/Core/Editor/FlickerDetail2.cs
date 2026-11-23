using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    public static class FlickerDetail2
    {
        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== FlickerLight 详细清单（含世界坐标，用于区分同名灯）===");

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

                int i = 0;
                foreach (var f in fls)
                {
                    i++;
                    var lt = f.GetComponent<Light>();
                    Vector3 pos = f.transform.position;
                    sb.AppendLine($"  [{i:D2}] {f.gameObject.name,-24} pos=({pos.x,7:F1},{pos.y,5:F1},{pos.z,7:F1})  " +
                                  $"base={f.baseIntensity,4:F1}  mode={f.mode,-7} int={(lt != null ? lt.intensity.ToString("F1") : "?"),5}  en={(lt != null ? lt.enabled.ToString() : "?"),-5}  " +
                                  $"interval={f.minInterval:F1}~{f.maxInterval:F1} dim={f.dimRatio:F2} fear={f.fearReactive}");
                }
            }

            string text = sb.ToString();
            Debug.Log(text);
            File.WriteAllText(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports/FlickerDetail.txt")), text, new UTF8Encoding(false));
        }
    }
}