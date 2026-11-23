using System.IO;using System.Text;using System.Collections.Generic;using UnityEditor;using UnityEngine;
namespace AbyssEcho.Core.EditorTools {
  public static class Ch1RoomList {
    public static void Run() {
      var sb = new StringBuilder();
      sb.AppendLine("=== 第一章房间布局 ===");
      sb.AppendLine();

      // 从 layouts 源码里读房间定义
      var files = new[] {
        "Assets/_Project/Scripts/Core/ChapterOneLayout.cs",
        "Assets/_Project/Scripts/Core/Layouts/ChapterOneLayout.cs",
      };
      string src = null;
      foreach (var f in files) if (File.Exists(f)) { src = File.ReadAllText(f); sb.AppendLine($"读取: {f}"); break; }
      if (src == null) {
        foreach (var f in Directory.GetFiles("Assets/_Project/Scripts", "ChapterOneLayout.cs", SearchOption.AllDirectories)) {
          src = File.ReadAllText(f); sb.AppendLine($"读取: {f}"); break;
        }
      }
      if (src == null) { sb.AppendLine("❌ 找不到 ChapterOneLayout.cs"); Debug.Log(sb.ToString()); return; }
      sb.AppendLine();

      // 匹配 Add("名字", x, z, w, d, ...) 或 new RoomDef(...)
      var matches = System.Text.RegularExpressions.Regex.Matches(src,
        @"(?:Add|Def|Room)\s*\(\s*""([A-Za-z0-9_]+)""\s*,\s*(-?[\d.]+)f?\s*,\s*(-?[\d.]+)f?\s*,\s*(-?[\d.]+)f?\s*,\s*(-?[\d.]+)f?");
      sb.AppendLine($"房间定义数: {matches.Count}");
      sb.AppendLine();
      sb.AppendLine($"{"房间",-16} {"中心X",8} {"中心Z",8} {"宽",7} {"深",7}   {"X范围",-18} {"Z范围",-18}");
      foreach (System.Text.RegularExpressions.Match m in matches) {
        string name = m.Groups[1].Value;
        float cx = float.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
        float cz = float.Parse(m.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture);
        float w  = float.Parse(m.Groups[4].Value, System.Globalization.CultureInfo.InvariantCulture);
        float d  = float.Parse(m.Groups[5].Value, System.Globalization.CultureInfo.InvariantCulture);
        sb.AppendLine($"{name,-16} {cx,8:F1} {cz,8:F1} {w,7:F1} {d,7:F1}   " +
                      $"[{cx-w/2:F1},{cx+w/2:F1}]      [{cz-d/2:F1},{cz+d/2:F1}]");
      }
      Debug.Log(sb.ToString());
      File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Final_Test\Ch1Rooms.txt", sb.ToString(), new UTF8Encoding(false));
    }
  }
}