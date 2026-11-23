using System.Collections;
using System.IO;
using System.Text;
using AbyssEcho.Narrative;
using AbyssEcho.Player;
using AbyssEcho.UI;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// S8 自动测试：第一章结局与开场标题卡。
    /// </summary>
    public class AutoTest_S8 : MonoBehaviour
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
            _log.AppendLine("ABYSS ECHO · S8 结局与标题卡自动测试");
            _log.AppendLine("=====================================");

            yield return new WaitForSeconds(0.5f);

            T63_EndingExists();
            T64_TitleCardExists();
            T65_EndingTriggerZone();
            T66_EndingText();
            T67_TriggerByTeleport();
            yield return new WaitForSeconds(2.0f);
            T68_EndingStarted();
            T69_InputFrozen();

            _log.AppendLine("=====================================");
            _log.AppendLine($"结果：通过 {_pass}，失败 {_fail}");

            string text = _log.ToString();
            Debug.Log("<b>[AutoTest_S8]</b>\n" + text);
            WriteReport(text);

            yield return new WaitForSeconds(0.2f);
            if (!Application.isEditor) Application.Quit(_fail == 0 ? 0 : 1);
        }

        private void T63_EndingExists()
        {
            var e = FindFirstObjectByType<ChapterEndingSystem>();
            Check("T63 结局系统存在", e != null);
        }

        private void T64_TitleCardExists()
        {
            var t = FindFirstObjectByType<ChapterTitleCard>();
            Check("T64 开场标题卡存在", t != null);
        }

        private void T65_EndingTriggerZone()
        {
            var e = FindFirstObjectByType<ChapterEndingSystem>();
            if (e == null) { Fail("T65 终点区域配置正确", "无结局系统"); return; }

            var expected = AbyssEcho.LevelGen.ChapterOneLayout.ChapterEndPoint;
            float dx = Mathf.Abs(e.triggerCenter.x - expected.x);
            float dz = Mathf.Abs(e.triggerCenter.z - expected.z);

            bool ok = dx < 0.1f && dz < 0.1f && e.triggerSize.x > 1f && e.triggerSize.z > 1f;
            Check("T65 终点区域配置正确", ok,
                  $"触发点=({e.triggerCenter.x:F1},{e.triggerCenter.z:F1}) 期望=({expected.x:F1},{expected.z:F1}) 尺寸={e.triggerSize}");
        }

        private void T66_EndingText()
        {
            var e = FindFirstObjectByType<ChapterEndingSystem>();
            if (e == null) { Fail("T66 结局文案已设置", "无结局系统"); return; }

            int n = e.endingLines?.Length ?? 0;
            bool ok = n >= 3 && !string.IsNullOrEmpty(e.chapterTitle);
            Check("T66 结局文案已设置", ok,
                  $"共 {n} 句，标题={e.chapterTitle}");
        }

        private void T67_TriggerByTeleport()
        {
            // 把玩家传送到终点，模拟走到那里
            var motor = FindFirstObjectByType<PlayerMotor>();
            var e = FindFirstObjectByType<ChapterEndingSystem>();
            if (motor == null || e == null) { Fail("T67 可传送到终点", "缺少组件"); return; }

            var cc = motor.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            motor.transform.position = e.triggerCenter;
            if (cc != null) cc.enabled = true;

            bool inZone = Vector3.Distance(motor.transform.position, e.triggerCenter) < 1f;
            Check("T67 可传送到终点", inZone, $"位置={motor.transform.position}");
        }

        private void T68_EndingStarted()
        {
            var e = FindFirstObjectByType<ChapterEndingSystem>();
            if (e == null) { Fail("T68 结局已开始播放", "无结局系统"); return; }

            Check("T68 结局已开始播放", e.IsPlaying, $"IsPlaying={e.IsPlaying}");
        }

        private void T69_InputFrozen()
        {
            var motor = FindFirstObjectByType<PlayerMotor>();
            var look = FindFirstObjectByType<PlayerLook>();

            bool frozen = (motor == null || !motor.enabled) && (look == null || !look.enabled);
            Check("T69 结局时输入已冻结", frozen,
                  $"motor.enabled={motor?.enabled} look.enabled={look?.enabled}");
        }

        // ────────────────────────── 工具 ──────────────────────────

        private void Check(string name, bool ok, string detail = "")
        {
            if (ok) { _pass++; _log.AppendLine($"[通过] {name} {detail}"); }
            else { _fail++; _log.AppendLine($"[失败] {name} {detail}"); }
        }

        private void Fail(string name, string detail)
        {
            _fail++;
            _log.AppendLine($"[失败] {name} {detail}");
        }

        private static void WriteReport(string text)
        {
            try
            {
                string dir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports"));
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "S8_AutoTestResult.txt"), text, new UTF8Encoding(false));
            }
            catch { }
        }
    }
}