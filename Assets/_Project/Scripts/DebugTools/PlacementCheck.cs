using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// 验证 Keypad_C2 和 Note_KeypadCode 的位置：玩家是否能在"门打开前"看到并交互。
    /// </summary>
    public class PlacementCheck : MonoBehaviour
    {
        public bool runOnStart = true;
        private readonly StringBuilder _log = new StringBuilder();

        private void Start()
        {
            if (runOnStart) StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            yield return new WaitForSeconds(0.8f);

            _log.AppendLine("=== 关键交互物位置验证 ===");
            _log.AppendLine();

            // ── 门禁键盘 ──
            var keypad = FindFirstObjectByType<AbyssEcho.Puzzle.KeypadPanel>();
            var doorC2 = FindDoor("Door_C2");

            _log.AppendLine("--- Keypad_C2 ---");
            if (keypad != null)
            {
                Vector3 kp = keypad.transform.position;
                _log.AppendLine($"  位置 = {kp}");
                if (doorC2 != null)
                {
                    Vector3 dp = doorC2.transform.position;
                    float dist = Vector3.Distance(kp, dp);
                    _log.AppendLine($"  到 Door_C2 的距离 = {dist:F2} 米");
                    _log.AppendLine($"  在门的哪一侧：键盘x={kp.x:F2} 门x={dp.x:F2} → " +
                                    (kp.x < dp.x ? "C1 一侧（玩家可达）✅" : "C2 一侧（被门挡住）❌"));
                }

                // 模拟玩家从 C1 内走向键盘
                Vector3 playerEye = kp + new Vector3(-1.4f, 0.35f, 0.6f);
                Vector3 dir = (kp - playerEye).normalized;
                if (Physics.SphereCast(playerEye, 0.12f, dir, out var hit, 2.6f,
                                       ~0, QueryTriggerInteraction.Collide))
                {
                    var it = hit.collider.GetComponentInParent<AbyssEcho.Interaction.IInteractable>();
                    _log.AppendLine($"  从 C1 内看向键盘: 命中 {hit.collider.name} → " +
                                    $"{(it != null ? it.GetType().Name + " ✅ 可交互" : "null ❌")}");
                }
                else
                {
                    _log.AppendLine("  ❌ 从 C1 内看向键盘：射线什么都没打到");
                }
            }
            else _log.AppendLine("  ❌ 找不到 KeypadPanel");
            _log.AppendLine();

            // ── 便条 ──
            var note = FindFirstObjectByType<AbyssEcho.Narrative.ReadableNote>();
            _log.AppendLine("--- Note_KeypadCode ---");
            if (note != null)
            {
                Vector3 np = note.transform.position;
                _log.AppendLine($"  位置 = {np}（从 0.92 抬高到 1.25）");
                _log.AppendLine($"  缩放 = {note.transform.localScale}");
                var col = note.GetComponent<BoxCollider>();
                if (col != null)
                {
                    Vector3 ws = Vector3.Scale(col.size, note.transform.lossyScale);
                    _log.AppendLine($"  碰撞体世界尺寸 = ({ws.x:F2}, {ws.y:F2}, {ws.z:F2})");
                }

                // 模拟玩家站在 1.5 米外，平视
                Vector3 eye = np + new Vector3(0f, 0.45f, -1.5f);
                Vector3 dir = (np - eye).normalized;
                if (Physics.SphereCast(eye, 0.12f, dir, out var hit, 2.6f,
                                       ~0, QueryTriggerInteraction.Collide))
                {
                    var it = hit.collider.GetComponentInParent<AbyssEcho.Interaction.IInteractable>();
                    _log.AppendLine($"  从 1.5 米外平视: 命中 {hit.collider.name} → " +
                                    $"{(it != null ? it.GetType().Name + " ✅" : "null ❌")}");
                }
                else _log.AppendLine("  ❌ 平视看不到便条");
            }
            else _log.AppendLine("  ❌ 找不到 ReadableNote");
            _log.AppendLine();

            // ── 门的状态 ──
            _log.AppendLine("--- 门状态 ---");
            foreach (var d in FindObjectsByType<AbyssEcho.Interaction.DoorInteractable>(FindObjectsSortMode.None))
                _log.AppendLine($"  {d.name}: locked={d.locked} 位置={d.transform.position} " +
                                $"提示=\"{d.lockedPrompt}\"");

            Flush();
            yield return null;
            if (!Application.isEditor) Application.Quit(0);
        }

        private AbyssEcho.Interaction.DoorInteractable FindDoor(string n)
        {
            foreach (var d in FindObjectsByType<AbyssEcho.Interaction.DoorInteractable>(FindObjectsSortMode.None))
                if (d.name == n) return d;
            return null;
        }

        private void Flush()
        {
            string text = _log.ToString();
            Debug.Log("<b>[PlacementCheck]</b>\n" + text);
            try
            {
                string dir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports"));
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "PlacementCheck.txt"), text, new UTF8Encoding(false));
            }
            catch { }
        }
    }
}