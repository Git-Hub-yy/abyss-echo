using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    public static class FlashlightSceneCheck
    {
        private static object GetField(object o, string name)
        {
            var f = o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            return f?.GetValue(o);
        }

        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== 各章手电筒场景配置 ===");

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

                var fl = Object.FindFirstObjectByType<AbyssEcho.Tools.FlashlightSystem>();
                if (fl == null) { sb.AppendLine("  ❌ 无 FlashlightSystem"); continue; }

                sb.AppendLine($"  FlashlightSystem @ {fl.gameObject.name}");
                sb.AppendLine($"    normalIntensity = {GetField(fl, "normalIntensity")}");
                sb.AppendLine($"    spotAngle       = {GetField(fl, "spotAngle")}");
                sb.AppendLine($"    range           = {GetField(fl, "range")}");
                sb.AppendLine($"    batteryPercent  = {GetField(fl, "batteryPercent")}");
                sb.AppendLine($"    turnOnDelay     = {GetField(fl, "turnOnDelay")}");
                sb.AppendLine($"    isOn(序列化)    = {GetField(fl, "isOn")}");
                sb.AppendLine($"    spotLight(序列化) = {(GetField(fl, "spotLight") != null ? "有" : "NULL(运行时自动查找)")}");

                var lights = fl.GetComponentsInChildren<Light>(true);
                sb.AppendLine($"    场景内子物体 Light = {lights.Length}（程序化生成→运行时才有）");

                sb.AppendLine($"    ambientLight = {RenderSettings.ambientLight}");

                foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                    sb.AppendLine($"    相机 {c.gameObject.name} cullingMask={c.cullingMask}");
            }

            string text = sb.ToString();
            Debug.Log(text);
            File.WriteAllText(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports/FlashlightSceneCheck.txt")), text, new UTF8Encoding(false));
        }
    }
}