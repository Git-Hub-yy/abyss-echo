using System.IO;using System.Text;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine;
namespace AbyssEcho.Core.EditorTools {
  public static class SpawnCheck {
    public static void Run() {
      var sb = new StringBuilder();
      sb.AppendLine("=== 五章玩家出生点与地面检测 ===");
      sb.AppendLine();
      foreach (var (ch,p) in new (int,string)[]{
        (1,"Assets/_Project/Scenes/Level_Chapter1_Graybox.unity"),
        (2,"Assets/_Project/Scenes/Level_Chapter2_Graybox.unity"),
        (3,"Assets/_Project/Scenes/Level_Chapter3_Graybox.unity"),
        (4,"Assets/_Project/Scenes/Level_Chapter4_Graybox.unity"),
        (5,"Assets/_Project/Scenes/Level_Chapter5_Graybox.unity")}) {
        EditorSceneManager.OpenScene(p, OpenSceneMode.Single);
        Physics.SyncTransforms();
        var motor = Object.FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
        sb.AppendLine($"──── 第{ch}章 ────");
        if (motor == null) { sb.AppendLine("  ❌ 无 PlayerMotor"); continue; }
        Vector3 pos = motor.transform.position;
        sb.AppendLine($"  玩家出生点: ({pos.x:F2}, {pos.y:F2}, {pos.z:F2})");

        // 从出生点向下打射线
        var hits = Physics.RaycastAll(pos + Vector3.up * 0.5f, Vector3.down, 50f, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a,b)=>a.distance.CompareTo(b.distance));
        if (hits.Length == 0) {
          sb.AppendLine("  ❌ 向下 50 米内没有地面 → **玩家会无限下坠**");
        } else {
          var h = hits[0];
          sb.AppendLine($"  最近下方物体: {h.collider.gameObject.name} @ y={h.point.y:F2}（距 {h.distance:F2}m）");
          float gap = h.point.y - pos.y;
          sb.AppendLine($"  间隙: {gap:F2} 米  {(Mathf.Abs(gap) < 0.5f ? "✅ 正常" : (gap > 0 ? "⚠️ 悬空" : "⚠️ 在地面以下"))}");
        }

        // 从高处向下打，找该 XZ 的地面
        var hits2 = Physics.RaycastAll(new Vector3(pos.x, 30f, pos.z), Vector3.down, 80f, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits2, (a,b)=>a.distance.CompareTo(b.distance));
        sb.AppendLine($"  从 y=30 向下，命中 {hits2.Length} 个：");
        int shown = 0;
        foreach (var h2 in hits2) {
          if (shown++ >= 4) break;
          sb.AppendLine($"    y={h2.point.y:F2}  {h2.collider.gameObject.name}");
        }
        sb.AppendLine();
      }
      Debug.Log(sb.ToString());
      File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Final_Test\SpawnCheck.txt", sb.ToString(), new UTF8Encoding(false));
    }
  }
}