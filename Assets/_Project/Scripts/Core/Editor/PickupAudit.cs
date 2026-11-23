using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace AbyssEcho.Core.EditorTools {
    public static class PickupAudit {
        public static void Run() {
            var sb = new StringBuilder();
            sb.AppendLine("=== 五章 PickupItem 审计 ===");
            sb.AppendLine();

            // ItemIds 已知 ID
            var known = new HashSet<string> { "item.fuse", "item.accesscard", "item.note", "item.tape", "item.bulb", "item.glove" };

            var scenes = new (int ch, string p)[] {
                (1,"Assets/_Project/Scenes/Level_Chapter1_Graybox.unity"),
                (2,"Assets/_Project/Scenes/Level_Chapter2_Graybox.unity"),
                (3,"Assets/_Project/Scenes/Level_Chapter3_Graybox.unity"),
                (4,"Assets/_Project/Scenes/Level_Chapter4_Graybox.unity"),
                (5,"Assets/_Project/Scenes/Level_Chapter5_Graybox.unity"),
            };

            var allIds = new Dictionary<string, List<string>>();

            foreach (var (ch, p) in scenes) {
                if (!File.Exists(p)) continue;
                EditorSceneManager.OpenScene(p, OpenSceneMode.Single);
                Physics.SyncTransforms();

                var picks = Object.FindObjectsByType<AbyssEcho.Interaction.PickupItem>(FindObjectsSortMode.None);
                sb.AppendLine($"──── 第{ch}章：{picks.Length} 个 PickupItem ────");
                sb.AppendLine($"  {"对象名",-28} {"itemId",-22} {"displayName",-16} {"amount",-8} {"interactable",-14} 状态");

                foreach (var pi in picks) {
                    if (pi == null) continue;
                    var t = typeof(AbyssEcho.Interaction.PickupItem);
                    var BF = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
                    string id = t.GetField("itemId", BF)?.GetValue(pi) as string ?? "?";
                    string dn = t.GetField("displayName", BF)?.GetValue(pi) as string ?? "?";
                    int amt = (int)(t.GetField("amount", BF)?.GetValue(pi) ?? 1);
                    var interF = typeof(AbyssEcho.Interaction.InteractableBase).GetField("interactable", BF);
                    bool inter = interF != null ? (bool)interF.GetValue(pi) : true;

                    bool isKnown = known.Contains(id);
                    string status = isKnown ? "✅" : "❌ 未在ItemIds中";
                    if (!isKnown) status += " → 物品栏显示为原始ID";

                    sb.AppendLine($"  {pi.gameObject.name,-28} {id,-22} {dn,-16} {amt,-8} {inter,-14} {status}");

                    if (!allIds.ContainsKey(id)) allIds[id] = new List<string>();
                    allIds[id].Add($"Ch{ch}/{pi.gameObject.name}");
                }
                sb.AppendLine();
            }

            sb.AppendLine("══════ 所有 itemId 汇总 ══════");
            foreach (var kv in allIds) {
                bool isKnown = known.Contains(kv.Key);
                sb.AppendLine($"  {(isKnown ? "✅" : "❌"),-4} {kv.Key,-24} 出现在 {kv.Value.Count} 处：" +
                              string.Join(", ", kv.Value.GetRange(0, Mathf.Min(3, kv.Value.Count))) +
                              (kv.Value.Count > 3 ? " ..." : ""));
            }

            sb.AppendLine();
            sb.AppendLine("══════ 结论 ══════");
            int unknown = 0;
            foreach (var kv in allIds) if (!known.Contains(kv.Key)) unknown++;
            sb.AppendLine($"  ItemIds 定义的 ID 数 = {known.Count}");
            sb.AppendLine($"  场景实际使用的 ID 数 = {allIds.Count}");
            sb.AppendLine($"  未在 ItemIds 中的 ID = {unknown} 个");
            if (unknown > 0) sb.AppendLine("  ⚠️ 这些物品在物品栏里会显示成原始 ID，且没有描述/颜色");

            Debug.Log(sb.ToString());
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\PickupAudit.txt", sb.ToString(), new UTF8Encoding(false));
        }
    }
}