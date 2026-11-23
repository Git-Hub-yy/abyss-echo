using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    public static class FootstepsInspect
    {
        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== PlayerFootsteps 检查 ===");

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

                var fps = Object.FindObjectsByType<AbyssEcho.Horror.PlayerFootsteps>(FindObjectsSortMode.None);
                sb.AppendLine($"  PlayerFootsteps = {fps.Length}");
                foreach (var f in fps)
                {
                    var so = new SerializedObject(f);
                    sb.AppendLine($"    @ {GetPath(f.transform)}  active={f.gameObject.activeInHierarchy} enabled={f.enabled}");
                    sb.AppendLine($"      motor={(f.motor != null ? f.motor.gameObject.name : "NULL")}");
                    sb.AppendLine($"      walkInterval={f.walkInterval} runInterval={f.runInterval} crouchInterval={f.crouchInterval}");
                    sb.AppendLine($"      onMetalSurface={f.onMetalSurface} autoDetectSurface={f.autoDetectSurface} surfaceLayers={f.surfaceLayers.value}");
                }

                // 其他可能的脚步声实现
                sb.AppendLine($"    [] 其他含 Footstep/Step 的组件：");
                int others = 0;
                foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                {
                    if (mb == null) continue;
                    string tn = mb.GetType().Name;
                    if (tn.Contains("Step") && !(mb is AbyssEcho.Horror.PlayerFootsteps))
                    { sb.AppendLine($"      ⚠️ {tn} @ {GetPath(mb.transform)}"); others++; }
                }
                if (others == 0) sb.AppendLine("      无");

                // AudioManager
                var am = Object.FindFirstObjectByType<AbyssEcho.Audio.AudioManager>();
                sb.AppendLine($"  AudioManager = {(am != null ? am.gameObject.name : "❌ 无")}");
                if (am != null)
                {
                    sb.AppendLine($"    masterVolume={am.masterVolume} sfxVolume={am.sfxVolume}");
                    var srcs = am.GetComponentsInChildren<AudioSource>(true);
                    sb.AppendLine($"    子物体 AudioSource 数量 = {srcs.Length}");
                }

                // Player 组件
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
            File.WriteAllText(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports/FootstepsInspect.txt")), text, new UTF8Encoding(false));
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