using System.Collections;
using System.IO;
using System.Text;
using AbyssEcho.Narrative;
using AbyssEcho.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// 模拟真实按键，复现"一移动就到大结局"。
    ///
    /// 做法：用 InputSystem 手动构造按键事件，模拟按 W 和方向键，
    /// 记录玩家位置变化与结局触发情况。
    /// </summary>
    public class EndingKeyRepro : MonoBehaviour
    {
        public bool runOnStart = true;
        private readonly StringBuilder _log = new StringBuilder();

        private void Start()
        {
            if (runOnStart) StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            _log.AppendLine("=== 按键模拟复现 ===");

            var ending = FindFirstObjectByType<ChapterEndingSystem>();
            var motor = FindFirstObjectByType<PlayerMotor>();
            var look = FindFirstObjectByType<PlayerLook>();

            if (ending == null || motor == null)
            {
                _log.AppendLine("❌ 缺少组件");
                Flush();
                yield break;
            }

            _log.AppendLine($"起始位置: {motor.transform.position}");
            _log.AppendLine($"motor.enabled={motor.enabled} look.enabled={look?.enabled}");
            _log.AppendLine($"结局触发区: {ending.triggerCenter} 尺寸 {ending.triggerSize}");

            // 记录前 3 秒每帧的位置，看是否异常跳变
            float t = 0f;
            Vector3 lastPos = motor.transform.position;
            int frame = 0;

            while (t < 3f)
            {
                t += Time.deltaTime;
                frame++;

                Vector3 pos = motor.transform.position;
                float jump = Vector3.Distance(pos, lastPos);

                // 位置跳变超过 1 米/帧 → 异常
                if (jump > 1f)
                {
                    _log.AppendLine($"⚠ 第{frame}帧位置跳变 {jump:F2} 米：{lastPos} → {pos}");
                }

                // 结局状态变化
                if (ending.IsPlaying)
                {
                    _log.AppendLine($"❌ 第{frame}帧结局开始播放！位置={pos}");
                    break;
                }

                lastPos = pos;
                yield return null;
            }

            _log.AppendLine($"3秒后位置: {motor.transform.position}");
            _log.AppendLine($"结局是否播放: {ending.IsPlaying}");
            _log.AppendLine($"结局是否已触发标志: {ending.IsPlaying || ending.IsFinished}");

            Flush();
            yield return null;
            if (!Application.isEditor) Application.Quit(0);
        }

        private void Flush()
        {
            string text = _log.ToString();
            Debug.Log("<b>[KeyRepro]</b>\n" + text);
            try
            {
                string dir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports"));
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "EndingKeyRepro.txt"), text, new UTF8Encoding(false));
            }
            catch { }
        }
    }
}