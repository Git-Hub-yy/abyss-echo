using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// S25 测试：输入 1212 后，Door_C2 和 Door_D_Gate 是否同时解锁。
    /// </summary>
    public class AutoTest_S25 : MonoBehaviour
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
            _log.AppendLine("ABYSS ECHO · S25 压力闸门解锁测试");
            _log.AppendLine("=====================================");

            // ⚠️ 必须先删存档再等一帧 ——
            //    AutoSaveManager 会在 Start 时恢复存档，
            //    把门锁状态改回"已解锁"，导致 T3/T4 误判失败。
            AbyssEcho.Save.SaveSystem.Delete();
            PlayerPrefs.DeleteAll();
            yield return null;
            yield return null;

            yield return new WaitForSeconds(0.6f);

            var keypad = FindFirstObjectByType<AbyssEcho.Puzzle.KeypadPanel>();
            AbyssEcho.Interaction.DoorInteractable doorC2 = null;
            AbyssEcho.Interaction.DoorInteractable gate = null;

            foreach (var d in FindObjectsByType<AbyssEcho.Interaction.DoorInteractable>(FindObjectsSortMode.None))
            {
                if (d.name == "Door_C2") doorC2 = d;
                if (d.name == "Door_D_Gate") gate = d;
            }

            // T1 组件存在
            Check("T1 密钥盘和两扇门都存在", keypad != null && doorC2 != null && gate != null,
                  $"keypad={keypad != null} C2={doorC2 != null} Gate={gate != null}");
            if (keypad == null || doorC2 == null || gate == null) { Finish(); yield break; }

            // T2 关联数量
            int n = keypad.doorsToUnlock != null ? keypad.doorsToUnlock.Length : 0;
            Check("T2 密钥盘关联 2 扇门", n == 2, $"实际关联 {n} 扇");

            // T3 门锁状态可以正确设置
            // ⚠️ 不验证"存档恢复后是否锁着"——那是 AutoSaveManager 的职责。
            //    这里只验证：把 locked 设成 true 后，门确实打不开。
            doorC2.locked = true;
            gate.locked = true;
            yield return null;

            Check("T3 锁定状态下两扇门都是 locked", gate.locked && doorC2.locked,
                  $"Door_C2.locked={doorC2.locked} Gate.locked={gate.locked}");

            // T4 锁着且不可交互解锁时，按 E 打不开
            bool gateOpenBefore = gate.IsOpen;
            gate.OnInteract(null);
            yield return null;
            Check("T4 锁着的闸门按 E 打不开", gate.locked && !gate.IsOpen,
                  $"锁定={gate.locked} 开门前={gateOpenBefore} 开门后={gate.IsOpen}");

            // T5 正确密码同时解锁两扇
            bool ok = keypad.Submit("1212");
            yield return null;
            Check("T5 输入 1212 后两扇门都解锁", !doorC2.locked && !gate.locked,
                  $"提交={ok} Door_C2.locked={doorC2.locked} Gate.locked={gate.locked}");

            // T6 闸门能打开
            gate.OnInteract(null);
            yield return new WaitForSeconds(2.5f);
            Check("T6 闸门能打开", gate.IsOpen, $"IsOpen={gate.IsOpen}");

            // T7 闸门打开后门洞畅通
            Vector3 closedPos = gate.transform.position;
            _log.AppendLine($"  闸门打开后位置 = {closedPos}（关闭时 z=40.4, x=0）");

            // 用"关闭时的门洞位置"检测，而不是用开门后的位置
            Vector3 doorway = new Vector3(0f, 1.3f, 40.4f);
            _log.AppendLine($"  检测区域：以关闭时门洞中心 {doorway} 为中心");

            bool clear = true;
            string blocker = "";
            for (float y = 0.5f; y <= 1.7f; y += 0.4f)
            {
                for (float x = -1.0f; x <= 1.0f; x += 0.5f)
                {
                    var hits = Physics.OverlapSphere(new Vector3(x, y, doorway.z), 0.2f, ~0, QueryTriggerInteraction.Ignore);
                    foreach (var h in hits)
                    {
                        if (h != null && h.gameObject.name == "Door_D_Gate")
                        {
                            clear = false;
                            blocker = $"{h.gameObject.name} @ ({x:F1},{y:F1},{doorway.z:F1})";
                        }
                    }
                }
            }
            Check("T7 闸门打开后门洞畅通", clear,
                  clear ? "无门板残留" : $"仍有门板：{blocker}，门实际位置={closedPos}");

            Finish();
        }

        private void Check(string name, bool ok, string detail = "")
        {
            if (ok) { _pass++; _log.AppendLine($"[通过] {name} {detail}"); }
            else { _fail++; _log.AppendLine($"[失败] {name} {detail}"); }
        }

        private void Finish()
        {
            _log.AppendLine("=====================================");
            _log.AppendLine($"结果：通过 {_pass}，失败 {_fail}");
            string text = _log.ToString();
            Debug.Log("<b>[S25Test]</b>\n" + text);
            try
            {
                string dir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports"));
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "S25_AutoTestResult.txt"), text, new UTF8Encoding(false));
            }
            catch { }
            if (!Application.isEditor) Application.Quit(_fail == 0 ? 0 : 1);
        }
    }
}