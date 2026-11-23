using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// S65：验证「灯泡 → 长明灯座 → 结局 D 照亮」。
    ///
    ///   CH3_CASE=bulb  → 装灯泡 → 期望结局 D「照亮」
    ///   CH3_CASE=bulb_vs_tape → 先装灯泡再供奉录音带 → 应保持 D
    /// </summary>
    public class Ch3LampEndingTest : MonoBehaviour
    {
        public bool runOnStart = true;

        private readonly StringBuilder _log = new StringBuilder();
        private int _pass, _fail, _unverified;
        private static readonly BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private void Start()
        {
            if (runOnStart) StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            string testCase = System.Environment.GetEnvironmentVariable("CH3_CASE") ?? "bulb";

            AbyssEcho.LevelGen.ChapterLayout.Current =
                AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter3;
            var ending = FindFirstObjectByType<AbyssEcho.Narrative.ChapterEndingSystem>();
            ending?.GetType().GetMethod("ApplyChapterConfig", BF)?.Invoke(ending, null);
            yield return new WaitForSeconds(0.6f);

            _log.AppendLine("══════ S65 灯泡结局验证 ══════");
            _log.AppendLine($"用例 = {testCase}");
            _log.AppendLine();

            var sockets = FindObjectsByType<AbyssEcho.Puzzle.LampSocket>(FindObjectsSortMode.None);
            var altar = FindFirstObjectByType<AbyssEcho.Puzzle.OfferingAltar>();
            var inv = AbyssEcho.Inventory.PlayerInventory.Instance;
            if (sockets.Length == 0 || inv == null || ending == null)
            {
                _log.AppendLine($"❌ 缺组件 sockets={sockets.Length} inv={inv != null} ending={ending != null}");
                Dump(testCase); yield break;
            }

            var t = typeof(AbyssEcho.Narrative.ChapterEndingSystem);
            var linesF = t.GetField("endingLines", BF);
            var footerF = t.GetField("chapterFooter", BF);

            _log.AppendLine($"场景中 LampSocket = {sockets.Length} 个");
            foreach (var s in sockets)
                _log.AppendLine($"  {s.gameObject.name} @ {s.transform.position} IsLit={s.IsLit}");
            _log.AppendLine();

            // ══ 装灯泡 ══
            _log.AppendLine("──── 装灯泡 ────");
            inv.Add("item.bulb");
            _log.AppendLine($"  背包有 bulb = {inv.Has("item.bulb")}");

            var socket = sockets[0];
            socket.OnInteract(gameObject);
            yield return new WaitForSeconds(0.3f);

            _log.AppendLine($"  {socket.gameObject.name}.IsLit = {socket.IsLit}");
            _log.AppendLine($"  灯 enabled = {(socket.lampLight != null ? socket.lampLight.enabled.ToString() : "无灯")}");
            _log.AppendLine($"  灯泡标记 active = {(socket.bulbMarker != null ? socket.bulbMarker.gameObject.activeSelf.ToString() : "无")}");
            _log.AppendLine($"  背包还有 bulb = {inv.Has("item.bulb")}（应 False，被消耗）");

            Check("T1 灯座已点亮", socket.IsLit, $"IsLit={socket.IsLit}");
            Check("T2 灯座灯光已开启", socket.lampLight != null && socket.lampLight.enabled,
                  socket.lampLight != null ? $"enabled={socket.lampLight.enabled}" : "无灯");
            Check("T3 灯泡被消耗", !inv.Has("item.bulb"), $"还有={inv.Has("item.bulb")}");

            // ══ 结局判定 ══
            _log.AppendLine();
            _log.AppendLine("──── 结局判定 ────");
            var resolveM = t.GetMethod("ResolveEndingId", BF);
            string id = resolveM?.Invoke(ending,
                new object[] { AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter3 }) as string;
            _log.AppendLine($"  ResolveEndingId = \"{id}\"");
            Check("T4 结局分支 = D", id == "D", $"实际={id}");

            // ══ 干扰测试：再供奉录音带 ══
            if (testCase == "bulb_vs_tape" && altar != null)
            {
                _log.AppendLine();
                _log.AppendLine("──── 干扰：再放录音带 ────");
                inv.Add("item.tape");
                altar.Offer("item.tape");
                yield return new WaitForSeconds(0.2f);
                _log.AppendLine($"  altar.GetEndingId() = \"{altar.GetEndingId()}\"");
                string id2 = resolveM?.Invoke(ending,
                    new object[] { AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter3 }) as string;
                _log.AppendLine($"  ResolveEndingId = \"{id2}\"（灯座优先，应为 D）");
                Check("T5 灯座优先于供奉台", id2 == "D", $"实际={id2}");
            }

            // ══ 触发结局，看播放什么 ══
            _log.AppendLine();
            _log.AppendLine("──── 触发结局 ────");
            var tcF = t.GetField("triggerCenter", BF);
            Vector3 tc = (Vector3)tcF.GetValue(ending);
            var motor = FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
            var cc = motor != null ? motor.GetComponent<CharacterController>() : null;
            if (cc != null) cc.enabled = false;
            motor.transform.position = new Vector3(tc.x, 0.05f, tc.z);
            if (cc != null) cc.enabled = true;
            _log.AppendLine($"  玩家传送到 {tc}");

            float wait = 0f;
            var phaseF = t.GetField("_phase", BF);
            while (wait < 15f)
            {
                string ph = phaseF?.GetValue(ending)?.ToString() ?? "";
                if (ph == "Lines" || ph == "Finished") break;
                wait += Time.deltaTime;
                yield return null;
            }
            yield return new WaitForSeconds(0.5f);

            _log.AppendLine($"  {wait:F1}s 后 _phase = {phaseF?.GetValue(ending)}");

            string footer = footerF?.GetValue(ending) as string ?? "";
            var lines = linesF?.GetValue(ending) as string[];
            _log.AppendLine($"  结语 = \"{footer}\"");
            _log.AppendLine($"  行数 = {lines?.Length ?? 0}");
            if (lines != null)
                for (int i = 0; i < Mathf.Min(4, lines.Length); i++)
                    _log.AppendLine($"    [{i}] {lines[i]}");

            Check("T6 结语 = 结局：照亮", footer == "结局：照亮", $"实际=\"{footer}\"");
            Check("T7 第一句正确",
                  lines != null && lines.Length > 0 && lines[0] == "你把灯泡拧进长明灯座。",
                  $"实际=\"{(lines != null && lines.Length > 0 ? lines[0] : "")}\"");

            _log.AppendLine();
            Unverified("U1 真人装上灯泡后的视觉效果");

            Dump(testCase);
        }

        private void Check(string name, bool ok, string detail)
        {
            if (ok) { _pass++; _log.AppendLine($"[通过] {name}  {detail}"); }
            else { _fail++; _log.AppendLine($"[失败] {name}  {detail}"); }
        }

        private void Unverified(string n) { _unverified++; _log.AppendLine($"[待人工验收] {n}"); }

        private void Dump(string testCase)
        {
            _log.AppendLine();
            _log.AppendLine("=====================================");
            _log.AppendLine($"用例 {testCase}：通过 {_pass}，失败 {_fail}，待人工验收 {_unverified}");

            string text = _log.ToString();
            Debug.Log("<b>[Ch3LampEndingTest]</b>\n" + text);
            try
            {
                Directory.CreateDirectory(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports");
                File.WriteAllText($@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Ch3Lamp_Case_{testCase}.txt",
                                  text, new UTF8Encoding(false));
            }
            catch { }

            if (!Application.isEditor) Application.Quit(_fail == 0 ? 0 : 1);
        }
    }
}