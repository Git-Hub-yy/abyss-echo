using System.IO;using System.Text;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine;
namespace AbyssEcho.Core.EditorTools {
  public static class PlayerCompCheck {
    public static void Run() {
      var sb = new StringBuilder();
      foreach (var (n,p) in new (int,string)[]{
        (1,"Assets/_Project/Scenes/Level_Chapter1_Graybox.unity"),
        (2,"Assets/_Project/Scenes/Level_Chapter2_Graybox.unity"),
        (3,"Assets/_Project/Scenes/Level_Chapter3_Graybox.unity"),
        (4,"Assets/_Project/Scenes/Level_Chapter4_Graybox.unity"),
        (5,"Assets/_Project/Scenes/Level_Chapter5_Graybox.unity")}) {
        EditorSceneManager.OpenScene(p, OpenSceneMode.Single);
        var m = Object.FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
        var card = Object.FindFirstObjectByType<AbyssEcho.UI.ChapterTitleCard>();
        var cc = m?.GetComponent<CharacterController>();
        var death = Object.FindFirstObjectByType<AbyssEcho.Horror.PlayerDeathSystem>();
        var flash = Object.FindFirstObjectByType<AbyssEcho.Tools.FlashlightSystem>();
        sb.AppendLine($"第{n}章:");
        sb.AppendLine($"  PlayerMotor={(m!=null?m.gameObject.name:"❌")} pos={(m!=null?m.transform.position.ToString():"")}");
        sb.AppendLine($"  CharacterController={(cc!=null?"✅":"❌")}");
        sb.AppendLine($"  ChapterTitleCard={(card!=null?card.gameObject.name+" enabled="+card.enabled:"❌ 无")}");
        sb.AppendLine($"  PlayerDeathSystem={(death!=null?"✅":"❌")}  FlashlightSystem={(flash!=null?"✅":"❌")}");
        var pickups = Object.FindObjectsByType<AbyssEcho.Interaction.PickupItem>(FindObjectsSortMode.None);
        sb.AppendLine($"  PickupItem={pickups.Length} 个");
      }
      Debug.Log(sb.ToString());
      File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Final_Test\PlayerComp.txt", sb.ToString(), new UTF8Encoding(false));
    }
  }
}