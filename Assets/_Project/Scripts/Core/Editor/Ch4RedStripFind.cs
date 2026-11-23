using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace AbyssEcho.Core.EditorTools {
    public static class Ch4RedStripFind {
        public static void Run() {
            var sb = new StringBuilder();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_Chapter4_Graybox.unity", OpenSceneMode.Single);
            Physics.SyncTransforms();

            sb.AppendLine("══════ 第四章所有使用有机材质的物体 ══════");
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)) {
                if (r.sharedMaterial == null) continue;
                if (!r.sharedMaterial.name.Contains("Org")) continue;
                var ws = r.bounds.size;
                sb.AppendLine($"  {r.gameObject.name,-30} {r.sharedMaterial.name,-20} @({r.transform.position.x:F1},{r.transform.position.y:F1},{r.transform.position.z:F1}) 尺寸={ws.x:F1}x{ws.y:F1}x{ws.z:F1}");
            }

            sb.AppendLine();
            sb.AppendLine("══════ 位置 Z=6 附近的物体 ══════");
            Vector3 camPos = new Vector3(0f, 1.67f, 5.9f);
            var hits = Physics.RaycastAll(camPos, Vector3.left, 6f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a,b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits) {
                if (h.collider == null) continue;
                var rend = h.collider.GetComponent<MeshRenderer>();
                string mat = rend != null && rend.sharedMaterial != null ? rend.sharedMaterial.name : "-";
                var ws = rend != null ? rend.bounds.size : Vector3.zero;
                sb.AppendLine($"  左: {h.collider.gameObject.name,-30} @{h.distance:F2}m {mat,-20} 尺寸={ws.x:F1}x{ws.y:F1}x{ws.z:F1}");
            }

            Debug.Log(sb.ToString());
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Ch4RedStripFind.txt", sb.ToString(), new UTF8Encoding(false));
        }
    }
}