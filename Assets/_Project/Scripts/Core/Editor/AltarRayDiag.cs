using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace AbyssEcho.Core.EditorTools {
    public static class AltarRayDiag {
        public static void Run() {
            var sb = new StringBuilder();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_Chapter3_Graybox.unity", OpenSceneMode.Single);
            Physics.SyncTransforms();

            var altar = GameObject.Find("供奉台");
            Vector3 ap = altar.transform.position;
            sb.AppendLine($"供奉台 @ {ap}");
            var bc = altar.GetComponent<BoxCollider>();
            var b = bc.bounds;
            sb.AppendLine($"  包围盒 x[{b.min.x:F2},{b.max.x:F2}] y[{b.min.y:F2},{b.max.y:F2}] z[{b.min.z:F2},{b.max.z:F2}]");

            sb.AppendLine();
            sb.AppendLine("══════ 玩家站南侧 1.6m 平视朝北的射线 ══════");
            Vector3 cam = new Vector3(ap.x, 1.67f, ap.z - 1.6f);
            sb.AppendLine($"  相机 {cam}  dir=(0,0,1)");
            var hits = Physics.RaycastAll(cam, Vector3.forward, 3f, ~0, QueryTriggerInteraction.Collide);
            foreach (var h in hits) {
                string comp = h.collider.GetComponentInParent<AbyssEcho.Interaction.IInteractable>() != null ? "【可交互】" : "";
                sb.AppendLine($"    {h.collider.gameObject.name} @ {h.distance:F2}m {comp}");
            }
            sb.AppendLine("  （空 = 什么都没打到）");

            sb.AppendLine();
            sb.AppendLine("══════ 检查供奉台周围有遮挡吗 ══════");
            Vector3 cam2 = new Vector3(ap.x, 1.0f, ap.z - 1.6f);
            sb.AppendLine($"  从高度 1.0m 水平朝北：");
            var hits2 = Physics.RaycastAll(cam2, Vector3.forward, 3f, ~0, QueryTriggerInteraction.Collide);
            foreach (var h in hits2) sb.AppendLine($"    {h.collider.gameObject.name} @ {h.distance:F2}m");

            sb.AppendLine();
            sb.AppendLine("══════ 供奉台的 layer 与 IInteractable ══════");
            sb.AppendLine($"  layer = {LayerMask.LayerToName(altar.layer)} ({altar.layer})");
            var ia = altar.GetComponent<AbyssEcho.Interaction.InteractableBase>();
            sb.AppendLine($"  InteractableBase = {(ia != null ? ia.GetType().Name : "❌ 无")}");
            if (ia != null) {
                sb.AppendLine($"  CanInteract = {ia.CanInteract}");
                sb.AppendLine($"  GetPrompt = \"{ia.GetPrompt()}\"");
            }

            sb.AppendLine();
            sb.AppendLine("══════ 模拟兜底探测（视线下方40°，半径0.35）══════");
            Vector3 dir = Quaternion.AngleAxis(40f, Vector3.right) * Vector3.forward;
            sb.AppendLine($"  方向 = {dir}");
            sb.AppendLine($"  相机 {cam}");
            if (Physics.SphereCast(cam, 0.35f, dir, out var hi, 2.2f, ~0, QueryTriggerInteraction.Collide)) {
                string comp = hi.collider.GetComponentInParent<AbyssEcho.Interaction.IInteractable>() != null ? "【可交互】" : "";
                sb.AppendLine($"    命中 {hi.collider.gameObject.name} @ {hi.distance:F2}m {comp}");
            } else sb.AppendLine($"    未命中");
            sb.AppendLine($"  射线落点估算 ({cam.x:F1}, {cam.y - 2.2f*dir.y:F2}, {cam.z + 2.2f*dir.z:F2})");

            Debug.Log(sb.ToString());
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\AltarRayDiag.txt", sb.ToString(), new UTF8Encoding(false));
        }
    }
}