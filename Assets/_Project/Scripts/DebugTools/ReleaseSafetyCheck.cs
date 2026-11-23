using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// 发布前安全检查：列出场景中所有带 runOnStart 的组件及其状态。
    /// 应该在发布版中**不存在**任何 runOnStart=true 的组件。
    /// </summary>
    public class ReleaseSafetyCheck : MonoBehaviour
    {
        public bool runOnStart = true;
        private readonly StringBuilder _log = new StringBuilder();

        private void Start()
        {
            if (runOnStart) StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            _log.AppendLine("=== 发布安全检查 ===");

            var all = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            int found = 0, enabledCount = 0;

            foreach (var mb in all)
            {
                if (mb == null) continue;
                var f = mb.GetType().GetField("runOnStart");
                if (f == null || f.FieldType != typeof(bool)) continue;

                found++;
                bool val = f.GetValue(mb) is bool b && b;
                if (val) enabledCount++;

                _log.AppendLine($"  {(val ? "⚠ 启用" : "✓ 禁用")} {mb.GetType().Name} @ {mb.gameObject.name}");
            }

            _log.AppendLine();
            _log.AppendLine($"找到带 runOnStart 的组件: {found} 个");
            _log.AppendLine($"其中处于启用状态: {enabledCount} 个 => {(enabledCount == 0 ? "✅ 安全" : "❌ 有调试脚本会运行！")}");

            Flush();
            yield return new WaitForSeconds(0.5f);
            if (!Application.isEditor) Application.Quit(enabledCount == 0 ? 0 : 1);
        }

        private void Flush()
        {
            string text = _log.ToString();
            Debug.Log("<b>[SafetyCheck]</b>\n" + text);
            try
            {
                string dir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports"));
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "ReleaseSafetyCheck.txt"), text, new UTF8Encoding(false));
            }
            catch { }
        }
    }
}