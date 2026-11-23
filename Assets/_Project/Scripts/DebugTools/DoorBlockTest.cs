using System.Collections;
using System.IO;
using System.Text;
using AbyssEcho.Narrative;
using AbyssEcho.Player;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// 门阻挡测试：把玩家依次放到各个门前方，向门的方向施加速度，
    /// 看玩家能否穿过去。
    /// </summary>
    public class DoorBlockTest : MonoBehaviour
    {
        public bool runOnStart = true;
        private readonly StringBuilder _log = new StringBuilder();

        private void Start()
        {
            if (runOnStart) StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            _log.AppendLine("=== 门阻挡测试 ===");

            var motor = FindFirstObjectByType<PlayerMotor>();
            var ending = FindFirstObjectByType<ChapterEndingSystem>();
            if (motor == null) { _log.AppendLine("无 PlayerMotor"); Flush(); yield break; }

            // 关闭结局系统，避免干扰
            if (ending != null) ending.enabled = false;

            // 列出所有门
            var doors = FindObjectsByType<AbyssEcho.Interaction.DoorInteractable>(FindObjectsSortMode.None);
            _log.AppendLine($"发现 {doors.Length} 个门");
            foreach (var d in doors)
            {
                var col = d.GetComponent<Collider>();
                _log.AppendLine($"  · {d.name} 位置={d.transform.position} 缩放={d.transform.localScale} " +
                                $"locked={d.locked} 碰撞体={(col != null ? col.GetType().Name + (col.enabled ? "" : "(禁用)") : "无")}");
            }
            _log.AppendLine();

            // 把玩家放到门的一侧，用 CharacterController 尝试穿过
            foreach (var d in doors)
            {
                var cc = motor.GetComponent<CharacterController>();
                Vector3 doorPos = d.transform.position;

                // 从门南侧 3 米处出发
                Vector3 start = new Vector3(doorPos.x, 0.1f, doorPos.z - 3f);

                if (cc != null) cc.enabled = false;
                motor.transform.position = start;
                if (cc != null) cc.enabled = true;

                yield return new WaitForSeconds(0.2f);

                // 直接用 CharacterController.Move 推向门
                if (cc != null)
                {
                    for (int i = 0; i < 60; i++)
                    {
                        cc.Move(new Vector3(0f, -0.02f, 0.08f));
                        yield return null;
                    }
                }

                Vector3 endPos = motor.transform.position;
                float passed = endPos.z - doorPos.z;

                _log.AppendLine($"[{d.name}] 起点Z={start.z:F1} 终点Z={endPos.z:F1} 相对门={passed:F2}m " +
                                $"{(passed > 0.5f ? "❌ 穿过去了！" : "✅ 被挡住")}");
            }

            Flush();
            yield return null;
            if (!Application.isEditor) Application.Quit(0);
        }

        private void Flush()
        {
            string text = _log.ToString();
            Debug.Log("<b>[DoorTest]</b>\n" + text);
            try
            {
                string dir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports"));
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "DoorBlockTest.txt"), text, new UTF8Encoding(false));
            }
            catch { }
        }
    }
}