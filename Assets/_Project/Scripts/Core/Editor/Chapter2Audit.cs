using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// 第二章内容审计（只读）：列出所有交互物、门、谜题的状态。
    /// </summary>
    public static class Chapter2Audit
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter2_Graybox.unity";

        [MenuItem("ABYSS ECHO/审计/第二章内容审计（只读）")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var sb = new StringBuilder();
            sb.AppendLine("========== 第二章内容审计 ==========");
            sb.AppendLine();

            // 交互物
            sb.AppendLine("=== 交互物（InteractableBase）===");
            var list = Object.FindObjectsByType<AbyssEcho.Interaction.InteractableBase>(FindObjectsSortMode.None);
            sb.AppendLine($"共 {list.Length} 个");
            foreach (var b in list)
            {
                if (b == null) continue;
                var col = b.GetComponent<Collider>();
                var childCol = b.GetComponentInChildren<Collider>(true);
                sb.AppendLine($"  {b.gameObject.name} [{b.GetType().Name}]");
                sb.AppendLine($"    位置={b.transform.position}  CanInteract={b.CanInteract}");
                sb.AppendLine($"    根碰撞体={(col != null ? "有" : "无")} 子碰撞体={(childCol != null ? "有" : "无")}");
            }
            sb.AppendLine();

            // 门
            sb.AppendLine("=== 门 ===");
            var doors = Object.FindObjectsByType<AbyssEcho.Interaction.DoorInteractable>(FindObjectsSortMode.None);
            foreach (var d in doors)
            {
                sb.AppendLine($"  {d.name}: locked={d.locked} unlockable={d.unlockableByInteraction}");
                sb.AppendLine($"    位置={d.transform.position} 尺寸={d.transform.localScale}");
                sb.AppendLine($"    提示=\"{d.lockedPrompt}\"");
            }
            sb.AppendLine();

            // 谜题
            sb.AppendLine("=== 谜题设备 ===");
            var pumps = Object.FindObjectsByType<AbyssEcho.Puzzle.WaterPumpSwitch>(FindObjectsSortMode.None);
            sb.AppendLine($"抽水泵: {pumps.Length} 个");
            foreach (var p in pumps)
            {
                sb.AppendLine($"  {p.name}: 位置={p.transform.position} IsDrained={p.IsDrained}");
                sb.AppendLine($"    doorsToUnlock={(p.doorsToUnlock != null ? p.doorsToUnlock.Length + " 扇" : "null")}");
                if (p.doorsToUnlock != null)
                    foreach (var d in p.doorsToUnlock)
                        sb.AppendLine($"      → {(d != null ? d.name : "NULL")}");
            }

            var freqs = Object.FindObjectsByType<AbyssEcho.Puzzle.FrequencyConsole>(FindObjectsSortMode.None);
            sb.AppendLine($"频率终端: {freqs.Length} 个");
            foreach (var f in freqs)
            {
                sb.AppendLine($"  {f.name}: 位置={f.transform.position} IsUnlocked={f.IsUnlocked}");
                sb.AppendLine($"    正确频率={f.correctFrequency} 范围={f.minFrequency}~{f.maxFrequency}");
                sb.AppendLine($"    doorsToUnlock={(f.doorsToUnlock != null ? f.doorsToUnlock.Length + " 扇" : "null")}");
                if (f.doorsToUnlock != null)
                    foreach (var d in f.doorsToUnlock)
                        sb.AppendLine($"      → {(d != null ? d.name : "NULL")}");
            }

            var tapes = Object.FindObjectsByType<AbyssEcho.Puzzle.TapeRecorder>(FindObjectsSortMode.None);
            sb.AppendLine($"录音机: {tapes.Length} 个");
            foreach (var t in tapes)
                sb.AppendLine($"  {t.gameObject.name}: tapeId={t.tapeId} 标题=\"{t.tapeTitle}\" 位置={t.transform.position}");

            var notes = Object.FindObjectsByType<AbyssEcho.Narrative.ReadableNote>(FindObjectsSortMode.None);
            sb.AppendLine($"便条: {notes.Length} 个");
            foreach (var n in notes)
                sb.AppendLine($"  {n.gameObject.name}: \"{n.title}\" 位置={n.transform.position}");

            var picks = Object.FindObjectsByType<AbyssEcho.Interaction.PickupItem>(FindObjectsSortMode.None);
            sb.AppendLine($"拾取物: {picks.Length} 个");
            foreach (var p in picks)
                sb.AppendLine($"  {p.name}: {p.itemId} 位置={p.transform.position}");
            sb.AppendLine();

            // 深水
            sb.AppendLine("=== 深水 ===");
            var waters = Object.FindObjectsByType<AbyssEcho.Horror.DeepWaterZone>(FindObjectsSortMode.None);
            sb.AppendLine($"深水区: {waters.Length} 个");
            foreach (var w in waters)
                sb.AppendLine($"  {w.name}: 位置={w.transform.position}");
            sb.AppendLine();

            // 怪物
            sb.AppendLine("=== 怪物 ===");
            var monsters = Object.FindObjectsByType<AbyssEcho.Monster.MonsterAI>(FindObjectsSortMode.None);
            sb.AppendLine($"怪物: {monsters.Length} 个");
            foreach (var m in monsters)
                sb.AppendLine($"  {m.name}: 位置={m.transform.position}");
            sb.AppendLine();

            // 灯
            sb.AppendLine("=== 灯光统计 ===");
            var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            int on = 0;
            foreach (var l in lights) if (l.enabled) on++;
            sb.AppendLine($"共 {lights.Length} 盏，亮 {on} 盏");

            // 房间
            var rooms = AbyssEcho.LevelGen.ChapterLayout.GetRooms(AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter2);
            sb.AppendLine();
            sb.AppendLine($"=== 房间（{rooms.Count} 个）===");
            foreach (var r in rooms)
                sb.AppendLine($"  {r.Id} {r.DisplayName}: x[{r.MinX:F1},{r.MaxX:F1}] z[{r.MinZ:F1},{r.MaxZ:F1}] 层高={r.Height:F1}");

            string text = sb.ToString();
            Debug.Log("<b>[Ch2Audit]</b>\n" + text);

            try
            {
                string dir = Path.GetFullPath(Path.Combine(
                    Path.GetDirectoryName(Application.dataPath), "../Reports"));
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "Chapter2Audit.txt"), text, new UTF8Encoding(false));
            }
            catch { }
        }
    }
}