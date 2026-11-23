using System.IO;using System.Text;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine;
namespace AbyssEcho.Core.EditorTools {
  public static class JKeyCheck {
    public static void Run() {
      var sb = new StringBuilder();
      foreach (var (ch,p) in new (int,string)[]{(1,"Assets/_Project/Scenes/Level_Chapter1_Graybox.unity"),(5,"Assets/_Project/Scenes/Level_Chapter5_Graybox.unity")}) {
        EditorSceneManager.OpenScene(p, OpenSceneMode.Single);
        var all = Object.FindObjectsByType<AbyssEcho.UI.NarrativeUI>(FindObjectsSortMode.None);
        sb.AppendLine($"第{ch}章: NarrativeUI 实例数 = {all.Length}");
        foreach (var n in all) sb.AppendLine($"   {n.gameObject.name} enabled={n.enabled}");
        var reads = Object.FindObjectsByType<AbyssEcho.Player.PlayerInputReader>(FindObjectsSortMode.None);
        sb.AppendLine($"  PlayerInputReader = {reads.Length}");
        foreach (var r in reads) sb.AppendLine($"   {r.gameObject.name} enabled={r.enabled}");
      }
      Debug.Log(sb.ToString());
      File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\JKeyCheck.txt", sb.ToString(), new UTF8Encoding(false));
    }
  }
}