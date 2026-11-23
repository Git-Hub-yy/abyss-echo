using System.Collections;using System.IO;using System.Text;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine;
namespace AbyssEcho.Core.EditorTools {
  public static class GapScan {
    public static void Run() {
      var sb = new StringBuilder();
      sb.AppendLine("=== 第一章地板缺口扫描（0.5m 网格）===");
      sb.AppendLine();
      EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_Chapter1_Graybox.unity", OpenSceneMode.Single);
      Physics.SyncTransforms();

      var holes = new System.Collections.Generic.List<Vector2>();
      for (float x = -16f; x <= 17f; x += 0.5f) {
        for (float z = -42f; z <= 44f; z += 0.5f) {
          bool hasFloor = false;
          var hits = Physics.RaycastAll(new Vector3(x, 20f, z), Vector3.down, 40f, ~0, QueryTriggerInteraction.Ignore);
          foreach (var h in hits) {
            if (h.collider.gameObject.name.StartsWith("Floor_") ||
                h.collider.gameObject.name.Contains("Floor")) { hasFloor = true; break; }
          }
          if (!hasFloor) holes.Add(new Vector2(x, z));
        }
      }

      sb.AppendLine($"缺口点数: {holes.Count}（总采样 {(17f+16f)/0.5f*1f*((44f+42f)/0.5f):F0}）");
      sb.AppendLine();

      // 聚类
      var clusters = new System.Collections.Generic.List<System.Collections.Generic.List<Vector2>>();
      foreach (var hole in holes) {
        bool added = false;
        foreach (var c in clusters) {
          foreach (var p in c) {
            if (Vector2.Distance(p, hole) < 1.2f) { c.Add(hole); added = true; break; }
          }
          if (added) break;
        }
        if (!added) clusters.Add(new System.Collections.Generic.List<Vector2> { hole });
      }

      sb.AppendLine($"缺口区域数: {clusters.Count}");
      sb.AppendLine();
      int idx = 1;
      foreach (var c in clusters) {
        if (c.Count < 2) continue;
        float minX=999,maxX=-999,minZ=999,maxZ=-999;
        foreach (var p in c) {
          if (p.x<minX)minX=p.x; if(p.x>maxX)maxX=p.x;
          if (p.y<minZ)minZ=p.y; if(p.y>maxZ)maxZ=p.y;
        }
        sb.AppendLine($"  缺口#{idx++}: {c.Count} 个采样点  X[{minX:F1},{maxX:F1}] Z[{minZ:F1},{maxZ:F1}]  中心=({(minX+maxX)/2:F1},{(minZ+maxZ)/2:F1})");
      }

      Debug.Log(sb.ToString());
      File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Final_Test\GapScan.txt", sb.ToString(), new UTF8Encoding(false));
    }
  }
}