using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>UI 组件挂载诊断：确认 TAB 物品栏、J 日志等是否真的在场景里。</summary>
    public class UIDiag : MonoBehaviour
    {
        public bool runOnStart = true;
        private readonly StringBuilder _log = new StringBuilder();

        private void Start()
        {
            if (runOnStart) StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            yield return new WaitForSeconds(0.8f);

            _log.AppendLine("=== UI 组件诊断 ===");
            _log.AppendLine($"场景：{UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");

            Report<AbyssEcho.UI.InventoryUI>("InventoryUI (Tab 物品栏)");
            Report<AbyssEcho.UI.NarrativeUI>("NarrativeUI (J 日志)");
            Report<AbyssEcho.UI.PauseMenuUI>("PauseMenuUI (Esc 暂停)");
            Report<AbyssEcho.UI.ControlsHelpUI>("ControlsHelpUI (H 操作说明)");
            Report<AbyssEcho.UI.CrosshairUI>("CrosshairUI (准星)");
            Report<AbyssEcho.UI.KeypadUI>("KeypadUI (密码键盘)");

            Flush();
            yield return null;
            if (!Application.isEditor) Application.Quit(0);
        }

        private void Report<T>(string label) where T : Component
        {
            var comps = FindObjectsByType<T>(FindObjectsSortMode.None);
            if (comps.Length == 0)
            {
                _log.AppendLine($"❌ {label}：未找到");
                return;
            }

            foreach (var c in comps)
            {
                _log.AppendLine($"✅ {label}：{c.gameObject.name} " +
                                $"active={c.gameObject.activeInHierarchy} enabled={(c as Behaviour)?.enabled}");
            }
        }

        private void Flush()
        {
            string text = _log.ToString();
            Debug.Log("<b>[UIDiag]</b>\n" + text);
            try
            {
                string dir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports"));
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "UIDiag.txt"), text, new UTF8Encoding(false));
            }
            catch { }
        }
    }
}