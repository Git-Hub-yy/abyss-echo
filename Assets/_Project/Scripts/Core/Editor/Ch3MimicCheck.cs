using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    public static class Ch3MimicCheck
    {
        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== 第三章 房间与 Mimic 位置对照 ===");

            var rooms = AbyssEcho.LevelGen.ChapterThreeLayout.Build();
            foreach (var r in rooms)
                sb.AppendLine($"  {r.Id,-14} x[{r.MinX,7:F1},{r.MaxX,7:F1}] z[{r.MinZ,7:F1},{r.MaxZ,7:F1}] 地面Y={r.FloorY:F2}");

            sb.AppendLine();
            sb.AppendLine("=== 房间 F5 详情 ===");
            foreach (var r in rooms)
            {
                if (r.Id != "F5") continue;
                sb.AppendLine($"  {r.Id} {r.DisplayName}: x[{r.MinX:F1},{r.MaxX:F1}] z[{r.MinZ:F1},{r.MaxZ:F1}]");
            }

            string text = sb.ToString();
            Debug.Log(text);
            File.WriteAllText(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports/Ch3Rooms.txt")), text, new UTF8Encoding(false));
        }
    }
}