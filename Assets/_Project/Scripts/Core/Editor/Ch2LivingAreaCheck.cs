using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace AbyssEcho.Core.EditorTools {
    public static class Ch2LivingAreaCheck {
        public static void Run() {
            var sb = new StringBuilder();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_Chapter2_Graybox.unity", OpenSceneMode.Single);
            Physics.SyncTransforms();

            // D5 生活区：中心 (0,0,24) 尺寸 14x8
            sb.AppendLine("══════ D5 生活区内的所有 Renderer ══════");
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)) {
                Vector3 p = r.transform.position;
                if (p.x >= -9f && p.x <= 9f && p.z >= 19f && p.z <= 29f) {
                    string mat = r.sharedMaterial != null ? r.sharedMaterial.name : "NULL";
                    sb.AppendLine($"  {r.gameObject.name,-22} {mat,-26} @({p.x:F1},{p.y:F1},{p.z:F1})");
                }
            }

            sb.AppendLine();
            sb.AppendLine("══════ 整个第二章：可加辅助色的候选物体 ══════");
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)) {
                string n = r.gameObject.name;
                if (n.Contains("柜") || n.Contains("箱") || n.Contains("桌") || n.Contains("椅") ||
                    n.Contains("帽") || n.Contains("板") || n.Contains("门")) {
                    string mat = r.sharedMaterial != null ? r.sharedMaterial.name : "NULL";
                    sb.AppendLine($"  {n,-22} {mat,-26} @({r.transform.position.x:F1},{r.transform.position.z:F1})");
                }
            }

            Debug.Log(sb.ToString());
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Ch2LivingAreaCheck.txt", sb.ToString(), new UTF8Encoding(false));
        }
    }
}