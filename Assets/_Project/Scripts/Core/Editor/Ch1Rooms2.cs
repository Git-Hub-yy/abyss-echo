using System.IO;using System.Text;using UnityEditor;using UnityEngine;
namespace AbyssEcho.Core.EditorTools {
  public static class Ch1Rooms2 {
    public static void Run() {
      var sb = new StringBuilder();
      sb.AppendLine("=== 第一章房间布局（RoomDef 格式）===");
      sb.AppendLine();
      string f = "Assets/_Project/Scripts/LevelGen/ChapterOneLayout.cs";
      string src = File.ReadAllText(f);

      // new RoomDef("A0", "深潜舱", new Vector3(0f, 0f, -41.5f), 2.0f, 2.0f, 2.7f);
      var ms = System.Text.RegularExpressions.Regex.Matches(src,
        @"new RoomDef\(\s*""([^""]+)""\s*,\s*""[^""]*""\s*,\s*new Vector3\(\s*(-?[\d.]+)f?\s*,\s*(-?[\d.]+)f?\s*,\s*(-?[\d.]+)f?\s*\)\s*,\s*(-?[\d.]+)f?\s*,\s*(-?[\d.]+)f?");
      sb.AppendLine($"RoomDef 数: {ms.Count}");
      sb.AppendLine();
      sb.AppendLine($"{"房间",-18} {"中心X",7} {"中心Z",7} {"宽",6} {"深",6}   {"X范围",-20} {"Z范围",-20}");
      foreach (System.Text.RegularExpressions.Match m in ms) {
        string n = m.Groups[1].Value;
        float cx = float.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
        float cz = float.Parse(m.Groups[4].Value, System.Globalization.CultureInfo.InvariantCulture);
        float w  = float.Parse(m.Groups[5].Value, System.Globalization.CultureInfo.InvariantCulture);
        float d  = float.Parse(m.Groups[6].Value, System.Globalization.CultureInfo.InvariantCulture);
        sb.AppendLine($"{n,-18} {cx,7:F1} {cz,7:F1} {w,6:F1} {d,6:F1}   " +
                      $"[{cx-w/2,6:F1},{cx+w/2,6:F1}]   [{cz-d/2,6:F1},{cz+d/2,6:F1}]");
      }

      // 也解析 AttachOutside 出来的房间坐标（它们基于父房间）
      sb.AppendLine();
      sb.AppendLine("注意：AttachOutside 生成的房间坐标由父房间推导，上表只含显式 RoomDef。");
      sb.AppendLine();

      // 直接用场景实测：列出所有地板及范围
      UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
        "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
      Physics.SyncTransforms();
      sb.AppendLine("=== 场景实际地板对象 ===");
      var list = new System.Collections.Generic.List<string>();
      foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)) {
        if (r == null || !r.gameObject.name.StartsWith("Floor_")) continue;
        var b = r.bounds;
        list.Add($"{r.gameObject.name,-30} X[{b.min.x,6:F1},{b.max.x,6:F1}] Z[{b.min.z,6:F1},{b.max.z,6:F1}] Y={b.max.y:F2}");
      }
      list.Sort();
      foreach (var s in list) sb.AppendLine("  " + s);

      Debug.Log(sb.ToString());
      File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Final_Test\Ch1Rooms2.txt", sb.ToString(), new UTF8Encoding(false));
    }
  }
}