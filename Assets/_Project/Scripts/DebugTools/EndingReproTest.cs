using System.Collections;
using System.IO;
using System.Text;
using AbyssEcho.Narrative;
using AbyssEcho.Player;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// 复现"一移动就到大结局"的问题。
    ///
    /// 模拟：站在起点，按住 W 移动几秒，记录玩家位置与结局状态。
    /// </summary>
    public class EndingReproTest : MonoBehaviour
    {
        public bool runOnStart = true;
        private readonly StringBuilder _log = new StringBuilder();

        private void Start()
        {
            if (runOnStart) StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            _log.AppendLine("=== 结局误触复现测试 ===");

            var ending = FindFirstObjectByType<ChapterEndingSystem>();
            var motor = FindFirstObjectByType<PlayerMotor>();
            var card = FindFirstObjectByType<AbyssEcho.UI.ChapterTitleCard>();

            // 关闭标题卡，避免遮挡
            if (card != null) card.enabled = false;

            if (ending == null || motor == null)
            {
                _log.AppendLine("❌ 缺少组件");
                Flush();
                yield break;
            }

            _log.AppendLine($"起点: {motor.transform.position}");
            _log.AppendLine($"触发区: 中心={ending.triggerCenter} 尺寸={ending.triggerSize}");
            _log.AppendLine($"结局已触发: {ending.IsPlaying}");
            yield return new WaitForSeconds(1f);

            // 模拟移动：直接改位置（不依赖输入设备）
            Vector3[] moves =
            {
                new Vector3(0f, 0.05f, -40f),
                new Vector3(0f, 0.05f, -38f),
                new Vector3(0f, 0.05f, -35f),
                new Vector3(0f, 0.05f, -30f),
                new Vector3(0f, 0.05f, -25f),
            };

            foreach (var pos in moves)
            {
                var cc = motor.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                motor.transform.position = pos;
                if (cc != null) cc.enabled = true;

                yield return new WaitForSeconds(0.5f);

                bool playing = ending != null && ending.IsPlaying;
                _log.AppendLine($"移动到 {pos} => 结局播放={playing}");

                if (playing)
                {
                    _log.AppendLine("❌ 结局被误触发！");
                    break;
                }
            }

            _log.AppendLine("测试结束");
            Flush();
            yield return null;
        }

        private void Flush()
        {
            string text = _log.ToString();
            Debug.Log("<b>[EndingRepro]</b>\n" + text);
            try
            {
                string dir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports"));
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "EndingRepro.txt"), text, new UTF8Encoding(false));
            }
            catch { }
        }
    }
}