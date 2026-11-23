using System.Collections;
using System.IO;
using System.Text;
using AbyssEcho.Narrative;
using AbyssEcho.Player;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// 结局误触诊断：输出触发区与玩家位置的实际数值。
    /// </summary>
    public class EndingTriggerDiagnostic : MonoBehaviour
    {
        public bool runOnStart = true;
        private readonly StringBuilder _log = new StringBuilder();

        private void Start()
        {
            if (runOnStart) StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            _log.AppendLine("=== 结局触发诊断 ===");

            var ending = FindFirstObjectByType<ChapterEndingSystem>();
            var motor = FindFirstObjectByType<PlayerMotor>();

            if (ending == null)
            {
                _log.AppendLine("❌ 找不到 ChapterEndingSystem");
                Flush();
                yield break;
            }

            _log.AppendLine($"触发区中心: {ending.triggerCenter}");
            _log.AppendLine($"触发区尺寸: {ending.triggerSize}");
            _log.AppendLine($"半尺寸: {ending.triggerSize * 0.5f}");
            _log.AppendLine($"触发延迟: {ending.triggerDelay}");

            if (motor != null)
            {
                _log.AppendLine($"玩家初始位置: {motor.transform.position}");

                var p = motor.transform.position;
                var half = ending.triggerSize * 0.5f;
                bool inX = Mathf.Abs(p.x - ending.triggerCenter.x) <= half.x;
                bool inY = Mathf.Abs(p.y - ending.triggerCenter.y) <= half.y;
                bool inZ = Mathf.Abs(p.z - ending.triggerCenter.z) <= half.z;

                _log.AppendLine($"初始是否在区内: X={inX} Y={inY} Z={inZ} => {(inX && inY && inZ ? "❌ 是（会误触！）" : "✅ 否")}");
                _log.AppendLine($"  X 差值={Mathf.Abs(p.x - ending.triggerCenter.x):F2} 允许={half.x:F2}");
                _log.AppendLine($"  Y 差值={Mathf.Abs(p.y - ending.triggerCenter.y):F2} 允许={half.y:F2}");
                _log.AppendLine($"  Z 差值={Mathf.Abs(p.z - ending.triggerCenter.z):F2} 允许={half.z:F2}");
            }

            _log.AppendLine($"结局系统 enabled: {ending.enabled}");
            _log.AppendLine($"结局系统 activeInHierarchy: {ending.gameObject.activeInHierarchy}");

            Flush();
            yield return null;
        }

        private void Flush()
        {
            string text = _log.ToString();
            Debug.Log("<b>[EndingDiag]</b>\n" + text);
            try
            {
                string dir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports"));
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "EndingTriggerDiag.txt"), text, new UTF8Encoding(false));
            }
            catch { }
        }
    }
}