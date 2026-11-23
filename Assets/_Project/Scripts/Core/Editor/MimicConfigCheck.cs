using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    public static class MimicConfigCheck
    {
        public static void Run()
        {
            var sb = new StringBuilder();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_Chapter3_Graybox.unity", OpenSceneMode.Single);
            sb.AppendLine("=== 第三章 MimicStalker 配置 ===");

            foreach (var m in Object.FindObjectsByType<AbyssEcho.Horror.MimicStalker>(FindObjectsSortMode.None))
            {
                var so = new SerializedObject(m);
                sb.AppendLine();
                sb.AppendLine($"--- {m.gameObject.name} @ {m.transform.position} ---");
                foreach (var f in new[] { "enableAttack", "attackDistance", "vanishDistance",
                                           "approachStartDistance", "approachSpeed",
                                           "respawnDelay", "respawnMinDist", "respawnMaxDist",
                                           "sightAngle", "sightRange", "maxVisibleTime" })
                {
                    var p = so.FindProperty(f);
                    if (p == null) { sb.AppendLine($"  {f} = <无>"); continue; }
                    sb.AppendLine($"  {f} = {p.floatValue}");
                }
            }

            string text = sb.ToString();
            Debug.Log(text);
            File.WriteAllText(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports/MimicConfig.txt")), text, new UTF8Encoding(false));
        }
    }
}