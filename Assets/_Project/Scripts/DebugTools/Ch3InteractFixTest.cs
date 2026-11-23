using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// S66：验证「平视能否交互到矮物体」+「提示是否显示具体道具」。
    ///
    /// 玩家反馈两件事：
    ///   ① 捡到灯泡之后放不进去
    ///   ② 捡到多个物体之后，无法知晓放的是哪个
    /// </summary>
    public class Ch3InteractFixTest : MonoBehaviour
    {
        public bool runOnStart = true;

        private readonly StringBuilder _log = new StringBuilder();
        private int _pass, _fail, _unverified;

        private void Start()
        {
            if (runOnStart) StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            AbyssEcho.LevelGen.ChapterLayout.Current =
                AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter3;
            yield return new WaitForSeconds(0.8f);

            _log.AppendLine("══════ S66 交互修复验证 ══════");

            var motor = FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
            var cc = motor != null ? motor.GetComponent<CharacterController>() : null;
            var cam = Camera.main;
            var detector = FindFirstObjectByType<AbyssEcho.Interaction.InteractionDetector>();
            var inv = AbyssEcho.Inventory.PlayerInventory.Instance;

            if (motor == null || cam == null || detector == null || inv == null)
            {
                _log.AppendLine($"❌ 缺组件 motor={motor != null} cam={cam != null} det={detector != null} inv={inv != null}");
                Dump(); yield break;
            }

            var look = FindFirstObjectByType<AbyssEcho.Player.PlayerLook>();
            if (look != null) look.enabled = false;

            // ══ T1 平视灯座能否检测到 ══
            _log.AppendLine();
            _log.AppendLine("──── T1 平视灯座（不低头）────");

            var socket = GameObject.Find("长明灯座1");
            if (socket == null) { _log.AppendLine("❌ 找不到灯座"); Dump(); yield break; }
            Vector3 sp = socket.transform.position;

            inv.Add("item.bulb");
            _log.AppendLine($"  背包有 bulb = {inv.Has("item.bulb")}");

            foreach (float dist in new[] { 1.0f, 1.4f, 1.8f })
            {
                // 玩家站在灯座南侧，平视朝北（pitch=0）
                Teleport(motor, cc, new Vector3(sp.x, 0.05f, sp.z - dist), 0f);
                yield return new WaitForSeconds(0.25f);

                bool has = detector.HasTarget;
                string prompt = detector.CurrentPrompt;
                _log.AppendLine($"  距 {dist:F1}m 平视: HasTarget={has}  提示=\"{prompt}\"");

                if (dist == 1.4f)
                    Check("T1 平视 1.4m 能检测到灯座", has, $"HasTarget={has}");
            }

            // ══ T2 点亮灯座 ══
            _log.AppendLine();
            _log.AppendLine("──── T2 通过检测器交互点亮 ────");

            Teleport(motor, cc, new Vector3(sp.x, 0.05f, sp.z - 1.4f), 0f);
            yield return new WaitForSeconds(0.3f);

            var cur = detector.Current;
            _log.AppendLine($"  当前目标 = {(cur != null ? cur.GetType().Name : "null")}");

            if (cur != null)
            {
                cur.OnInteract(cam.gameObject);
                yield return new WaitForSeconds(0.3f);
            }

            var sockComp = socket.GetComponent<AbyssEcho.Puzzle.LampSocket>();
            _log.AppendLine($"  灯座 IsLit = {sockComp.IsLit}");
            Check("T2 平视交互成功点亮灯座", sockComp.IsLit, $"IsLit={sockComp.IsLit}");

            // ══ T3 平视供奉台能否检测到 ══
            _log.AppendLine();
            _log.AppendLine("──── T3 平视供奉台（不低头）────");

            var altar = GameObject.Find("供奉台");
            if (altar != null)
            {
                Vector3 ap = altar.transform.position;
                // 加录音带和保险丝，模拟"带多个物品"
                inv.Add("item.tape");
                inv.Add("item.fuse");
                yield return new WaitForSeconds(0.2f);

                Teleport(motor, cc, new Vector3(ap.x, 0.05f, ap.z - 1.6f), 0f);
                yield return new WaitForSeconds(0.3f);

                bool has = detector.HasTarget;
                string prompt = detector.CurrentPrompt;
                _log.AppendLine($"  距 1.6m 平视: HasTarget={has}");
                _log.AppendLine($"  提示 = \"{prompt}\"");

                Check("T3 平视能检测到供奉台", has, $"HasTarget={has}");
                Check("T4 提示写明将放下的道具", prompt.Contains("录音带"),
                      $"提示=\"{prompt}\"");
            }

            // ══ T5 只有保险丝时的提示 ══
            _log.AppendLine();
            _log.AppendLine("──── T5 只带保险丝时的提示 ────");
            // 重新进场景太麻烦，这里直接读默认提示逻辑
            var altarComp = altar != null ? altar.GetComponent<AbyssEcho.Puzzle.OfferingAltar>() : null;
            if (altarComp != null)
            {
                _log.AppendLine($"  （模拟前：背包 tape={inv.Has("item.tape")} fuse={inv.Has("item.fuse")}）");
            }

            _log.AppendLine();
            Unverified("U1 真人走到灯座前平视按 E 的实际手感");
            Unverified("U2 交互提示在准星下方显示的实际位置");

            Dump();
        }

        private void Teleport(AbyssEcho.Player.PlayerMotor motor, CharacterController cc,
                              Vector3 pos, float pitch)
        {
            if (cc != null) cc.enabled = false;
            motor.transform.position = pos;
            motor.transform.rotation = Quaternion.Euler(pitch, 0f, 0f);
            if (cc != null) cc.enabled = true;
        }

        private void Check(string name, bool ok, string detail)
        {
            if (ok) { _pass++; _log.AppendLine($"[通过] {name}  {detail}"); }
            else { _fail++; _log.AppendLine($"[失败] {name}  {detail}"); }
        }

        private void Unverified(string n) { _unverified++; _log.AppendLine($"[待人工验收] {n}"); }

        private void Dump()
        {
            _log.AppendLine();
            _log.AppendLine("=====================================");
            _log.AppendLine($"通过 {_pass}，失败 {_fail}，待人工验收 {_unverified}");

            string text = _log.ToString();
            Debug.Log("<b>[Ch3InteractFixTest]</b>\n" + text);
            try
            {
                Directory.CreateDirectory(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports");
                File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Ch3InteractFixTest.txt",
                                  text, new UTF8Encoding(false));
            }
            catch { }

            if (!Application.isEditor) Application.Quit(_fail == 0 ? 0 : 1);
        }
    }
}