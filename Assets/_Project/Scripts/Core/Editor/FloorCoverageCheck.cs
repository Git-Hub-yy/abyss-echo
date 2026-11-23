using System.IO;using System.Text;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine;
namespace AbyssEcho.Core.EditorTools {
  public static class FloorCoverageCheck {
    public static void Run() {
      var sb = new StringBuilder();
      sb.AppendLine("=== 第一章地板覆盖检测 ===");
      sb.AppendLine();
      EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_Chapter1_Graybox.unity", OpenSceneMode.Single);
      Physics.SyncTransforms();

      // 所有地板
      var floors = new System.Collections.Generic.List<Renderer>();
      foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)) {
        if (r == null) continue;
        string n = r.gameObject.name;
        if (n.StartsWith("Floor_")) floors.Add(r);
      }
      sb.AppendLine($"地板对象数: {floors.Count}");
      Bounds total = new Bounds();
      bool first = true;
      foreach (var f in floors) {
        if (first) { total = f.bounds; first = false; } else total.Encapsulate(f.bounds);
      }
      sb.AppendLine($"地板总包围盒: center={total.center} size={total.size}");
      sb.AppendLine($"  X[{total.min.x:F2}, {total.max.x:F2}]  Z[{total.min.z:F2}, {total.max.z:F2}]");
      sb.AppendLine();

      // 逐个地板列出（只列 X 范围含 7.5 的）
      sb.AppendLine("包含 X=7.5 的地板：");
      int found = 0;
      foreach (var f in floors) {
        var b = f.bounds;
        if (b.min.x <= 7.5f && b.max.x >= 7.5f) {
          sb.AppendLine($"  {f.gameObject.name}: X[{b.min.x:F2},{b.max.x:F2}] Y[{b.min.y:F2},{b.max.y:F2}] Z[{b.min.z:F2},{b.max.z:F2}]");
          found++;
        }
      }
      sb.AppendLine($"共 {found} 个");
      sb.AppendLine();

      // 检查 Z=33.25 附近
      sb.AppendLine("包含 X=7.5, Z=33.25 的地板：");
      foreach (var f in floors) {
        var b = f.bounds;
        if (b.min.x <= 7.5f && b.max.x >= 7.5f && b.min.z <= 33.25f && b.max.z >= 33.25f)
          sb.AppendLine($"  ✅ {f.gameObject.name}: Y[{b.min.y:F2},{b.max.y:F2}]");
      }
      sb.AppendLine();

      // 射线验证
      sb.AppendLine("射线检测 (7.5, 30, 33.25) → 下:");
      var hits = Physics.RaycastAll(new Vector3(7.5f, 30f, 33.25f), Vector3.down, 80f, ~0, QueryTriggerInteraction.Ignore);
      System.Array.Sort(hits, (a,b)=>a.distance.CompareTo(b.distance));
      if (hits.Length == 0) sb.AppendLine("  ❌ 空");
      else foreach (var h in hits) sb.AppendLine($"  y={h.point.y:F2}  {h.collider.gameObject.name}");

      Debug.Log(sb.ToString());
      File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Final_Test\FloorCoverage.txt", sb.ToString(), new UTF8Encoding(false));
    }
  }
}