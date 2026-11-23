using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace AbyssEcho.Core.EditorTools {
    public static class G5WallVisualCheck {
        public static void Run() {
            var sb = new StringBuilder();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_Chapter4_Graybox.unity", OpenSceneMode.Single);
            Physics.SyncTransforms();
            sb.AppendLine("══════ G5 北墙三段的实际包围盒 ══════");
            foreach (var n in new[] {"North_G5_Left","North_G5_Right","North_G5_Top","North_G5_Full"}) {
                var o = GameObject.Find(n);
                if (o == null) { sb.AppendLine($"  {n}: 不存在"); continue; }
                var bc = o.GetComponent<BoxCollider>();
                var b = bc.bounds;
                var mr = o.GetComponent<MeshRenderer>();
                var mf = o.GetComponent<MeshFilter>();
                sb.AppendLine($"  {n}");
                sb.AppendLine($"    世界 x[{b.min.x:F2},{b.max.x:F2}] y[{b.min.y:F2},{b.max.y:F2}] z[{b.min.z:F2},{b.max.z:F2}]");
                sb.AppendLine($"    active={o.activeInHierarchy}  Renderer={(mr!=null?mr.enabled.ToString():"无")}  Mesh={(mf!=null&&mf.sharedMesh!=null?mf.sharedMesh.name:"无")}");
                if (mr != null) sb.AppendLine($"    材质={(mr.sharedMaterial!=null?mr.sharedMaterial.name:"NULL")}");
                sb.AppendLine($"    localScale={o.transform.localScale}");
            }

            sb.AppendLine();
            sb.AppendLine("══════ 从 (0,1.5,0) 沿 +X 方向（北）射线，看撞到谁 ══════");
            sb.AppendLine("  （x=-19 是门洞中心）");
            for (float x = -21f; x <= -17f; x += 0.5f) {
                var origin = new Vector3(x, 1.5f, 0f);
                var hits = Physics.RaycastAll(origin, Vector3.forward, 10f, ~0, QueryTriggerInteraction.Ignore);
                var names = new System.Collections.Generic.List<string>();
                foreach (var h in hits) {
                    if (h.collider == null) continue;
                    float z = h.point.z;
                    names.Add($"{h.collider.gameObject.name}@z={z:F2}");
                }
                sb.AppendLine($"  x={x,6:F1}  {(names.Count==0 ? "无命中(通!)" : string.Join(" | ", names))}");
            }

            Debug.Log(sb.ToString());
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\G5WallVisualCheck.txt", sb.ToString(), new UTF8Encoding(false));
        }
    }
}