using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace AbyssEcho.Core.EditorTools {
    public static class CameraOffsetCheck {
        public static void Run() {
            var sb = new StringBuilder();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_Chapter4_Graybox.unity", OpenSceneMode.Single);
            Physics.SyncTransforms();
            var motor = Object.FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
            if (motor == null) { sb.AppendLine("找不到 PlayerMotor"); Debug.Log(sb.ToString()); return; }
            var cam = Object.FindFirstObjectByType<Camera>();
            sb.AppendLine("══════ 玩家与相机相对位置 ══════");
            sb.AppendLine($"  Player 位置 = {motor.transform.position}");
            sb.AppendLine($"  Camera 位置 = {cam.transform.position}");
            sb.AppendLine($"  Camera 相对偏移 = {cam.transform.position - motor.transform.position}");
            sb.AppendLine($"  Camera localPosition = {cam.transform.localPosition}");
            sb.AppendLine($"  Camera 父 = {(cam.transform.parent != null ? cam.transform.parent.name : "无")}");

            sb.AppendLine();
            sb.AppendLine("══════ 相机父链 ══════");
            var t = cam.transform;
            int d = 0;
            while (t != null && d++ < 8) {
                sb.AppendLine($"  [{d}] {t.name}  world={t.position}  local={t.localPosition}");
                t = t.parent;
            }

            sb.AppendLine();
            sb.AppendLine("══════ 从相机位置朝北看，能看到门框吗 ══════");
            var org = cam.transform.position;
            sb.AppendLine($"  射线起点 = {org}");
            // 朝北（+Z）
            for (float yaw = -10f; yaw <= 10f; yaw += 5f) {
                Vector3 dir = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
                var hits = Physics.RaycastAll(new Vector3(org.x, 1.5f, 0f), dir, 12f, ~0, QueryTriggerInteraction.Ignore);
                var names = new System.Collections.Generic.List<string>();
                foreach (var h in hits) names.Add($"{h.collider.gameObject.name}@{h.distance:F1}m");
                sb.AppendLine($"  yaw={yaw,5:F0}  {(names.Count==0?"无":string.Join(" | ", names))}");
            }
            Debug.Log(sb.ToString());
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\CameraOffsetCheck.txt", sb.ToString(), new UTF8Encoding(false));
        }
    }
}