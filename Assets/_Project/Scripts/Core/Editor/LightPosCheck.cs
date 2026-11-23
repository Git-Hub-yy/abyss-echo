using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    public static class LightPosCheck
    {
        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== 灯光世界坐标重复检查 ===");

            string[] scenes = {
                "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity",
                "Assets/_Project/Scenes/Level_Chapter3_Graybox.unity",
            };

            foreach (var p in scenes)
            {
                EditorSceneManager.OpenScene(p, OpenSceneMode.Single);
                sb.AppendLine();
                sb.AppendLine($"### {Path.GetFileNameWithoutExtension(p)}");

                var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
                var posCount = new Dictionary<string, int>();

                foreach (var l in lights)
                {
                    Vector3 w = l.transform.position;
                    string key = $"({w.x:F1},{w.y:F1},{w.z:F1})";
                    if (!posCount.ContainsKey(key)) posCount[key] = 0;
                    posCount[key]++;
                }

                int dupGroups = 0, dupTotal = 0;
                foreach (var kv in posCount)
                {
                    if (kv.Value > 1) { dupGroups++; dupTotal += kv.Value; }
                }

                sb.AppendLine($"  灯光总数 = {lights.Length}");
                sb.AppendLine($"  唯一坐标数 = {posCount.Count}");
                sb.AppendLine($"  重复坐标组数 = {dupGroups}，涉及灯光 = {dupTotal}");

                int shown = 0;
                foreach (var kv in posCount)
                {
                    if (kv.Value > 1 && shown < 10)
                    {
                        sb.AppendLine($"    坐标 {kv.Key} 有 {kv.Value} 盏灯");
                        shown++;
                    }
                }

                // 抽样看几盏灯的世界坐标与局部坐标
                sb.AppendLine("  --- 抽样（前 8 盏）---");
                int n = 0;
                foreach (var l in lights)
                {
                    if (n++ >= 8) break;
                    sb.AppendLine($"    {l.gameObject.name,-26} world={l.transform.position}  local={l.transform.localPosition}  " +
                                  $"parent={l.transform.parent?.name ?? "-"}");
                }
            }

            string text = sb.ToString();
            Debug.Log(text);
            File.WriteAllText(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports/LightPosCheck.txt")), text, new UTF8Encoding(false));
        }
    }
}