using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>列出场景中所有 UI 组件及其启用状态。</summary>
    public static class UIComponentList
    {
        [MenuItem("ABYSS ECHO/工具：列出场景 UI 组件")]
        public static void List()
        {
            var sb = new StringBuilder();

            foreach (var scenePath in new[]
            {
                "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity",
                "Assets/_Project/Scenes/Level_Chapter2_Graybox.unity"
            })
            {
                if (!System.IO.File.Exists(scenePath))
                {
                    sb.AppendLine($"{scenePath} : 不存在");
                    continue;
                }

                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                sb.AppendLine($"=== {System.IO.Path.GetFileName(scenePath)} ===");

                Check<AbyssEcho.UI.InventoryUI>(sb, "InventoryUI (Tab)");
                Check<AbyssEcho.UI.NarrativeUI>(sb, "NarrativeUI (J)");
                Check<AbyssEcho.UI.PauseMenuUI>(sb, "PauseMenuUI (Esc)");
                Check<AbyssEcho.UI.ControlsHelpUI>(sb, "ControlsHelpUI (H)");
                Check<AbyssEcho.UI.CrosshairUI>(sb, "CrosshairUI");
                Check<AbyssEcho.UI.KeypadUI>(sb, "KeypadUI (E)");
                Check<AbyssEcho.Narrative.JournalSystem>(sb, "JournalSystem");

                sb.AppendLine();
            }

            string text = sb.ToString();
            Debug.Log("<b>[UIList]</b>\n" + text);

            try
            {
                string dir = Path.GetFullPath(Path.Combine(
                    Path.GetDirectoryName(Application.dataPath), "../Reports"));
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "UIComponentList.txt"), text, new UTF8Encoding(false));
            }
            catch { }
        }

        private static void Check<T>(StringBuilder sb, string label) where T : Component
        {
            var comps = Object.FindObjectsByType<T>(FindObjectsSortMode.None);
            if (comps.Length == 0)
            {
                sb.AppendLine($"  ❌ {label}：未找到");
                return;
            }

            foreach (var c in comps)
            {
                bool active = c.gameObject.activeInHierarchy;
                bool enabled = (c as Behaviour)?.enabled ?? true;
                sb.AppendLine($"  {(active && enabled ? "✅" : "⚠️")} {label}：对象={c.gameObject.name} " +
                              $"active={active} enabled={enabled}");
            }
        }
    }
}