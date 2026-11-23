using System.IO;using System.Text;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine;
namespace AbyssEcho.Core.EditorTools {
  public static class TitleCardCheck {
    public static void Run() {
      var sb = new StringBuilder();
      foreach (var (ch,p) in new (int,string)[]{(1,"Assets/_Project/Scenes/Level_Chapter1_Graybox.unity"),(2,"Assets/_Project/Scenes/Level_Chapter2_Graybox.unity"),(3,"Assets/_Project/Scenes/Level_Chapter3_Graybox.unity"),(4,"Assets/_Project/Scenes/Level_Chapter4_Graybox.unity"),(5,"Assets/_Project/Scenes/Level_Chapter5_Graybox.unity")}) {
        EditorSceneManager.OpenScene(p, OpenSceneMode.Single);
        var cards = Object.FindObjectsByType<AbyssEcho.UI.ChapterTitleCard>(FindObjectsSortMode.None);
        sb.AppendLine($"第{ch}章: ChapterTitleCard = {cards.Length} 个");
        foreach (var c in cards) {
          var t = typeof(AbyssEcho.UI.ChapterTitleCard);
          var BF = System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
          var tt = t.GetField("_totalTime", BF) ?? t.GetField("totalTime", BF);
          var ot = t.GetField("onlyFirstTime", BF);
          var sk = t.GetField("allowSkip", BF);
          sb.AppendLine($"  {c.gameObject.name} enabled={c.enabled} " +
            $"totalTime={(tt!=null?tt.GetValue(c).ToString():"?")} onlyFirstTime={(ot!=null?ot.GetValue(c).ToString():"?")} allowSkip={(sk!=null?sk.GetValue(c).ToString():"?")}");
        }
      }
      Debug.Log(sb.ToString());
      File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Final_Test\TitleCardCheck.txt", sb.ToString(), new UTF8Encoding(false));
    }
  }
}