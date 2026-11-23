using System.Collections;using System.IO;using System.Text;using System.Reflection;using UnityEngine;
namespace AbyssEcho.DebugTools {
  public class CamCheckTest : MonoBehaviour {
    StringBuilder _log = new StringBuilder();
    void Start() { StartCoroutine(Run()); }
    IEnumerator Run() {
      yield return new WaitForSeconds(7f);
      var det = FindFirstObjectByType<AbyssEcho.Interaction.InteractionDetector>();
      var cams = FindObjectsByType<Camera>(FindObjectsSortMode.None);
      _log.AppendLine($"场景相机数 = {cams.Length}");
      foreach (var c in cams) _log.AppendLine($"  {c.name}  depth={c.depth}  enabled={c.enabled}  pos={c.transform.position}  tag={c.tag}");
      _log.AppendLine($"Camera.main = {(Camera.main != null ? Camera.main.name : "null")}");
      if (det != null) {
        var f = typeof(AbyssEcho.Interaction.InteractionDetector).GetField("_cam", BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);
        var cam = f?.GetValue(det) as Camera;
        _log.AppendLine($"detector._cam = {(cam != null ? cam.name + " @ " + cam.transform.position : "null")}");
        var distF = typeof(AbyssEcho.Interaction.InteractionDetector).GetField("interactDistance", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        var radiusF = typeof(AbyssEcho.Interaction.InteractionDetector).GetField("castRadius", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        _log.AppendLine($"interactDistance = {distF?.GetValue(det)}  castRadius = {radiusF?.GetValue(det)}");
      }
      Debug.Log("<b>[CamCheck]</b>\n" + _log);
      File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Final_Test\02_Raw_camcheck.txt", _log.ToString(), new UTF8Encoding(false));
      Application.Quit(0);
    }
  }
}