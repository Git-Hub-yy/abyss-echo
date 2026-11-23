using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace AbyssEcho.Core.EditorTools {
    public static class SocketRayCheck {
        public static void Run() {
            var sb = new StringBuilder();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_Chapter3_Graybox.unity", OpenSceneMode.Single);
            Physics.SyncTransforms();

            sb.AppendLine("══════ 模拟玩家站在灯座前，能否交互到 ══════");
            sb.AppendLine("相机高度 = 1.62（Player 0.05 + CameraPivot 1.62）");
            sb.AppendLine("交互距离 = 2.6，SphereCast 半径 = 0.12");
            sb.AppendLine();

            var socket = GameObject.Find("长明灯座1");
            if (socket == null) { sb.AppendLine("找不到灯座"); Debug.Log(sb.ToString()); return; }
            Vector3 sp = socket.transform.position;
            var bc = socket.GetComponent<BoxCollider>();
            var b = bc.bounds;
            sb.AppendLine($"灯座1 @ {sp}  包围盒 y[{b.min.y:F2},{b.max.y:F2}]");
            sb.AppendLine($"  灯座顶部 y = {b.max.y:F2}");
            sb.AppendLine($"  相机高度 = 1.67（世界）");
            sb.AppendLine($"  → 相机比灯座高 {1.67f - b.max.y:F2} 米");
            sb.AppendLine();

            // 玩家站在灯座南侧不同距离，水平朝北看
            sb.AppendLine("══════ 玩家站在灯座南侧 N 米，水平朝北（pitch=0）══════");
            sb.AppendLine($"{"距离",-8} {"射线起点",-28} {"命中",-30} 结果");
            sb.AppendLine(new string('-', 96));
            foreach (float dist in new[] {0.8f, 1.2f, 1.5f, 2.0f, 2.5f}) {
                Vector3 camPos = new Vector3(sp.x, 1.67f, sp.z - dist);
                Vector3 dir = Vector3.forward;
                bool hit = Physics.SphereCast(camPos, 0.12f, dir, out var hi, 2.6f, ~0, QueryTriggerInteraction.Collide);
                string name = hit && hi.collider != null ? hi.collider.gameObject.name : "无";
                string mark = name.Contains("长明灯座") ? "✅ 能交互" : "❌ 交互不到";
                sb.AppendLine($"{dist,-8:F1} ({camPos.x:F1},{camPos.y:F2},{camPos.z:F1}),  {name,-30} {mark}");
            }

            sb.AppendLine();
            sb.AppendLine("══════ 玩家低头 30° 再看 ══════");
            foreach (float dist in new[] {0.8f, 1.2f, 1.5f, 2.0f}) {
                Vector3 camPos = new Vector3(sp.x, 1.67f, sp.z - dist);
                Vector3 dir = Quaternion.Euler(30f, 0, 0) * Vector3.forward;
                bool hit = Physics.SphereCast(camPos, 0.12f, dir, out var hi, 2.6f, ~0, QueryTriggerInteraction.Collide);
                string name = hit && hi.collider != null ? hi.collider.gameObject.name : "无";
                string mark = name.Contains("长明灯座") ? "✅" : "❌";
                sb.AppendLine($"  距{dist:F1}m 低头30°: {name,-30} {mark}");
            }

            sb.AppendLine();
            sb.AppendLine("══════ 对比：供奉台（高度 1.0）══════");
            var altar = GameObject.Find("供奉台");
            var ab = altar.GetComponent<BoxCollider>().bounds;
            sb.AppendLine($"供奉台包围盒 y[{ab.min.y:F2},{ab.max.y:F2}]");
            foreach (float dist in new[] {1.0f, 1.5f, 2.0f}) {
                Vector3 camPos = new Vector3(ab.center.x, 1.67f, ab.center.z - dist - 0.7f);
                bool hit = Physics.SphereCast(camPos, 0.12f, Vector3.forward, out var hi, 2.6f, ~0, QueryTriggerInteraction.Collide);
                string name = hit && hi.collider != null ? hi.collider.gameObject.name : "无";
                string mark = name.Contains("供奉台") ? "✅" : "❌";
                sb.AppendLine($"  距{dist:F1}m 水平: {name,-30} {mark}");
            }

            Debug.Log(sb.ToString());
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\SocketRayCheck.txt", sb.ToString(), new UTF8Encoding(false));
        }
    }
}