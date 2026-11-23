using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace AbyssEcho.Core.EditorTools {
    public static class Ch5FinalCheck {
        public static void Run() {
            var sb = new StringBuilder();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_Chapter5_Graybox.unity", OpenSceneMode.Single);
            Physics.SyncTransforms();
            int rend=0, nullSlot=0, pink=0, missScript=0;
            var mats = new System.Collections.Generic.HashSet<string>();
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)) {
                if (r == null) continue; rend++;
                var sm = r.sharedMaterials;
                if (sm == null || sm.Length == 0) { nullSlot++; continue; }
                foreach (var m in sm) {
                    if (m == null) { nullSlot++; continue; }
                    mats.Add(m.name);
                    if (m.shader == null || m.shader.name.Contains("Error")) pink++;
                }
            }
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
                foreach (var c in go.GetComponents<Component>()) if (c == null) missScript++;
            int lights = 0;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l != null && l.enabled) lights++;
            sb.AppendLine($"Renderer={rend} 材质种类={mats.Count} 空槽={nullSlot} 粉色={pink} MissingScript={missScript} 灯光={lights}");
            Debug.Log(sb.ToString());
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Ch5FinalCheck.txt", sb.ToString(), new UTF8Encoding(false));
        }
    }
}