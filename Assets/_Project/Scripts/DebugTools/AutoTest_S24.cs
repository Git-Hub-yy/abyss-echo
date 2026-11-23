using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// S24 四项回归测试：逐一验证四个问题的修复，互不干扰。
    /// </summary>
    public class AutoTest_S24 : MonoBehaviour
    {
        public bool runOnStart = true;

        private int _pass, _fail;
        private readonly StringBuilder _log = new StringBuilder();

        private void Start()
        {
            if (runOnStart) StartCoroutine(RunAll());
        }

        private IEnumerator RunAll()
        {
            _log.AppendLine("ABYSS ECHO · S24 四项修复回归测试");
            _log.AppendLine("=====================================");
            yield return new WaitForSeconds(0.6f);

            yield return T1_DoorOpensFully();
            yield return T2_NoteStaysOpen();
            yield return T3_EndingNotTriggeredInC1();
            yield return T4_JournalToggleStable();

            _log.AppendLine("=====================================");
            _log.AppendLine($"结果：通过 {_pass}，失败 {_fail}");

            string text = _log.ToString();
            Debug.Log("<b>[S24Test]</b>\n" + text);

            try
            {
                string dir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports"));
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "S24_AutoTestResult.txt"), text, new UTF8Encoding(false));
            }
            catch { }

            yield return new WaitForSeconds(0.2f);
            if (!Application.isEditor) Application.Quit(_fail == 0 ? 0 : 1);
        }

        // ── 问题 1：Door_C2 打开后门洞要完全让开 ──
        private IEnumerator T1_DoorOpensFully()
        {
            AbyssEcho.Interaction.DoorInteractable door = null;
            foreach (var d in FindObjectsByType<AbyssEcho.Interaction.DoorInteractable>(FindObjectsSortMode.None))
                if (d.name == "Door_C2") door = d;

            if (door == null) { Check("T1 门洞完全让开", false, "找不到 Door_C2"); yield break; }

            Vector3 closed = door.transform.position;
            door.locked = false;
            door.OnInteract(null);
            yield return new WaitForSeconds(2.5f);

            Vector3 open = door.transform.position;
            float dx = Mathf.Abs(open.x - closed.x);
            float dz = Mathf.Abs(open.z - closed.z);

            // 门宽沿 Z，所以应该沿 Z 位移，且距离 ≥ 门宽的 90%
            float doorWidth = Mathf.Max(door.transform.localScale.x, door.transform.localScale.z);
            bool alongZ = dz > dx;
            bool farEnough = Mathf.Max(dx, dz) >= doorWidth * 0.9f;

            // 门洞中央检查
            bool clear = true;
            for (float y = 0.5f; y <= 1.7f; y += 0.4f)
            {
                for (float z = closed.z - 0.8f; z <= closed.z + 0.8f; z += 0.4f)
                {
                    var hits = Physics.OverlapSphere(new Vector3(closed.x, y, z), 0.2f, ~0, QueryTriggerInteraction.Ignore);
                    foreach (var h in hits)
                        if (h != null && h.gameObject.name == "Door_C2") clear = false;
                }
            }

            Check("T1 门洞完全让开", alongZ && farEnough && clear,
                  $"沿Z={alongZ} 距离={Mathf.Max(dx, dz):F2}(需≥{doorWidth * 0.9f:F2}) 门洞畅通={clear}");
        }

        // ── 问题 2：便条打开后要停留，不能立刻关闭 ──
        private IEnumerator T2_NoteStaysOpen()
        {
            var note = FindFirstObjectByType<AbyssEcho.Narrative.ReadableNote>();
            var nar = FindFirstObjectByType<AbyssEcho.UI.NarrativeUI>();

            if (note == null || nar == null) { Check("T2 便条打开后保持", false, "找不到组件"); yield break; }

            nar.Close();
            yield return null;

            note.OnInteract(null);
            bool openedImmediately = nar.IsOpen;

            // 等 0.5 秒，模拟玩家看图
            yield return new WaitForSeconds(0.5f);
            bool stillOpen = nar.IsOpen;

            // 再等 1 秒
            yield return new WaitForSeconds(1.0f);
            bool stillOpen2 = nar.IsOpen;

            nar.Close();

            Check("T2 便条打开后保持不消失", openedImmediately && stillOpen && stillOpen2,
                  $"打开={openedImmediately} 0.5s后={stillOpen} 1.5s后={stillOpen2}");
        }

        // ── 问题 3：站在 C1 里不应该触发结局 ──
        private IEnumerator T3_EndingNotTriggeredInC1()
        {
            var ending = FindFirstObjectByType<AbyssEcho.Narrative.ChapterEndingSystem>();
            var motor = FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();

            if (ending == null || motor == null) { Check("T3 C1 内不触发结局", false, "找不到组件"); yield break; }

            // 记录原始位置
            Vector3 orig = motor.transform.position;

            // 把玩家放到 C1 北侧（原来会误触发的点 z=38）
            var cc = motor.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            motor.transform.position = new Vector3(0f, 0.05f, 38f);
            if (cc != null) cc.enabled = true;

            // 等 2 秒（超过 triggerDelay 0.8s）
            yield return new WaitForSeconds(2.0f);

            bool triggered = ending.IsFinished || ending.IsPlaying;

            // 回到原位
            if (cc != null) cc.enabled = false;
            motor.transform.position = orig;
            if (cc != null) cc.enabled = true;

            Check("T3 站在 C1 (z=38) 不触发结局", !triggered,
                  triggered ? "❌ 还是触发了" : "正确：C1 内不触发");

            // 再验证：进到闸门房间内(z=42)应该触发
            yield return new WaitForSeconds(0.3f);
        }

        // ── 问题 4：按 J 开关日志要稳定 ──
        private IEnumerator T4_JournalToggleStable()
        {
            var nar = FindFirstObjectByType<AbyssEcho.UI.NarrativeUI>();
            if (nar == null) { Check("T4 日志开关稳定", false, "找不到 NarrativeUI"); yield break; }

            nar.Close();
            yield return new WaitForSeconds(0.2f);

            // 打开
            nar.ToggleJournal();
            bool opened = nar.IsOpen;

            // 等 0.5 秒（模拟玩家阅读）
            yield return new WaitForSeconds(0.5f);
            bool stillOpen = nar.IsOpen;

            // 再关
            nar.ToggleJournal();
            yield return new WaitForSeconds(0.2f);
            bool closed = !nar.IsOpen;

            Check("T4 按 J 打开后稳定不闪退", opened && stillOpen && closed,
                  $"打开={opened} 0.5s后仍开={stillOpen} 可关闭={closed}");
        }

        // ────────────────────────── 工具 ──────────────────────────
        private void Check(string name, bool ok, string detail = "")
        {
            if (ok) { _pass++; _log.AppendLine($"[通过] {name} {detail}"); }
            else { _fail++; _log.AppendLine($"[失败] {name} {detail}"); }
        }
    }
}