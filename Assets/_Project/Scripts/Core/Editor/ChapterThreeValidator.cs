using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>S31：校验第三章 F 区布局（重叠 + 可达性）。</summary>
    public static class ChapterThreeValidator
    {
        [MenuItem("ABYSS ECHO/校验第三章布局")]
        public static void Validate()
        {
            var rooms = AbyssEcho.LevelGen.ChapterLayout.GetRooms(
                AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter5);

            var sb = new StringBuilder();
            sb.AppendLine("===== 第五章 H 区布局校验 =====");
            sb.AppendLine($"房间数：{rooms.Count}");
            sb.AppendLine();

            // ① 重叠检查
            int overlaps = 0;
            for (int i = 0; i < rooms.Count; i++)
            {
                for (int j = i + 1; j < rooms.Count; j++)
                {
                    var a = rooms[i];
                    var b = rooms[j];
                    bool ox = a.MinX < b.MaxX && b.MinX < a.MaxX;
                    bool oz = a.MinZ < b.MaxZ && b.MinZ < a.MaxZ;
                    if (ox && oz)
                    {
                        sb.AppendLine($"❌ 重叠：{a.Id} 与 {b.Id}");
                        sb.AppendLine($"   {a.Id}: x[{a.MinX:F1},{a.MaxX:F1}] z[{a.MinZ:F1},{a.MaxZ:F1}]");
                        sb.AppendLine($"   {b.Id}: x[{b.MinX:F1},{b.MaxX:F1}] z[{b.MinZ:F1},{b.MaxZ:F1}]");
                        overlaps++;
                    }
                }
            }
            if (overlaps == 0) sb.AppendLine("✅ 无重叠");

            // ② 门洞对接检查
            sb.AppendLine();
            sb.AppendLine("--- 门洞对接 ---");
            int noMatch = 0;
            foreach (var r in rooms)
            {
                foreach (var d in r.Doorways)
                {
                    bool found = false;
                    foreach (var other in rooms)
                    {
                        if (other.Id == r.Id) continue;

                        // 检查两房间是否贴合且另一侧也有门
                        float dx = 0, dz = 0;
                        if (d.Side == AbyssEcho.LevelGen.WallSide.North) { dz = other.MinZ - r.MaxZ; }
                        else if (d.Side == AbyssEcho.LevelGen.WallSide.South) { dz = other.MaxZ - r.MinZ; }
                        else if (d.Side == AbyssEcho.LevelGen.WallSide.East) { dx = other.MinX - r.MaxX; }
                        else { dx = other.MaxX - r.MinX; }

                        if (Mathf.Abs(dx) < 0.15f && Mathf.Abs(dz) < 0.15f)
                        {
                            // 贴合了，看对方有没有对应门
                            var opposite = d.Side == AbyssEcho.LevelGen.WallSide.North ? AbyssEcho.LevelGen.WallSide.South
                                         : d.Side == AbyssEcho.LevelGen.WallSide.South ? AbyssEcho.LevelGen.WallSide.North
                                         : d.Side == AbyssEcho.LevelGen.WallSide.East ? AbyssEcho.LevelGen.WallSide.West
                                         : AbyssEcho.LevelGen.WallSide.East;

                            foreach (var od in other.Doorways)
                            {
                                if (od.Side == opposite && Mathf.Abs(od.Offset - d.Offset) < 0.6f)
                                {
                                    found = true;
                                    break;
                                }
                            }
                            if (found) break;
                        }
                    }

                    if (!found)
                    {
                        sb.AppendLine($"⚠️ {r.Id} 的 {d.Side} 门找不到对接（偏移 {d.Offset:F1}）");
                        noMatch++;
                    }
                }
            }
            if (noMatch == 0) sb.AppendLine("✅ 所有门洞都有对接");

            // ③ 连通性（BFS）
            sb.AppendLine();
            sb.AppendLine("--- 连通性 ---");
            var connections = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>();
            foreach (var r in rooms)
                connections[r.Id] = new System.Collections.Generic.List<string>();

            foreach (var r in rooms)
            {
                foreach (var d in r.Doorways)
                {
                    foreach (var other in rooms)
                    {
                        if (other.Id == r.Id) continue;

                        float dx = 0, dz = 0;
                        if (d.Side == AbyssEcho.LevelGen.WallSide.North) { dz = other.MinZ - r.MaxZ; }
                        else if (d.Side == AbyssEcho.LevelGen.WallSide.South) { dz = other.MaxZ - r.MinZ; }
                        else if (d.Side == AbyssEcho.LevelGen.WallSide.East) { dx = other.MinX - r.MaxX; }
                        else { dx = other.MaxX - r.MinX; }

                        if (Mathf.Abs(dx) < 0.15f && Mathf.Abs(dz) < 0.15f)
                        {
                            var opposite = d.Side == AbyssEcho.LevelGen.WallSide.North ? AbyssEcho.LevelGen.WallSide.South
                                         : d.Side == AbyssEcho.LevelGen.WallSide.South ? AbyssEcho.LevelGen.WallSide.North
                                         : d.Side == AbyssEcho.LevelGen.WallSide.East ? AbyssEcho.LevelGen.WallSide.West
                                         : AbyssEcho.LevelGen.WallSide.East;

                            foreach (var od in other.Doorways)
                            {
                                if (od.Side == opposite && Mathf.Abs(od.Offset - d.Offset) < 0.6f)
                                {
                                    if (!connections[r.Id].Contains(other.Id)) connections[r.Id].Add(other.Id);
                                    if (!connections[other.Id].Contains(r.Id)) connections[other.Id].Add(r.Id);
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            string startId = "H1";
            var visited = new System.Collections.Generic.HashSet<string> { startId };
            var queue = new System.Collections.Generic.Queue<string>();
            queue.Enqueue(startId);
            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                if (!connections.TryGetValue(cur, out var nb)) continue;
                foreach (var n in nb)
                    if (visited.Add(n)) queue.Enqueue(n);
            }

            var unreachable = new System.Collections.Generic.List<string>();
            foreach (var r in rooms)
                if (!visited.Contains(r.Id)) unreachable.Add(r.Id);

            if (unreachable.Count == 0)
                sb.AppendLine($"✅ 从 {startId} 可达全部 {rooms.Count} 个房间");
            else
                sb.AppendLine($"❌ 从 {startId} 无法到达：{string.Join(", ", unreachable)}");

            // ④ 关键房间检查
            sb.AppendLine();
            sb.AppendLine("--- 关键房间 ---");
            string[] critical = { "H1", "H2", "H3", "H4", "H5", "H6", "H7" };
            foreach (var id in critical)
            {
                var r = rooms.Find(x => x.Id == id);
                sb.AppendLine($"  {(r != null ? "✅" : "❌")} {id} {(r != null ? r.DisplayName : "缺失")}");
            }

            // ⑤ 房间清单
            sb.AppendLine();
            sb.AppendLine("--- 房间清单 ---");
            foreach (var r in rooms)
                sb.AppendLine($"  {r.Id,-12} {r.DisplayName,-10} x[{r.MinX,6:F1},{r.MaxX,6:F1}] z[{r.MinZ,6:F1},{r.MaxZ,6:F1}] 高={r.Height:F1} 门={r.Doorways.Count}");

            string text = sb.ToString();
            Debug.Log("<b>[Ch3Validate]</b>\n" + text);

            try
            {
                string dir = Path.GetFullPath(Path.Combine(
                    Path.GetDirectoryName(Application.dataPath), "../Reports"));
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "ChapterFiveValidation.txt"), text, new UTF8Encoding(false));
            }
            catch { }
        }
    }
}