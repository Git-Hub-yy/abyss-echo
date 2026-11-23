using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// S26 第二章流程测试：抽水 → 开 D2→L2 门 → 频率 → 开 E4 门。
    /// </summary>
    public class AutoTest_S26 : MonoBehaviour
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
            _log.AppendLine("ABYSS ECHO · S26 第二章流程测试");
            _log.AppendLine("=====================================");

            AbyssEcho.Save.SaveSystem.Delete();
            PlayerPrefs.DeleteAll();
            yield return null;
            yield return null;
            yield return new WaitForSeconds(0.6f);

            // ── 组件存在 ──
            var pump = FindFirstObjectByType<AbyssEcho.Puzzle.WaterPumpSwitch>();
            var freq = FindFirstObjectByType<AbyssEcho.Puzzle.FrequencyConsole>();

            AbyssEcho.Interaction.DoorInteractable doorD2L2 = null;
            AbyssEcho.Interaction.DoorInteractable doorE4 = null;
            foreach (var d in FindObjectsByType<AbyssEcho.Interaction.DoorInteractable>(FindObjectsSortMode.None))
            {
                if (d.name == "Door_D2_L2") doorD2L2 = d;
                if (d.name == "Door_E4") doorE4 = d;
            }

            Check("T1 关键组件都存在",
                  pump != null && freq != null && doorD2L2 != null && doorE4 != null,
                  $"水泵={pump != null} 频率={freq != null} D2_L2={doorD2L2 != null} E4={doorE4 != null}");
            if (pump == null || freq == null || doorD2L2 == null || doorE4 == null) { Finish(); yield break; }

            // ── T2 灯光充足 ──
            var lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            int on = 0;
            foreach (var l in lights) if (l.enabled) on++;
            Check("T2 第二章灯光充足", on >= 30, $"共 {lights.Length} 盏，亮 {on} 盏（要求 ≥30）");

            // ── T3 锁定状态 ──
            Check("T3 两扇门都锁着且不可硬开",
                  doorD2L2.locked && !doorD2L2.unlockableByInteraction &&
                  doorE4.locked && !doorE4.unlockableByInteraction,
                  $"D2_L2={doorD2L2.locked}/{doorD2L2.unlockableByInteraction} E4={doorE4.locked}/{doorE4.unlockableByInteraction}");

            // ── T4 泵关联门 ──
            Check("T4 抽水泵关联了 D2_L2 门",
                  pump.doorsToUnlock != null && pump.doorsToUnlock.Length >= 1,
                  $"关联 {(pump.doorsToUnlock != null ? pump.doorsToUnlock.Length : 0)} 扇");

            // ── T5 锁着的门按 E 打不开 ──
            doorD2L2.OnInteract(null);
            yield return null;
            Check("T5 锁着的水压门按 E 打不开", doorD2L2.locked && !doorD2L2.IsOpen,
                  $"locked={doorD2L2.locked} IsOpen={doorD2L2.IsOpen}");

            // ── T6 抽水后门解锁 ──
            pump.OnInteract(null);
            yield return new WaitForSeconds(4.5f);   // pumpDuration 3.5s + 余量
            Check("T6 抽水后 D2_L2 门解锁", !doorD2L2.locked,
                  $"IsDrained={pump.IsDrained} Door.locked={doorD2L2.locked}");

            // ── T7 水压门能打开且门洞畅通 ──
            doorD2L2.OnInteract(null);
            yield return new WaitForSeconds(2.5f);
            bool open = doorD2L2.IsOpen;

            Vector3 dp = doorD2L2.transform.position;
            bool clear = true;
            for (float y = 0.5f; y <= 1.7f; y += 0.4f)
            {
                for (float x = -0.8f; x <= 0.8f; x += 0.4f)
                {
                    var hits = Physics.OverlapSphere(new Vector3(x, y, dp.z - 0.3f), 0.2f, ~0, QueryTriggerInteraction.Ignore);
                    foreach (var h in hits)
                        if (h != null && h.gameObject.name == "Door_D2_L2") clear = false;
                }
            }
            Check("T7 水压门能打开且门洞畅通", open && clear,
                  $"IsOpen={open} 门洞畅通={clear}");

            // ── T8 频率错误不解锁 ──
            freq.Submit(999f);
            yield return null;
            Check("T8 错误频率不解锁 E4", doorE4.locked, $"E4.locked={doorE4.locked}");

            // ── T9 正确频率解锁 E4 ──
            bool ok = freq.Submit(freq.correctFrequency);
            yield return null;
            Check("T9 正确频率(220)解锁 E4", !doorE4.locked,
                  $"提交={ok} E4.locked={doorE4.locked}");

            // ── T10 E4 门能打开 ──
            doorE4.OnInteract(null);
            yield return new WaitForSeconds(2.5f);
            Check("T10 E4 门能打开", doorE4.IsOpen, $"IsOpen={doorE4.IsOpen}");

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
            Debug.Log("<b>[S26Test]</b>\n" + text);
            try
            {
                string dir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports"));
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "S26_AutoTestResult.txt"), text, new UTF8Encoding(false));
            }
            catch { }
            if (!Application.isEditor) Application.Quit(_fail == 0 ? 0 : 1);
        }
    }
}