using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace AbyssEcho.Core.EditorTools {
    public static class AltarHierarchyCheck {
        public static void Run() {
            var sb = new StringBuilder();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_Chapter3_Graybox.unity", OpenSceneMode.Single);
            Physics.SyncTransforms();

            sb.AppendLine("══════ 供奉台 vs 供奉台面 的层级关系 ══════");
            foreach (var n in new[] {"供奉台", "供奉台面"}) {
                var go = GameObject.Find(n);
                if (go == null) { sb.AppendLine($"  {n} 不存在"); continue; }
                sb.AppendLine($"  {n} @ {go.transform.position}");
                string path = go.name;
                var t = go.transform;
                int d = 0;
                while (t.parent != null && d++ < 8) { t = t.parent; path = t.name + "/" + path; }
                sb.AppendLine($"    路径 = {path}");
                sb.AppendLine($"    父 = {(go.transform.parent != null ? go.transform.parent.name : "无")}");
                var ia = go.GetComponentInParent<AbyssEcho.Interaction.IInteractable>();
                sb.AppendLine($"    GetComponentInParent<IInteractable> = {(ia != null ? ia.GetType().Name : "❌ null")}");
                foreach (var c in go.GetComponents<Component>())
                    sb.AppendLine($"      组件 {(c == null ? "Missing" : c.GetType().Name)}");
            }

            sb.AppendLine();
            sb.AppendLine("══════ 结论 ══════");
            sb.AppendLine("  兜底探测命中的『供奉台面』如果不是『供奉台』的子物体，");
            sb.AppendLine("  就找不到 OfferingAltar，交互失败。");

            Debug.Log(sb.ToString());
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\AltarHierarchyCheck.txt", sb.ToString(), new UTF8Encoding(false));
        }
    }
}