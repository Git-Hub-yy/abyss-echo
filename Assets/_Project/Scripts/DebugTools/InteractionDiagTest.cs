using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// 阶段二：交互检测射线诊断。
    ///
    /// 为什么检测器返回 null？
    /// 逐层排查：
    ///   ① 相机位置与朝向
    ///   ② 射线打到的第一个碰撞体是谁
    ///   ③ 那个碰撞体上有没有 InteractableBase
    ///   ④ interactDistance / castRadius / hitLayers 配置
    /// </summary>
    public class InteractionDiagTest : MonoBehaviour
    {
        private readonly StringBuilder _log = new StringBuilder();

        public IEnumerator RunAll()
        {
            _log.AppendLine("══════ 交互检测射线诊断 ══════");
            _log.AppendLine();

            var card = FindFirstObjectByType<AbyssEcho.UI.ChapterTitleCard>();
            if (card != null && card.enabled)
            {
                float w = 0f;
                while (card != null && card.enabled && w < 12f) { yield return new WaitForSeconds(0.25f); w += 0.25f; }
            }
            yield return new WaitForSeconds(0.5f);

            var detector = FindFirstObjectByType<AbyssEcho.Interaction.InteractionDetector>();
            var motor = FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
            var cam = Camera.main;

            if (detector == null || motor == null || cam == null)
            {
                _log.AppendLine($"❌ detector={detector != null} motor={motor != null} cam={cam != null}");
                Dump(); yield break;
            }

            // ── 检测器配置 ──
            var dt = detector.GetType();
            var BF = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
            _log.AppendLine("──── InteractionDetector 配置 ────");
            foreach (var fn in new[] { "interactDistance", "castRadius", "hitLayers", "debugDrawRay", "_cam" })
            {
                var f = dt.GetField(fn, BF);
                if (f == null) { _log.AppendLine($"  {fn}: (字段不存在)"); continue; }
                object v = f.GetValue(detector);
                if (fn == "hitLayers") v = ((LayerMask)v).value;
                _log.AppendLine($"  {fn} = {v}");
            }
            _log.AppendLine();

            // ── 相机 ──
            _log.AppendLine("──── 相机 ────");
            _log.AppendLine($"  Camera.main = {cam.name}");
            _log.AppendLine($"  position = {cam.transform.position}");
            _log.AppendLine($"  forward  = {cam.transform.forward}");
            _log.AppendLine($"  masking = {(cam.cullingMask == -1 ? "Everything" : cam.cullingMask.ToString())}");
            _log.AppendLine();

            // ── 找一个可交互物 ──
            var all = FindObjectsByType<AbyssEcho.Interaction.InteractableBase>(FindObjectsSortMode.None);
            AbyssEcho.Interaction.InteractableBase target = null;
            foreach (var a in all)
                if (a != null && a.CanInteract) { target = a; break; }

            if (target == null) { _log.AppendLine("❌ 无可交互物"); Dump(); yield break; }

            Vector3 tp = target.transform.position;
            _log.AppendLine("──── 目标 ────");
            _log.AppendLine($"  {target.gameObject.name} @ {tp}");
            _log.AppendLine($"  CanInteract = {target.CanInteract}");
            _log.AppendLine($"  layer = {LayerMask.LayerToName(target.gameObject.layer)}({target.gameObject.layer})");

            var col = target.GetComponent<Collider>();
            _log.AppendLine($"  碰撞体: {(col != null ? col.GetType().Name + " isTrigger=" + col.isTrigger : "❌ 无碰撞体")}");
            if (col != null)
                _log.AppendLine($"  bounds = {col.bounds.center} / {col.bounds.size}");
            _log.AppendLine();

            // ── 把玩家放到目标前 ──
            var cc = motor.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            Vector3 standPos = new Vector3(tp.x, 0.05f, tp.z - 1.2f);
            motor.transform.position = standPos;
            Vector3 face = new Vector3(tp.x - standPos.x, 0f, tp.z - standPos.z);
            motor.transform.rotation = Quaternion.LookRotation(face);
            if (cc != null) cc.enabled = true;
            Physics.SyncTransforms();

            // 强制相机对准
            cam.transform.LookAt(tp);
            yield return new WaitForSeconds(0.8f);

            _log.AppendLine("──── 对齐后 ────");
            _log.AppendLine($"  玩家位置 = {motor.transform.position}");
            _log.AppendLine($"  相机位置 = {cam.transform.position}");
            _log.AppendLine($"  相机前向 = {cam.transform.forward}");
            _log.AppendLine($"  目标方向 = {(tp - cam.transform.position).normalized}");
            _log.AppendLine();

            // ── 手动射线 ──
            _log.AppendLine("──── 射线测试 ────");
            Vector3 origin = cam.transform.position;
            Vector3 dir = cam.transform.forward;

            var hits = Physics.RaycastAll(origin, dir, 10f, ~0, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            _log.AppendLine($"  RaycastAll 命中 {hits.Length} 个：");
            int shown = 0;
            foreach (var h in hits)
            {
                if (shown++ >= 10) break;
                var it = h.collider.GetComponentInParent<AbyssEcho.Interaction.InteractableBase>();
                _log.AppendLine($"    {h.distance:F2}m  {h.collider.gameObject.name}  " +
                                $"layer={LayerMask.LayerToName(h.collider.gameObject.layer)}  " +
                                $"trigger={h.collider.isTrigger}  " +
                                $"interactable={(it != null ? it.GetType().Name : "—")}");
            }

            // 直接朝目标打
            Vector3 toTarget = tp - origin;
            var directHits = Physics.RaycastAll(origin, toTarget.normalized, toTarget.magnitude + 0.5f, ~0, QueryTriggerInteraction.Collide);
            System.Array.Sort(directHits, (a, b) => a.distance.CompareTo(b.distance));
            _log.AppendLine();
            _log.AppendLine($"  直瞄目标（距离 {toTarget.magnitude:F2}m）命中 {directHits.Length} 个：");
            shown = 0;
            foreach (var h in directHits)
            {
                if (shown++ >= 6) break;
                var it = h.collider.GetComponentInParent<AbyssEcho.Interaction.InteractableBase>();
                _log.AppendLine($"    {h.distance:F2}m  {h.collider.gameObject.name}  " +
                                $"interactable={(it != null ? it.GetType().Name : "—")}");
            }

            // ── 调一次检测器 Update 再读 ──
            _log.AppendLine();
            _log.AppendLine("──── 检测器状态 ────");
            var updateM = dt.GetMethod("Update", BF);
            updateM?.Invoke(detector, null);
            yield return null;

            var curF = dt.GetField("_current", BF) ?? dt.GetField("current", BF);
            var promptP = dt.GetProperty("CurrentPrompt", BF) ?? dt.GetProperty("Prompt", BF);
            var currentP = dt.GetProperty("Current", BF);

            object cur = curF?.GetValue(detector) ?? currentP?.GetValue(detector);
            _log.AppendLine($"  _current = {(cur != null ? cur.ToString() : "null")}");
            _log.AppendLine($"  CurrentPrompt = '{promptP?.GetValue(detector) ?? "(属性不存在)"}'");

            // 通过反射调用 TryFind 看看返回什么
            var findM = dt.GetMethod("FindInteractable", BF) ?? dt.GetMethod("DetectInteractable", BF);
            if (findM != null)
            {
                object r = findM.Invoke(detector, null);
                _log.AppendLine($"  {findM.Name}() = {(r != null ? r.ToString() : "null")}");
            }
            else
            {
                _log.AppendLine("  (找不到内部的查找方法)");
            }

            Dump();
        }

        private void Dump()
        {
            string text = _log.ToString();
            Debug.Log("<b>[InteractionDiagTest]</b>\n" + text);
            try
            {
                Directory.CreateDirectory(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Final_Test");
                File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Final_Test\02_Raw_interdiag.txt",
                                  text, new UTF8Encoding(false));
            }
            catch { }
            if (!Application.isEditor) Application.Quit(0);
        }
    }
}