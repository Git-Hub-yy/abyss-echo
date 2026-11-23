using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// S71：验证第四章「进入核心腔」目标能否正确完成。
    ///
    /// 玩家反馈："进入核心舱之后，右上角的任务没有显示完成"
    ///
    /// 测试步骤：
    ///   T1 初始时目标应未完成
    ///   T2 把玩家传送到 G7 核心腔内部
    ///   T3 等待 HUD 刷新，目标应变完成
    ///   T4 走出核心腔后目标应**保持完成**（锁存，不撤销）
    /// </summary>
    public class Ch4ObjectiveTest : MonoBehaviour
    {
        public bool runOnStart = true;
        private readonly StringBuilder _log = new StringBuilder();
        private int _pass, _fail;
        private static readonly BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private void Start()
        {
            if (runOnStart) StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            AbyssEcho.LevelGen.ChapterLayout.Current =
                AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter4;
            yield return new WaitForSeconds(1.0f);

            _log.AppendLine("══════ 第四章「进入核心腔」目标验证 ══════");

            var hud = FindFirstObjectByType<AbyssEcho.UI.ObjectiveHUD>();
            var motor = FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
            if (hud == null || motor == null)
            {
                _log.AppendLine($"❌ 缺组件 hud={hud != null} motor={motor != null}");
                Dump(); yield break;
            }

            var look = FindFirstObjectByType<AbyssEcho.Player.PlayerLook>();
            if (look != null) look.enabled = false;

            var cc = motor.GetComponent<CharacterController>();

            // 反射读私有字段
            var t = typeof(AbyssEcho.UI.ObjectiveHUD);
            var reachedF = t.GetField("_reachedCoreChamber", BF);
            var objF = t.GetField("_objectives", BF);

            // ══ T1 初始状态 ══
            _log.AppendLine();
            _log.AppendLine("──── T1 初始状态 ────");
            _log.AppendLine($"  玩家位置 = {motor.transform.position}");
            _log.AppendLine($"  _reachedCoreChamber = {reachedF?.GetValue(hud)}");
            DumpObjectives(objF, hud);

            bool init = reachedF != null && (bool)reachedF.GetValue(hud);
            Check("T1 初始未完成", !init, $"reached={init}");

            // ══ T2 传送进核心腔 ══
            _log.AppendLine();
            _log.AppendLine("──── T2 传送进核心腔 G7 ────");
            Vector3 insidePos = new Vector3(0f, 0.05f, 16f);   // G7 中心
            if (cc != null) cc.enabled = false;
            motor.transform.position = insidePos;
            if (cc != null) cc.enabled = true;
            _log.AppendLine($"  传送到 {insidePos}（G7 中心，范围 X[-10,10] Z[7,25]）");

            // 等 HUD 刷新（0.5 秒一次，等 2 秒保险）
            yield return new WaitForSeconds(2.0f);

            bool after = reachedF != null && (bool)reachedF.GetValue(hud);
            _log.AppendLine($"  _reachedCoreChamber = {after}");
            DumpObjectives(objF, hud);
            Check("T2 进入后目标完成", after, $"reached={after}");

            // ══ T3 走出核心腔，验证锁存 ══
            _log.AppendLine();
            _log.AppendLine("──── T3 走出核心腔（验证锁存不撤销）────");
            Vector3 outsidePos = new Vector3(-19f, 0.05f, 0f);  // G5 内
            if (cc != null) cc.enabled = false;
            motor.transform.position = outsidePos;
            if (cc != null) cc.enabled = true;
            _log.AppendLine($"  传送到 {outsidePos}（G5 内，范围外）");

            yield return new WaitForSeconds(2.0f);

            bool stillDone = reachedF != null && (bool)reachedF.GetValue(hud);
            _log.AppendLine($"  _reachedCoreChamber = {stillDone}");
            DumpObjectives(objF, hud);
            Check("T3 离开后仍保持完成", stillDone, $"reached={stillDone}");

            // ══ T4 心理模拟：从 G5 北门走到核心腔，检查路径 ──
            _log.AppendLine();
            _log.AppendLine("──── T4 路径落地检查（G5 北门 → G7）────");
            string[] waypoints = {
                "(-19, 0, 3)  G5 内", "(-19, 0, 5)  G5 北门", "(-19, 0, 7)  过渡区",
                "(-16, 0, 8)  过渡区西", "(-14, 0, 8)  过渡区东", "(-12, 0, 8)  连廊",
                "(-8, 0, 8)   G7 内"
            };
            var pts = new[] {
                new Vector3(-19, 0, 3), new Vector3(-19, 0, 5), new Vector3(-19, 0, 7),
                new Vector3(-16, 0, 8), new Vector3(-14, 0, 8), new Vector3(-12, 0, 8),
                new Vector3(-8, 0, 8)
            };
            for (int i = 0; i < pts.Length; i++)
            {
                var hits = Physics.RaycastAll(pts[i] + Vector3.up * 5f, Vector3.down, 20f);
                float top = -999f;
                string name = "无";
                foreach (var h in hits)
                {
                    if (h.collider == null) continue;
                    var b = h.collider.bounds;
                    if (pts[i].x < b.min.x || pts[i].x > b.max.x) continue;
                    if (pts[i].z < b.min.z || pts[i].z > b.max.z) continue;
                    if (b.max.y > 1.0f || b.max.y < -1.0f) continue;
                    if (h.point.y > top) { top = h.point.y; name = h.collider.gameObject.name; }
                }
                bool ok = top > -1f;
                _log.AppendLine($"  {waypoints[i],-26} 地面={top:F2} {name,-30} {(ok ? "✅" : "❌ 虚空")}");
                if (i == 3 || i == 4)
                    Check($"T4 路径点 {i} 有地面", ok, $"{name}");
            }

            Dump();
        }

        private void DumpObjectives(FieldInfo objF, object hud)
        {
            if (objF == null) return;
            var list = objF.GetValue(hud) as System.Collections.IEnumerable;
            if (list == null) return;
            foreach (var item in list)
            {
                var itemType = item.GetType();
                var textF = itemType.GetField("Item1");
                var doneF = itemType.GetField("Item2");
                if (textF == null || doneF == null) continue;
                string text = textF.GetValue(item) as string ?? "?";
                bool done = (bool)doneF.GetValue(item);
                _log.AppendLine($"      [{(done ? "✓" : " ")}] {text}");
            }
        }

        private void Check(string name, bool ok, string detail)
        {
            if (ok) { _pass++; _log.AppendLine($"[通过] {name}  {detail}"); }
            else { _fail++; _log.AppendLine($"[失败] {name}  {detail}"); }
        }

        private void Dump()
        {
            _log.AppendLine();
            _log.AppendLine("=====================================");
            _log.AppendLine($"通过 {_pass}，失败 {_fail}");

            string text = _log.ToString();
            Debug.Log("<b>[Ch4ObjectiveTest]</b>\n" + text);
            try
            {
                Directory.CreateDirectory(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports");
                File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Ch4ObjectiveTest.txt",
                                  text, new UTF8Encoding(false));
            }
            catch { }

            if (!Application.isEditor) Application.Quit(_fail == 0 ? 0 : 1);
        }
    }
}