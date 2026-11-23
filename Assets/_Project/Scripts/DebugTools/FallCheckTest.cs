using System.Collections;using System.IO;using System.Text;using UnityEngine;
namespace AbyssEcho.DebugTools {
  public class FallCheckTest : MonoBehaviour {
    StringBuilder _log = new StringBuilder();
    void Start() { StartCoroutine(Run()); }
    IEnumerator Run() {
      var motor = FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
      _log.AppendLine("=== 玩家下坠检测 ===");
      _log.AppendLine($"场景: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
      _log.AppendLine($"初始位置: {motor?.transform.position}");
      _log.AppendLine();
      _log.AppendLine($"{"时间",-8} {"位置",-32} {"速度",-10} grounded");
      for (int i = 0; i < 24; i++) {
        yield return new WaitForSeconds(0.5f);
        var cc = motor?.GetComponent<CharacterController>();
        var v = motor != null ? motor.GetComponent<CharacterController>() : null;
        _log.AppendLine($"{i*0.5f,-8:F1} {motor?.transform.position.ToString(),-32} " +
                        $"{((cc!=null)?cc.velocity.y:0),-10:F2} {(cc!=null?cc.isGrounded:false)}");
      }
      Debug.Log("<b>[FallCheck]</b>\n" + _log);
      File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Final_Test\02_Raw_fall.txt", _log.ToString(), new UTF8Encoding(false));
      Application.Quit(0);
    }
  }
}