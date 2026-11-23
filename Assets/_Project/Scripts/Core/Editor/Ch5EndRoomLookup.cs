using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace AbyssEcho.Core.EditorTools {
    public static class Ch5EndRoomLookup {
        public static void Run() {
            var sb = new StringBuilder();
            sb.AppendLine("=== 第五章终点房间查找验证 ===");
            sb.AppendLine();

            // 强制设成第五章
            AbyssEcho.LevelGen.ChapterLayout.Current = AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter5;
            sb.AppendLine($"ChapterLayout.Current = {AbyssEcho.LevelGen.ChapterLayout.Current}");

            var rooms = AbyssEcho.LevelGen.ChapterLayout.GetRooms(AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter5);
            sb.AppendLine($"GetRooms(Chapter5) 返回 {rooms.Count} 个房间");
            sb.AppendLine();
            sb.AppendLine($"  {"Id",-12} {"Center",-24} {"MinZ",-8} {"MaxZ",-8} FloorY");
            foreach (var r in rooms) {
                sb.AppendLine($"  {r.Id,-12} {r.Center.ToString(),-24} {r.MinZ,-8:F1} {r.MaxZ,-8:F1} {r.FloorY:F1}");
            }

            sb.AppendLine();
            var endPt = AbyssEcho.LevelGen.ChapterLayout.GetChapterEndPoint();
            sb.AppendLine($"GetChapterEndPoint() = {endPt}");

            var h7 = rooms.Find(r => r.Id == "H7");
            sb.AppendLine();
            if (h7 == null) {
                sb.AppendLine("❌ 找不到 Id==\"H7\" 的房间！");
                sb.AppendLine("   → 这就是结局触发点停留在默认 (0,1,39) 的原因");
            } else {
                sb.AppendLine($"✅ 找到 H7: Center={h7.Center} FloorY={h7.FloorY}");
                sb.AppendLine($"   应有触发点 = ({h7.Center.x}, {h7.FloorY + 0.9f}, {h7.Center.z})");
            }

            Debug.Log(sb.ToString());
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Ch5EndRoomLookup.txt", sb.ToString(), new UTF8Encoding(false));
        }
    }
}