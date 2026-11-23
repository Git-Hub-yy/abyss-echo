using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    public static class FlashlightSerializedCheck
    {
        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== FlashlightSystem 序列化字段值 ===");

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
                var fl = Object.FindFirstObjectByType<AbyssEcho.Tools.FlashlightSystem>();
                sb.AppendLine();
                sb.AppendLine($"--- {Path.GetFileName(p)} ---");
                if (fl == null) { sb.AppendLine("  ❌ 无 FlashlightSystem"); continue; }

                var so = new SerializedObject(fl);
                string[] fields = { "spotLight", "input", "modelRoot", "batteryPercent",
                                    "batteryLifetimeSeconds", "normalIntensity", "spotAngle",
                                    "range", "turnOnDelay", "isOn", "hasBatteryInstalled" };
                foreach (var f in fields)
                {
                    var pr = so.FindProperty(f);
                    if (pr == null) { sb.AppendLine($"  {f} = <字段不存在>"); continue; }
                    string val = pr.propertyType == SerializedPropertyType.ObjectReference
                        ? (pr.objectReferenceValue != null ? pr.objectReferenceValue.name : "NULL")
                        : pr.ToString();
                    sb.AppendLine($"  {f} = {val}");
                }
            }

            string text = sb.ToString();
            Debug.Log(text);
            File.WriteAllText(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports/FlashlightSerialized.txt")), text, new UTF8Encoding(false));
        }
    }
}