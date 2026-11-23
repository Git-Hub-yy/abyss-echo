using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
namespace AbyssEcho.DebugTools {
    public class CamDirProbe : MonoBehaviour {
        void Start() { StartCoroutine(Run()); }
        IEnumerator Run() {
            yield return new WaitForSeconds(1.0f);
            var sb = new StringBuilder();
            sb.AppendLine("══════ 相机实际朝向探测 ══════");
            var motor = FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
            var cc = motor.GetComponent<CharacterController>();
            var cam = Camera.main;
            var look = FindFirstObjectByType<AbyssEcho.Player.PlayerLook>();
            if (look != null) look.enabled = false;

            var pivot = cam.transform.parent;

            // 放到 G5 中心
            cc.enabled = false;
            motor.transform.position = new Vector3(-19f, 0.05f, 0f);
            motor.transform.rotation = Quaternion.Euler(0, 0, 0);
            cc.enabled = true;
            yield return null; yield return null;

            sb.AppendLine($"  Player pos = {motor.transform.position}  rot = {motor.transform.eulerAngles}");
            sb.AppendLine($"  Pivot  pos = {pivot.position}  worldRot = {pivot.eulerAngles}  localRot = {pivot.localEulerAngles}");
            sb.AppendLine($"  Camera pos = {cam.transform.position}");
            sb.AppendLine($"  Camera worldRot = {cam.transform.eulerAngles}");
            sb.AppendLine($"  Camera forward = {cam.transform.forward}");
            sb.AppendLine();
            sb.AppendLine("  → forward 应该是 (0,0,1) 才是朝北");

            // 射线：相机正前方
            var org = cam.transform.position;
            var fwd = cam.transform.forward;
            var hits = Physics.RaycastAll(org, fwd, 15f, ~0, QueryTriggerInteraction.Ignore);
            sb.AppendLine();
            sb.AppendLine($"  从相机 {org} 沿 forward 射 15m：");
            foreach (var h in hits)
                sb.AppendLine($"    {h.collider.gameObject.name} @ {h.distance:F2}m  point={h.point}");

            Debug.Log(sb.ToString());
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\CamDirProbe.txt", sb.ToString(), new UTF8Encoding(false));
            if (!Application.isEditor) Application.Quit(0);
        }
    }
}