using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    public static class AllMimicAttackCheck
    {
        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== 全部章节 MimicStalker.enableAttack 检查 ===");

            string[] scenes = {
                "Assets/_Project/Scenes/Level_Chapter3_Graybox.unity",
                "Assets/_Project/Scenes/Level_Chapter4_Graybox.unity",
                "Assets/_Project/Scenes/Level_Chapter5_Graybox.unity",
            };

            foreach (var p in scenes)
            {
                EditorSceneManager.OpenScene(p, OpenSceneMode.Single);
                sb.AppendLine();
                sb.AppendLine($"--- {Path.GetFileNameWithoutExtension(p)} ---");
                var mimics = Object.FindObjectsByType<AbyssEcho.Horror.MimicStalker>(FindObjectsSortMode.None);
                sb.AppendLine($"  MimicStalker 总数 = {mimics.Length}");
                int attackOn = 0;
                foreach (var m in mimics)
                {
                    var so = new SerializedObject(m);
                    var p2 = so.FindProperty("enableAttack");
                    bool on = p2 != null && p2.boolValue;
                    if (on) attackOn++;
                    sb.AppendLine($"    {m.gameObject.name}: enableAttack = {on}");
                }
                sb.AppendLine($"  → 启用攻击的数量 = {attackOn} / {mimics.Length}");
            }

            // 第一章 MonsterAI 是否能攻击
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_Chapter1_Graybox.unity", OpenSceneMode.Single);
            sb.AppendLine();
            sb.AppendLine("--- Level_Chapter1_Graybox ---");
            foreach (var m in Object.FindObjectsByType<AbyssEcho.Monster.MonsterAI>(FindObjectsSortMode.None))
            {
                var so = new SerializedObject(m);
                sb.AppendLine($"  MonsterAI {m.gameObject.name}: enabled={m.enabled}");
                foreach (var f in new[] { "attackDistance", "chaseSpeed" })
                {
                    var p3 = so.FindProperty(f);
                    if (p3 != null) sb.AppendLine($"    {f} = {p3.floatValue}");
                }
            }

            string text = sb.ToString();
            Debug.Log(text);
            File.WriteAllText(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports/AllMimicAttack.txt")), text, new UTF8Encoding(false));
        }
    }
}