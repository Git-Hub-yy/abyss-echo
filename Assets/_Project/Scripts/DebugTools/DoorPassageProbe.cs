using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// 复核：开门后玩家能否真正穿过门洞。
    ///
    /// ⚠️ 上一版探测的错误：
    ///   我在"门的 transform.position"做探测，但门滑走后那个位置
    ///   本来就该是墙段（South_B1_SegEnd / Seg0 是门洞两侧的墙）。
    ///   正确做法是沿**门洞的通行方向**逐点扫描。
    /// </summary>
    public class DoorPassageProbe : MonoBehaviour
    {
        public bool runOnStart = true;
        public int forceChapter = 0;

        private readonly StringBuilder _log = new StringBuilder();

        private void Start()
        {
            if (runOnStart) StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            if (forceChapter >= 1 && forceChapter <= 5)
                AbyssEcho.LevelGen.ChapterLayout.Current =
                    (AbyssEcho.LevelGen.ChapterLayout.Chapter)forceChapter;

            yield return new WaitForSeconds(0.8f);

            _log.AppendLine("===== 开门后通行性复核 =====");
            _log.AppendLine();

            AbyssEcho.Interaction.DoorInteractable a3 = null;
            foreach (var d in FindObjectsByType<AbyssEcho.Interaction.DoorInteractable>(FindObjectsSortMode.None))
                if (d.name.Contains("A3_B")) a3 = d;

            if (a3 != null) yield return TestDoor(a3, "Door_A3_B", 0f, -2.00f, new Vector3(0f, 0f, 1f));

            AbyssEcho.Interaction.DoorInteractable c2 = null;
            foreach (var d in FindObjectsByType<AbyssEcho.Interaction.DoorInteractable>(FindObjectsSortMode.None))
                if (d.name.Contains("C2")) c2 = d;

            if (c2 != null) yield return TestDoor(c2, "Door_C2", 8.40f, 35.00f, new Vector3(1f, 0f, 0f));

            var kp = FindFirstObjectByType<AbyssEcho.Puzzle.KeypadPanel>();
            if (kp != null) { kp.Submit(kp.correctCode); yield return null; }

            AbyssEcho.Interaction.DoorInteractable dg = null;
            foreach (var d in FindObjectsByType<AbyssEcho.Interaction.DoorInteractable>(FindObjectsSortMode.None))
                if (d.name.Contains("D_Gate")) dg = d;

            if (dg != null) yield return TestDoor(dg, "Door_D_Gate", 0f, 40.00f, new Vector3(0f, 0f, 1f));

            Dump();
        }

        private IEnumerator TestDoor(AbyssEcho.Interaction.DoorInteractable door, string tag,
                                     float cx, float cz, Vector3 passageDir)
        {
            _log.AppendLine($"═════ {tag} ═════");
            _log.AppendLine($"  初始: locked={door.locked} IsOpen={door.IsOpen} slideDistance={door.slideDistance}");

            if (door.locked)
            {
                door.locked = false;
                _log.AppendLine($"  → 强制解锁（模拟谜题完成）locked={door.locked}");
            }

            door.OnInteract(gameObject);
            yield return new WaitForSeconds(1.5f);

            _log.AppendLine($"  开门后: IsOpen={door.IsOpen} 位置={door.transform.position}");
            _log.AppendLine($"  通行方向 = {passageDir}，沿 ±1.5 米逐点扫描（球半径 0.32 = 玩家半径）：");

            int blockedPoints = 0;
            for (float t = -1.5f; t <= 1.51f; t += 0.25f)
            {
                Vector3 basePos = new Vector3(cx, 0.9f, cz) + passageDir * t;

                var hits = Physics.OverlapSphere(basePos, 0.32f, ~0, QueryTriggerInteraction.Ignore);
                string names = "";
                int solid = 0;
                foreach (var h in hits)
                {
                    if (h == null) continue;
                    if (h is CharacterController) continue;
                    if (h.bounds.max.y < 0.4f) continue;
                    if (h.bounds.min.y > 2.05f) continue;

                    var di = h.GetComponentInParent<AbyssEcho.Interaction.DoorInteractable>();
                    if (di != null && di.IsOpen) continue;

                    solid++;
                    names += h.gameObject.name + " ";
                }

                if (solid > 0)
                {
                    blockedPoints++;
                    _log.AppendLine($"      t={t,5:F2} ❌ {solid} 个 [{names.Trim()}]");
                }
                else
                {
                    _log.AppendLine($"      t={t,5:F2} ✓ 畅通");
                }
            }

            _log.AppendLine($"  结果: {(blockedPoints == 0 ? "✓ 整条通道畅通" : $"⚠️ {blockedPoints} 个采样点被挡")}");
            _log.AppendLine();
        }

        private void Dump()
        {
            string text = _log.ToString();
            Debug.Log("<b>[DoorPassageProbe]</b>\n" + text);

            var di = new DirectoryInfo(Application.dataPath);
            for (int hop = 0; hop < 6 && di != null; hop++)
            {
                string reports = Path.Combine(di.FullName, "Reports");
                if (Directory.Exists(reports))
                {
                    try { File.WriteAllText(Path.Combine(reports, "DoorPassageProbe.txt"), text, new UTF8Encoding(false)); break; }
                    catch { }
                }
                di = di.Parent;
            }

            if (!Application.isEditor) Application.Quit(0);
        }
    }
}