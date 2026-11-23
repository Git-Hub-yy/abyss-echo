using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    public static class WalkthroughData
    {
        public static void Run()
        {
            var sb = new StringBuilder();

            var chapters = new[] {
                AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter1,
                AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter2,
                AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter3,
                AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter4,
                AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter5,
            };

            foreach (var c in chapters)
            {
                AbyssEcho.LevelGen.ChapterLayout.Current = c;
                var rooms = AbyssEcho.LevelGen.ChapterLayout.GetRooms(c);

                sb.AppendLine($"===== Chapter{(int)c} {AbyssEcho.LevelGen.ChapterLayout.GetChapterName(c)} =====");
                sb.AppendLine($"房间数 = {rooms.Count}");
                sb.AppendLine($"起点 = {AbyssEcho.LevelGen.ChapterLayout.GetPlayerStart()}");
                sb.AppendLine($"终点 = {AbyssEcho.LevelGen.ChapterLayout.GetChapterEndPoint()}");
                sb.AppendLine($"场景 = {AbyssEcho.LevelGen.ChapterLayout.GetSceneName(c)}");
                sb.AppendLine();

                // 关键路径
                var path = AbyssEcho.LevelGen.ChapterLayoutMeta.GetCriticalPath();
                sb.AppendLine("关键路径: " + string.Join(" -> ", path));
                sb.AppendLine();

                sb.AppendLine("--- 房间清单 ---");
                foreach (var r in rooms)
                {
                    sb.AppendLine($"{r.Id,-16} {r.DisplayName,-16} x[{r.MinX,7:F1},{r.MaxX,7:F1}] z[{r.MinZ,7:F1},{r.MaxZ,7:F1}] 高={r.Height,4:F1} 门={r.Doorways.Count}");
                }
                sb.AppendLine();

                // 场景里的交互物
                string sceneName = AbyssEcho.LevelGen.ChapterLayout.GetSceneName(c);
                string scenePath = $"Assets/_Project/Scenes/{sceneName}.unity";
                if (File.Exists(scenePath))
                {
                    EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                    sb.AppendLine("--- 门 ---");
                    foreach (var d in Object.FindObjectsByType<AbyssEcho.Interaction.DoorInteractable>(FindObjectsSortMode.None))
                        sb.AppendLine($"  {d.gameObject.name,-22} pos={d.transform.position} locked={d.locked}");

                    sb.AppendLine("--- 拾取物 ---");
                    foreach (var p in Object.FindObjectsByType<AbyssEcho.Interaction.PickupItem>(FindObjectsSortMode.None))
                        sb.AppendLine($"  {p.gameObject.name,-22} {p.displayName,-10} id={p.itemId,-14} pos={p.transform.position}");

                    sb.AppendLine("--- 谜题/交互装置 ---");
                    foreach (var k in Object.FindObjectsByType<AbyssEcho.Puzzle.KeypadPanel>(FindObjectsSortMode.None))
                        sb.AppendLine($"  [键盘] {k.gameObject.name} pos={k.transform.position} code={k.correctCode}");
                    foreach (var w in Object.FindObjectsByType<AbyssEcho.Puzzle.PowerRestorePanel>(FindObjectsSortMode.None))
                        sb.AppendLine($"  [配电箱] {w.gameObject.name} pos={w.transform.position} 需={w.requiredItemId}");
                    foreach (var w in Object.FindObjectsByType<AbyssEcho.Puzzle.WaterPumpSwitch>(FindObjectsSortMode.None))
                        sb.AppendLine($"  [水泵] {w.gameObject.name} pos={w.transform.position}");
                    foreach (var t in Object.FindObjectsByType<AbyssEcho.Puzzle.TapeRecorder>(FindObjectsSortMode.None))
                        sb.AppendLine($"  [录音机] {t.gameObject.name} pos={t.transform.position}");
                    foreach (var f in Object.FindObjectsByType<AbyssEcho.Puzzle.FrequencyConsole>(FindObjectsSortMode.None))
                        sb.AppendLine($"  [频率台] {f.gameObject.name} pos={f.transform.position} freq={f.correctFrequency}");
                    foreach (var o in Object.FindObjectsByType<AbyssEcho.Puzzle.OfferingAltar>(FindObjectsSortMode.None))
                        sb.AppendLine($"  [供奉台] {o.gameObject.name} pos={o.transform.position} A={o.itemForEndingA} C={o.itemForEndingC}");
                    foreach (var p in Object.FindObjectsByType<AbyssEcho.Puzzle.PulseSyncPuzzle>(FindObjectsSortMode.None))
                        sb.AppendLine($"  [脉搏] {p.gameObject.name} pos={p.transform.position}");
                    foreach (var e in Object.FindObjectsByType<AbyssEcho.Puzzle.EvacuationConsole>(FindObjectsSortMode.None))
                        sb.AppendLine($"  [撤离台] {e.gameObject.name} pos={e.transform.position}");

                    sb.AppendLine("--- 可读笔记 ---");
                    foreach (var n in Object.FindObjectsByType<AbyssEcho.Narrative.ReadableNote>(FindObjectsSortMode.None))
                        sb.AppendLine($"  {n.gameObject.name} pos={n.transform.position}");

                    sb.AppendLine("--- 出口触发 ---");
                    var ending = Object.FindFirstObjectByType<AbyssEcho.Narrative.ChapterEndingSystem>();
                    if (ending != null)
                        sb.AppendLine($"  ChapterEndingSystem @ {ending.transform.position}");
                }
                sb.AppendLine();
                sb.AppendLine();
            }

            string text = sb.ToString();
            Debug.Log(text);
            File.WriteAllText(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports/WalkthroughData.txt")), text, new UTF8Encoding(false));
        }
    }
}