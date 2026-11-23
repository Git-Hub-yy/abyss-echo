using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// 校验章节布局：重叠、联通性、门高度。
    ///
    /// 与 LayoutValidator 的区别：
    ///   LayoutValidator 校验几何约束（门高、门楣厚度等）
    ///   本工具校验**章节内部的一致性**（房间是否重叠、是否全部可达）
    ///
    /// 联通判定（正确版）：
    ///   两个房间 A、B 相连，必须同时满足：
    ///   ① 墙面贴合（间距 &lt; 0.5 米）
    ///   ② 沿墙方向有重叠
    ///   ③ **A 在贴合的那面墙上有门，且 B 也有门**
    ///
    ///   之前版本只看①②，导致连接廊（只有单侧门）被误判。
    /// </summary>
    public static class ChapterLayoutChecker
    {
        // ────────────────────────── 第二章 ──────────────────────────

        [MenuItem("ABYSS ECHO/S11 校验第二章布局")]
        public static void ValidateChapter2()
        {
            Run("第二章「回声」", AbyssEcho.LevelGen.ChapterTwoLayout.Build(),
                "S11_Chapter2LayoutValidation.txt", "D1");
        }

        // ────────────────────────── 第一章（回归测试）──────────────────────────

        [MenuItem("ABYSS ECHO/校验第一章布局（回归）")]
        public static void ValidateChapter1()
        {
            Run("第一章「深潜」", AbyssEcho.LevelGen.ChapterOneLayout.Build(),
                "S11_Chapter1LayoutValidation.txt", "A0");
        }

        // ────────────────────────── 核心逻辑 ──────────────────────────

        public static void Run(string chapterName, List<AbyssEcho.LevelGen.RoomDef> rooms,
                               string reportFile, string startRoomId)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"ABYSS ECHO · {chapterName} 布局校验");
            sb.AppendLine("=====================================");
            sb.AppendLine($"房间总数：{rooms.Count}");
            sb.AppendLine();

            foreach (var r in rooms)
            {
                sb.AppendLine($"  · {r.Id,-16} {r.DisplayName,-12} " +
                              $"中心=({r.Center.x,7:F1},{r.Center.z,7:F1}) " +
                              $"尺寸={r.SizeX:F1}×{r.SizeZ:F1} 层高={r.Height:F1} 门={r.Doorways.Count}");
            }
            sb.AppendLine();

            // ── ① 重叠 ──
            sb.AppendLine("重叠检测：");
            int overlaps = 0;
            for (int i = 0; i < rooms.Count; i++)
            {
                for (int j = i + 1; j < rooms.Count; j++)
                {
                    var a = rooms[i];
                    var b = rooms[j];
                    bool ox = a.MinX < b.MaxX - 0.01f && a.MaxX > b.MinX + 0.01f;
                    bool oz = a.MinZ < b.MaxZ - 0.01f && a.MaxZ > b.MinZ + 0.01f;
                    if (ox && oz)
                    {
                        sb.AppendLine($"  ❌ {a.Id} 与 {b.Id} 重叠 " +
                                      $"(A: x[{a.MinX:F1},{a.MaxX:F1}] z[{a.MinZ:F1},{a.MaxZ:F1}] | " +
                                      $"B: x[{b.MinX:F1},{b.MaxX:F1}] z[{b.MinZ:F1},{b.MaxZ:F1}])");
                        overlaps++;
                    }
                }
            }
            if (overlaps == 0) sb.AppendLine("  ✅ 无重叠");
            sb.AppendLine();

            // ── ② 联通（带门判定）──
            sb.AppendLine("联通性检测：");
            var reachable = new HashSet<string>();
            var queue = new Queue<string>();

            var start = rooms.Find(r => r.Id == startRoomId);
            if (start == null)
            {
                sb.AppendLine($"  ❌ 找不到起点房间 {startRoomId}");
            }
            else
            {
                reachable.Add(start.Id);
                queue.Enqueue(start.Id);

                while (queue.Count > 0)
                {
                    string curId = queue.Dequeue();
                    var cur = rooms.Find(r => r.Id == curId);
                    if (cur == null) continue;

                    foreach (var other in rooms)
                    {
                        if (reachable.Contains(other.Id)) continue;
                        if (AreConnected(cur, other))
                        {
                            reachable.Add(other.Id);
                            queue.Enqueue(other.Id);
                        }
                    }
                }
            }

            sb.AppendLine($"  从 {startRoomId} 出发可达：{reachable.Count} / {rooms.Count}");
            foreach (var r in rooms)
                if (!reachable.Contains(r.Id))
                    sb.AppendLine($"  ❌ {r.Id}（{r.DisplayName}）不可达");
            if (reachable.Count == rooms.Count) sb.AppendLine("  ✅ 全部房间可达");
            sb.AppendLine();

            // ── ③ 结论 ──
            bool pass = overlaps == 0 && reachable.Count == rooms.Count;
            sb.AppendLine("=====================================");
            sb.AppendLine($"结论：{(pass ? "✅ 布局校验通过" : "❌ 布局存在问题")}");

            string text = sb.ToString();
            Debug.Log("<b>[LayoutChecker]</b>\n" + text);

            try
            {
                string dir = Path.GetFullPath(Path.Combine(
                    Path.GetDirectoryName(Application.dataPath), "../Reports"));
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, reportFile), text, new UTF8Encoding(false));
            }
            catch { }

            if (!pass) EditorApplication.Exit(1);
        }

        /// <summary>
        /// 判断两个房间是否**真正连通**。
        /// 必须：墙面贴合 + 沿墙重叠 + 双方在该墙面都有门。
        /// </summary>
        private static bool AreConnected(AbyssEcho.LevelGen.RoomDef a, AbyssEcho.LevelGen.RoomDef b)
        {
            const float tol = 0.6f;   // 允许的墙面缝隙

            // A 的北墙 贴 B 的南墙
            if (Mathf.Abs(a.MaxZ - b.MinZ) < tol &&
                a.MinX < b.MaxX && a.MaxX > b.MinX &&
                HasDoor(a, AbyssEcho.LevelGen.WallSide.North) &&
                HasDoor(b, AbyssEcho.LevelGen.WallSide.South))
                return true;

            // B 的北墙 贴 A 的南墙
            if (Mathf.Abs(b.MaxZ - a.MinZ) < tol &&
                a.MinX < b.MaxX && a.MaxX > b.MinX &&
                HasDoor(b, AbyssEcho.LevelGen.WallSide.North) &&
                HasDoor(a, AbyssEcho.LevelGen.WallSide.South))
                return true;

            // A 的东墙 贴 B 的西墙
            if (Mathf.Abs(a.MaxX - b.MinX) < tol &&
                a.MinZ < b.MaxZ && a.MaxZ > b.MinZ &&
                HasDoor(a, AbyssEcho.LevelGen.WallSide.East) &&
                HasDoor(b, AbyssEcho.LevelGen.WallSide.West))
                return true;

            // B 的东墙 贴 A 的西墙
            if (Mathf.Abs(b.MaxX - a.MinX) < tol &&
                a.MinZ < b.MaxZ && a.MaxZ > b.MinZ &&
                HasDoor(b, AbyssEcho.LevelGen.WallSide.East) &&
                HasDoor(a, AbyssEcho.LevelGen.WallSide.West))
                return true;

            return false;
        }

        private static bool HasDoor(AbyssEcho.LevelGen.RoomDef r, AbyssEcho.LevelGen.WallSide side)
        {
            foreach (var d in r.Doorways)
                if (d.Side == side) return true;
            return false;
        }
    }
}