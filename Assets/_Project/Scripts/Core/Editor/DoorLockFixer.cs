using System.Collections.Generic;
using AbyssEcho.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S18 门锁修复：让"谜题门"真的必须靠谜题解锁。
    ///
    /// 修复的问题（玩家实测发现）：
    ///   DoorInteractable.OnInteract 里：
    ///       if (locked) {
    ///           if (requiredItemId 非空) { 检查背包 }
    ///           locked = false;          // ← 没配道具就直接解锁
    ///       }
    ///   而 CreateDoor 从来没设 requiredItemId，
    ///   所以对着 Door_A3_B 按 E 就能开，不需要保险丝，配电箱谜题形同虚设。
    /// </summary>
    public static class DoorLockFixer
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity";

        [MenuItem("ABYSS ECHO/S18 修复门锁逻辑")]
        public static void Fix()
        {
            // ⚠️ 必须先打开场景，否则 FindObjectsByType 找不到任何东西
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== S18 门锁修复 ===");

            // 需要"谜题解锁"的门 → 对应的提示文案
            var puzzlePrompts = new Dictionary<string, string>
            {
                { "Door_A3_B", "门锁着 —— 需要先恢复电力（去 A3 接待室找配电箱）" },
                { "Door_C2", "门禁锁着 —— 需要门禁密码" },
                { "Door_D_Gate", "压力闸门锁死 —— 需要解除频率锁定" },
                { "Door_E4", "需要解除频率锁定" },
            };

            var doors = Object.FindObjectsByType<DoorInteractable>(FindObjectsSortMode.None);
            sb.AppendLine($"场景中共 {doors.Length} 扇门");

            int fixedCount = 0;
            foreach (var door in doors)
            {
                if (!puzzlePrompts.TryGetValue(door.name, out var prompt))
                {
                    sb.AppendLine($"  · {door.name}：不是谜题门，跳过");
                    continue;
                }

                door.locked = true;
                door.lockedPrompt = prompt;
                door.requiredItemId = "";
                door.unlockableByInteraction = false;   // 关键：按 E 不能解锁

                EditorUtility.SetDirty(door);
                fixedCount++;
                sb.AppendLine($"  ✅ {door.name}：已设为必须解谜");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            sb.AppendLine();
            sb.AppendLine($"共修复 {fixedCount} 扇门");

            string text = sb.ToString();
            Debug.Log("<b>[DoorLockFix]</b>\n" + text);

            try
            {
                string dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(Application.dataPath), "../Reports"));
                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "DoorLockFix.txt"),
                    text, new System.Text.UTF8Encoding(false));
            }
            catch { }
        }
    }
}