using System.Collections;
using System.IO;
using System.Text;
using AbyssEcho.Narrative;
using AbyssEcho.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// 深度诊断：找出场景中所有 PlayerMotor、所有 ChapterEndingSystem，
    /// 以及它们的位置关系，定位"一移动就触发结局"的真正原因。
    /// </summary>
    public class DeepEndingDiag : MonoBehaviour
    {
        public bool runOnStart = true;
        private readonly StringBuilder _log = new StringBuilder();

        private void Start()
        {
            if (runOnStart) StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            _log.AppendLine("=== 深度诊断 ===");
            _log.AppendLine($"场景: {SceneManager.GetActiveScene().name}");
            _log.AppendLine();

            // ① 所有 PlayerMotor
            var motors = FindObjectsByType<PlayerMotor>(FindObjectsSortMode.None);
            _log.AppendLine($"PlayerMotor 数量: {motors.Length}");
            foreach (var m in motors)
            {
                _log.AppendLine($"  · {m.name} 位置={m.transform.position} enabled={m.enabled} active={m.gameObject.activeInHierarchy}");
            }
            _log.AppendLine();

            // ② 所有 ChapterEndingSystem
            var endings = FindObjectsByType<ChapterEndingSystem>(FindObjectsSortMode.None);
            _log.AppendLine($"ChapterEndingSystem 数量: {endings.Length}");
            foreach (var e in endings)
            {
                _log.AppendLine($"  · {e.name} 触发区中心={e.triggerCenter} 尺寸={e.triggerSize} enabled={e.enabled}");
                _log.AppendLine($"    所在对象: {e.gameObject.name} active={e.gameObject.activeInHierarchy}");
            }
            _log.AppendLine();

            // ③ 检查触发判断
            foreach (var e in endings)
            {
                foreach (var m in motors)
                {
                    var p = m.transform.position;
                    var half = e.triggerSize * 0.5f;
                    bool inX = Mathf.Abs(p.x - e.triggerCenter.x) <= half.x;
                    bool inY = Mathf.Abs(p.y - e.triggerCenter.y) <= half.y;
                    bool inZ = Mathf.Abs(p.z - e.triggerCenter.z) <= half.z;
                    bool inside = inX && inY && inZ;

                    _log.AppendLine($"[检查] {m.name}@{p} vs {e.name}@{e.triggerCenter}");
                    _log.AppendLine($"  X差={Mathf.Abs(p.x - e.triggerCenter.x):F2}/{half.x:F2}={inX}");
                    _log.AppendLine($"  Y差={Mathf.Abs(p.y - e.triggerCenter.y):F2}/{half.y:F2}={inY}");
                    _log.AppendLine($"  Z差={Mathf.Abs(p.z - e.triggerCenter.z):F2}/{half.z:F2}={inZ}");
                    _log.AppendLine($"  => {(inside ? "❌ 在触发区内！" : "✅ 不在")}");
                }
            }
            _log.AppendLine();

            // ④ 观察 2 秒，看位置是否变化
            _log.AppendLine("=== 观察 2 秒 ===");
            for (int i = 0; i < 4; i++)
            {
                yield return new WaitForSeconds(0.5f);
                var ms = FindObjectsByType<PlayerMotor>(FindObjectsSortMode.None);
                var es = FindObjectsByType<ChapterEndingSystem>(FindObjectsSortMode.None);
                string s = $"  t={0.5f * (i + 1):F1}s: ";
                foreach (var m in ms) s += $"{m.name}@{m.transform.position:F1} ";
                foreach (var e in es) s += $"ended={e.IsPlaying} ";
                _log.AppendLine(s);
            }

            Flush();
            yield return null;
            if (!Application.isEditor) Application.Quit(0);
        }

        private void Flush()
        {
            string text = _log.ToString();
            Debug.Log("<b>[DeepDiag]</b>\n" + text);
            try
            {
                string dir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports"));
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "DeepEndingDiag.txt"), text, new UTF8Encoding(false));
            }
            catch { }
        }
    }
}