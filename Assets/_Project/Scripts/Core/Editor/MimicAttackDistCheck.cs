using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    public static class MimicAttackDistCheck
    {
        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("══════ 各章 Mimic 攻击距离 ══════");

            for (int n = 3; n <= 5; n++)
            {
                EditorSceneManager.OpenScene($"Assets/_Project/Scenes/Level_Chapter{n}_Graybox.unity", OpenSceneMode.Single);
                sb.AppendLine();
                sb.AppendLine($"### Chapter{n}");

                foreach (var m in Object.FindObjectsByType<AbyssEcho.Horror.MimicStalker>(FindObjectsSortMode.None))
                {
                    var so = new SerializedObject(m);
                    sb.AppendLine($"  {m.gameObject.name}");
                    sb.AppendLine($"    attackDistance     = {so.FindProperty("attackDistance")?.floatValue}");
                    sb.AppendLine($"    vanishDistance     = {so.FindProperty("vanishDistance")?.floatValue}");
                    sb.AppendLine($"    approachStartDist  = {so.FindProperty("approachStartDistance")?.floatValue}");
                    sb.AppendLine($"    approachSpeed      = {so.FindProperty("approachSpeed")?.floatValue}");
                    sb.AppendLine($"    enableAttack       = {so.FindProperty("enableAttack")?.boolValue}");
                    sb.AppendLine($"    位置 = {m.transform.position}");
                }
            }

            string text = sb.ToString();
            Debug.Log(text);
            File.WriteAllText(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports/MimicAttackDist.txt")), text, new UTF8Encoding(false));
        }
    }
}