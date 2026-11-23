using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    public static class MissingSystemsCheck
    {
        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== 关键系统存在性核实 ===");

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
                sb.AppendLine($"--- {Path.GetFileName(p)} ---");

                sb.AppendLine($"  FearSystem: {Object.FindObjectsByType<AbyssEcho.Horror.FearSystem>(FindObjectsSortMode.None).Length}");
                sb.AppendLine($"  FearVisuals: {Object.FindObjectsByType<AbyssEcho.Horror.FearVisuals>(FindObjectsSortMode.None).Length}");
                sb.AppendLine($"  PlayerDeathSystem: {Object.FindObjectsByType<AbyssEcho.Horror.PlayerDeathSystem>(FindObjectsSortMode.None).Length}");
                sb.AppendLine($"  Volume: {Object.FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsSortMode.None).Length}");
                sb.AppendLine($"  FlickerLight: {Object.FindObjectsByType<AbyssEcho.Horror.FlickerLight>(FindObjectsSortMode.None).Length}");
                sb.AppendLine($"  PlayerFootsteps: {Object.FindObjectsByType<AbyssEcho.Horror.PlayerFootsteps>(FindObjectsSortMode.None).Length}");

                // 手电筒细节
                var fl = Object.FindFirstObjectByType<AbyssEcho.Tools.FlashlightSystem>();
                if (fl != null)
                {
                    var t = fl.GetType();
                    var slF = t.GetField("spotLight");
                    var mrF = t.GetField("modelRoot");
                    sb.AppendLine($"  FlashlightSystem: 存在 @ {fl.gameObject.name}");
                    sb.AppendLine($"    spotLight = {(slF?.GetValue(fl) != null ? "有" : "❌空")}");
                    sb.AppendLine($"    modelRoot = {(mrF?.GetValue(fl) != null ? "有" : "❌空")}");
                    // 列出所有子物体里有没有 Light
                    var lights = fl.GetComponentsInChildren<Light>(true);
                    sb.AppendLine($"    子物体 Light 数量 = {lights.Length}");
                    foreach (var l in lights) sb.AppendLine($"      - {l.gameObject.name}");
                }
                else
                {
                    sb.AppendLine("  FlashlightSystem: ❌ 不存在");
                }
            }

            string text = sb.ToString();
            Debug.Log(text);
            File.WriteAllText(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports/MissingSystemsCheck.txt")), text, new UTF8Encoding(false));
        }
    }
}