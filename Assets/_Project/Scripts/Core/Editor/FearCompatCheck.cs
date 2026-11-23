using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    public static class FearCompatCheck
    {
        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== FearSystem 兼容性检查 ===");

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

                var fears = Object.FindObjectsByType<AbyssEcho.Horror.FearSystem>(FindObjectsSortMode.None);
                sb.AppendLine($"  FearSystem = {fears.Length}");
                foreach (var f in fears)
                {
                    sb.AppendLine($"    @ {f.gameObject.name}");
                    sb.AppendLine($"      currentFear={f.currentFear} recovery={f.recoveryRate} dark={f.darkRiseRate}");
                    sb.AppendLine($"      tense={f.tenseThreshold} fear={f.fearThreshold} panic={f.panicThreshold}");
                    sb.AppendLine($"      input={(f.input != null ? f.input.name : "NULL")} flashlight={(f.flashlight != null ? f.flashlight.name : "NULL")}");
                }

                // HorrorTrigger 的 maxFearToTrigger 检查
                var trigs = Object.FindObjectsByType<AbyssEcho.Horror.HorrorTrigger>(FindObjectsSortMode.None);
                sb.AppendLine($"  HorrorTrigger = {trigs.Length}");
                foreach (var t in trigs)
                {
                    var so = new SerializedObject(t);
                    var pMax = so.FindProperty("maxFearToTrigger");
                    var pJump = so.FindProperty("fearJump");
                    var pOne = so.FindProperty("oneShot");
                    sb.AppendLine($"    {t.gameObject.name}: maxFearToTrigger={(pMax != null ? pMax.floatValue.ToString("F2") : "?")} " +
                                  $"fearJump={(pJump != null ? pJump.floatValue.ToString("F2") : "?")} " +
                                  $"oneShot={(pOne != null ? pOne.boolValue.ToString() : "?")}");
                }

                // FlickerLight 的 fearReactive
                int fearReactive = 0;
                foreach (var fl in Object.FindObjectsByType<AbyssEcho.Horror.FlickerLight>(FindObjectsSortMode.None))
                {
                    var so = new SerializedObject(fl);
                    var pr = so.FindProperty("fearReactive");
                    if (pr != null && pr.boolValue) fearReactive++;
                }
                sb.AppendLine($"  FlickerLight fearReactive 数量 = {fearReactive}");

                // 玩家组件
                var motor = Object.FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
                if (motor != null)
                {
                    var names = new StringBuilder();
                    foreach (var c in motor.GetComponents<Component>())
                        if (c != null) names.Append(c.GetType().Name + ", ");
                    sb.AppendLine($"  Player 组件 = {names}");
                }
            }

            string text = sb.ToString();
            Debug.Log(text);
            File.WriteAllText(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports/FearCompatCheck.txt")), text, new UTF8Encoding(false));
        }
    }
}