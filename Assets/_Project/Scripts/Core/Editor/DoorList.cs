using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>列出场景中所有门及其碰撞体，检查是否挡住了主通道。</summary>
    public static class DoorList
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity";

        [MenuItem("ABYSS ECHO/工具：列出所有门")]
        public static void List()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var sb = new StringBuilder();
            sb.AppendLine("=== 场景中所有门 ===");

            var doors = Object.FindObjectsByType<AbyssEcho.Interaction.DoorInteractable>(FindObjectsSortMode.None);
            sb.AppendLine($"共 {doors.Length} 扇门");
            sb.AppendLine();

            foreach (var d in doors)
            {
                Vector3 pos = d.transform.position;
                Vector3 scale = d.transform.localScale;
                var col = d.GetComponent<Collider>();

                sb.AppendLine($"  【{d.name}】");
                sb.AppendLine($"    位置=({pos.x:F2}, {pos.y:F2}, {pos.z:F2})");
                sb.AppendLine($"    尺寸=({scale.x:F2}, {scale.y:F2}, {scale.z:F2})");
                sb.AppendLine($"    locked={d.locked}  unlockableByInteraction={d.unlockableByInteraction}");
                sb.AppendLine($"    requiredItem={(string.IsNullOrEmpty(d.requiredItemId) ? "（空）" : d.requiredItemId)}");
                sb.AppendLine($"    IsOpen={d.IsOpen}  碰撞体={(col != null ? col.GetType().Name + (col.enabled ? "" : "(禁用)") : "无")}");
                sb.AppendLine($"    提示=\"{d.lockedPrompt}\"");
                sb.AppendLine();
            }

            string text = sb.ToString();
            Debug.Log("<b>[DoorList]</b>\n" + text);

            try
            {
                string dir = Path.GetFullPath(Path.Combine(
                    Path.GetDirectoryName(Application.dataPath), "../Reports"));
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "DoorList.txt"), text, new UTF8Encoding(false));
            }
            catch { }
        }
    }
}