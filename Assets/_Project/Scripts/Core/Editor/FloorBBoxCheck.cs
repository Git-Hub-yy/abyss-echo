using System.IO;using System.Text;using System.Collections.Generic;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine;
namespace AbyssEcho.Core.EditorTools {
  public static class FloorBBoxCheck {
    public static void Run() {
      var sb = new StringBuilder();
      sb.AppendLine("=== 第一章：地板包围盒精确覆盖检测 ===");
      sb.AppendLine();
      EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_Chapter1_Graybox.unity", OpenSceneMode.Single);
      Physics.SyncTransforms();

      // 收集所有 Floor_ 开头的渲染器包围盒
      var rects = new List<(string name, float minX, float maxX, float minZ, float maxZ, float topY)>();
      foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)) {
        if (r == null) continue;
        if (!r.gameObject.name.StartsWith("Floor_")) continue;
        var b = r.bounds;
        rects.Add((r.gameObject.name, b.min.x, b.max.x, b.min.z, b.max.z, b.max.y));
      }
      sb.AppendLine($"地板包围盒数: {rects.Count}");
      sb.AppendLine();

      // 测试关键点
      var testPoints = new (float x, float z, string note)[] {
        (7.5f, 33.25f, "★ 玩家实际掉落点"),
        (0f, -41.5f, "第一章出生点"),
        (0f, 0f, "原点"),
        (7.5f, 34.5f, "C1_C2_Link 地板中心"),
        (7.5f, 28f, "C_EW 走廊"),
        (0f, 8f, "低矮通道"),
        (0f, -6f, "撞墙测试起点"),
      };

      sb.AppendLine("关键点覆盖检查：");
      sb.AppendLine();
      foreach (var (x, z, note) in testPoints) {
        bool covered = false;
        string by = "";
        float topY = -999;
        foreach (var r in rects) {
          if (x >= r.minX && x <= r.maxX && z >= r.minZ && z <= r.maxZ) {
            covered = true;
            if (r.topY > topY) { topY = r.topY; by = r.name; }
          }
        }
        sb.AppendLine($"  ({x:F1}, {z:F1}) {note}");
        sb.AppendLine($"    {(covered ? $"✅ 有地板: {by} (y={topY:F2})" : "❌ 无地板 → 会掉进虚空")}");
      }
      sb.AppendLine();

      // 生成覆盖网格图（文字版），行=Z 列=X
      sb.AppendLine("覆盖网格（X 从 -16 到 17，Z 从 -42 到 44，步长 2m）");
      sb.AppendLine("  ■=有地板  ·=无地板");
      sb.AppendLine();
      sb.Append("       ");
      for (float x = -16f; x <= 17f; x += 2f) sb.Append($"{(int)x,3}");
      sb.AppendLine();
      for (float z = 44f; z >= -42f; z -= 2f) {
        sb.Append($"{z,6} ");
        for (float x = -16f; x <= 17f; x += 2f) {
          bool cov = false;
          foreach (var r in rects)
            if (x >= r.minX && x <= r.maxX && z >= r.minZ && z <= r.maxZ) { cov = true; break; }
          sb.Append(cov ? "  ■" : "  ·");
        }
        sb.AppendLine();
      }

      Debug.Log(sb.ToString());
      File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Final_Test\FloorBBox.txt", sb.ToString(), new UTF8Encoding(false));
    }
  }
}