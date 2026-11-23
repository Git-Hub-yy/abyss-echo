using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace AbyssEcho.Core.EditorTools {
    public static class LeftWallIdentify {
        public static void Run() {
            var sb = new StringBuilder();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_Chapter1_Graybox.unity", OpenSceneMode.Single);
            Physics.SyncTransforms();

            Vector3 camPos = new Vector3(9.9f, 1.67f, 15.1f);
            sb.AppendLine($"══════ 从起点 ({camPos.x},{camPos.z}) 向各个方向射线 ══════");

            string[] dirs = { "正前(+Z)","左(-X)","右(+X)","后(-Z)" };
            Vector3[] vecs = { Vector3.forward, Vector3.left, Vector3.right, Vector3.back };

            for (int i = 0; i < 4; i++) {
                sb.AppendLine($"--- {dirs[i]} ---");
                var hits = Physics.RaycastAll(camPos, vecs[i], 12f, ~0, QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits, (a,b) => a.distance.CompareTo(b.distance));
                foreach (var h in hits) {
                    if (h.collider == null) continue;
                    var rend = h.collider.GetComponent<MeshRenderer>();
                    string mat = rend != null && rend.sharedMaterial != null ? rend.sharedMaterial.name : "-";
                    var ws = rend != null ? rend.bounds.size : Vector3.zero;
                    sb.AppendLine($"    {h.collider.gameObject.name,-32} @{h.distance:F2}m  {mat,-22} 尺寸={ws.x:F1}x{ws.y:F1}x{ws.z:F1}");
                }
            }

            Debug.Log(sb.ToString());
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\LeftWallIdentify.txt", sb.ToString(), new UTF8Encoding(false));
        }
    }
}