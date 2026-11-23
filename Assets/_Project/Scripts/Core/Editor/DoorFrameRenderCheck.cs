using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace AbyssEcho.Core.EditorTools {
    public static class DoorFrameRenderCheck {
        public static void Run() {
            var sb = new StringBuilder();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_Chapter4_Graybox.unity", OpenSceneMode.Single);
            Physics.SyncTransforms();

            sb.AppendLine("══════ 门框对象渲染状态 ══════");
            foreach (var n in new[] {"G5Door_Frame_L","G5Door_Frame_R","G5Door_Frame_Top",
                                     "G5Door_Lining_L","G5Door_Lining_R","G5Door_Lining_T",
                                     "G5Door_Threshold","Light_G5Door_Guide"}) {
                var o = GameObject.Find(n);
                if (o == null) { sb.AppendLine($"  {n}: ❌ 不存在"); continue; }
                var mr = o.GetComponent<Renderer>();
                var mr2 = o.GetComponent<Light>();
                sb.AppendLine($"  {n}");
                sb.AppendLine($"    位置={o.transform.position}  active={o.activeInHierarchy}");
                if (mr != null) sb.AppendLine($"    Renderer.enabled={mr.enabled}  visible={mr.isVisible}  材质={(mr.sharedMaterial!=null?mr.sharedMaterial.name:"NULL")}  bounds={mr.bounds.center}/{mr.bounds.size}");
                if (mr2 != null) sb.AppendLine($"    Light int={mr2.intensity} range={mr2.range} enabled={mr2.enabled}");
            }

            sb.AppendLine();
            sb.AppendLine("══════ 模拟相机在 (-19,1.67,0) 朝北(+Z)的射线 ══════");
            for (float y = 0.5f; y <= 3.0f; y += 0.5f) {
                var org = new Vector3(-19f, y, 0f);
                var hits = Physics.RaycastAll(org, Vector3.forward, 8f, ~0, QueryTriggerInteraction.Ignore);
                var names = new System.Collections.Generic.List<string>();
                foreach (var h in hits) names.Add($"{h.collider.gameObject.name}@{h.distance:F2}m");
                sb.AppendLine($"  y={y:F1}  {(names.Count==0?"(通)":string.Join(" | ", names))}");
            }

            sb.AppendLine();
            sb.AppendLine("══════ 相机朝北的视野角（FOV）计算 ══════");
            var cam = Object.FindFirstObjectByType<Camera>();
            sb.AppendLine($"  FOV={cam.fieldOfView} aspect={cam.aspect}");
            sb.AppendLine($"  距离墙 5m 时，竖直视野高度 = {2*5*Mathf.Tan(cam.fieldOfView*0.5f*Mathf.Deg2Rad):F2}m");
            sb.AppendLine($"  水平视野宽度 = {2*5*Mathf.Tan(cam.fieldOfView*0.5f*Mathf.Deg2Rad)*cam.aspect:F2}m");
            sb.AppendLine($"  门洞宽 2.4m 高 3.0m");
            sb.AppendLine($"  → 门洞占水平视野 {2.4f/(2*5*Mathf.Tan(cam.fieldOfView*0.5f*Mathf.Deg2Rad)*cam.aspect)*100:F0}%");
            Debug.Log(sb.ToString());
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\DoorFrameRenderCheck.txt", sb.ToString(), new UTF8Encoding(false));
        }
    }
}