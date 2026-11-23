using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    public static class MonsterInventory
    {
        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("══════ 各章怪物组件清单 ══════");

            for (int n = 1; n <= 5; n++)
            {
                string p = $"Assets/_Project/Scenes/Level_Chapter{n}_Graybox.unity";
                if (!File.Exists(p)) continue;
                EditorSceneManager.OpenScene(p, OpenSceneMode.Single);

                sb.AppendLine();
                sb.AppendLine($"### Chapter{n}");

                var mimics = Object.FindObjectsByType<AbyssEcho.Horror.MimicStalker>(FindObjectsSortMode.None);
                var monsters = Object.FindObjectsByType<AbyssEcho.Monster.MonsterAI>(FindObjectsSortMode.None);
                var deaths = Object.FindObjectsByType<AbyssEcho.Horror.PlayerDeathSystem>(FindObjectsSortMode.None);

                sb.AppendLine($"  MimicStalker = {mimics.Length}");
                foreach (var m in mimics)
                {
                    var so = new SerializedObject(m);
                    sb.AppendLine($"    {m.gameObject.name} @ {m.transform.position} " +
                                  $"enableAttack={so.FindProperty("enableAttack")?.boolValue} " +
                                  $"attackDistance={so.FindProperty("attackDistance")?.floatValue}");
                }

                sb.AppendLine($"  MonsterAI = {monsters.Length}");
                foreach (var m in monsters)
                    sb.AppendLine($"    {m.gameObject.name} @ {m.transform.position}");

                sb.AppendLine($"  PlayerDeathSystem = {deaths.Length}");

                // 搜索所有带 "Monster"/"Enemy"/"Stalker"/"Mimic" 字样的 GameObject
                sb.AppendLine("  名称含怪物关键词的对象：");
                foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                {
                    string nm = t.name;
                    if (nm.Contains("Monster") || nm.Contains("Enemy") || nm.Contains("Mimic") || nm.Contains("Stalker"))
                    {
                        var comps = new List<string>();
                        foreach (var c in t.GetComponents<Component>()) if (c != null) comps.Add(c.GetType().Name);
                        sb.AppendLine($"    {nm} @ {t.position}  [{string.Join(", ", comps)}]");
                    }
                }
            }

            string text = sb.ToString();
            Debug.Log(text);
            File.WriteAllText(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports/MonsterInventory.txt")), text, new UTF8Encoding(false));
        }
    }
}