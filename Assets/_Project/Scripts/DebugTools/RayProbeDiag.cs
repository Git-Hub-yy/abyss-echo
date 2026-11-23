using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// 精确射线诊断：模拟玩家站在门/便条前 1.5 米，看向它们，
    /// 看 SphereCast 到底命中了什么、能不能解析出 IInteractable。
    /// </summary>
    public class RayProbeDiag : MonoBehaviour
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

            _log.AppendLine("=== 精确射线诊断 ===");
            _log.AppendLine();

            ProbeTarget("Door_C2", new Vector3(7.70f, 1.15f, 35.00f));
            ProbeTarget("Note_KeypadCode", new Vector3(-11.50f, 0.92f, 41.80f));
            ProbeTarget("PowerPanel_A3", new Vector3(-2.60f, 1.35f, -9.25f));
            ProbeTarget("Keypad_C2", new Vector3(8.68f, 1.35f, 35.00f));
            ProbeTarget("Door_A3_B", new Vector3(0f, 1.15f, -2.15f));
            ProbeTarget("Pickup_Fuse", new Vector3(2.20f, 0.95f, -13.00f));

            Flush();
            yield return null;
            if (!Application.isEditor) Application.Quit(0);
        }

        private void ProbeTarget(string name, Vector3 targetPos)
        {
            _log.AppendLine($"--- {name} @ {targetPos} ---");

            // 从目标位置向四周找方向，模拟玩家站在 1.6 米外看过去
            Vector3[] offsets =
            {
                new Vector3(0, 0, -1.6f), new Vector3(0, 0, 1.6f),
                new Vector3(-1.6f, 0, 0), new Vector3(1.6f, 0, 0),
            };

            bool found = false;
            foreach (var off in offsets)
            {
                Vector3 eye = targetPos + off + Vector3.up * 0.55f;   // 眼睛高度约 1.7
                Vector3 dir = (targetPos + Vector3.up * 0.0f) - eye;

                if (Physics.SphereCast(eye, 0.12f, dir.normalized, out var hit, 2.6f,
                                       ~0, QueryTriggerInteraction.Collide))
                {
                    var it = hit.collider.GetComponentInParent<AbyssEcho.Interaction.IInteractable>();
                    string itName = it != null ? it.GetType().Name : "null";

                    if (it != null)
                    {
                        _log.AppendLine($"  从 {off} 方向: 命中 {hit.collider.gameObject.name} " +
                                        $"→ IInteractable={itName}  CanInteract={it.CanInteract}  ✅");
                        found = true;
                        break;
                    }
                    else
                    {
                        _log.AppendLine($"  从 {off} 方向: 命中 {hit.collider.gameObject.name} " +
                                        $"→ IInteractable=null（被挡住了？）");
                    }
                }
                else
                {
                    _log.AppendLine($"  从 {off} 方向: 未命中任何东西");
                }
            }

            if (!found) _log.AppendLine($"  ❌ {name} 在任何方向都无法被探测到！");
            _log.AppendLine();
        }

        private void Flush()
        {
            string text = _log.ToString();
            Debug.Log("<b>[RayProbe]</b>\n" + text);
            try
            {
                string dir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports"));
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "RayProbeDiag.txt"), text, new UTF8Encoding(false));
            }
            catch { }
        }
    }
}