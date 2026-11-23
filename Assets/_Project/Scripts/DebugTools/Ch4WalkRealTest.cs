using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// S59：用**真实 CharacterController 移动**验证 G5 → G7 能否走通。
    ///
    /// ══════════════════ 为什么不用 CapsuleCast ══════════════════
    ///
    ///   CapsuleCast 是"一整段直线扫过去"，跨两块地板时
    ///   胶囊下沿会擦到地板边缘，产生假阳性（明明能走却报被挡）。
    ///
    ///   本测试直接把 PlayerMotor 的 CharacterController 拿过来，
    ///   用 cc.Move() 一帧一帧地走，和玩家实际操作完全一致。
    /// </summary>
    public class Ch4WalkRealTest : MonoBehaviour
    {
        public bool runOnStart = true;

        private readonly StringBuilder _log = new StringBuilder();
        private int _pass, _fail, _unverified;

        private AbyssEcho.Player.PlayerMotor _motor;
        private CharacterController _cc;

        private void Start()
        {
            if (runOnStart) StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            AbyssEcho.LevelGen.ChapterLayout.Current =
                AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter4;
            var ending = FindFirstObjectByType<AbyssEcho.Narrative.ChapterEndingSystem>();
            ending?.GetType().GetMethod("ApplyChapterConfig",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic)?.Invoke(ending, null);

            yield return new WaitForSeconds(1.2f);

            _log.AppendLine("══════ S59 G5→G7 真实行走验证 ══════");
            _log.AppendLine($"[开始] 时间={Time.realtimeSinceStartup:F1}s");

            _motor = FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
            _cc = _motor != null ? _motor.GetComponent<CharacterController>() : null;
            if (_motor == null || _cc == null) { _log.AppendLine("❌ 无玩家"); Dump(); yield break; }

            // 关掉会干扰走路的东西
            _motor.enabled = false;
            var look = FindFirstObjectByType<AbyssEcho.Player.PlayerLook>();
            if (look != null) look.enabled = false;
            foreach (var m in FindObjectsByType<AbyssEcho.Horror.MimicStalker>(FindObjectsSortMode.None))
                m.enabled = false;
            var fv = FindFirstObjectByType<AbyssEcho.Horror.FearVisuals>();
            if (fv != null) fv.enabled = false;

            // ══ 路径点（世界坐标）══
            var waypoints = new[]
            {
                new Vector3(-19f, 0.05f, 0f),      // G5 中心
                new Vector3(-19f, 0.05f, 4f),      // G5 北部
                new Vector3(-19f, 0.05f, 5.8f),    // 出门洞进入过渡区
                new Vector3(-16f, 0.05f, 6.2f),    // 沿过渡区东行
                new Vector3(-13f, 0.05f, 6.2f),    // 过渡区东端
                new Vector3(-11.5f, 0.05f, 7.2f),  // 进连廊
                new Vector3(-11f, 0.05f, 8.3f),    // 连廊中
                new Vector3(-9f, 0.05f, 8.5f),     // 进 G7
                new Vector3(-6f, 0.05f, 10f),      // G7 内
            };

            string[] names = {
                "G5 中心", "G5 北", "门洞外", "过渡区东行", "过渡区东端",
                "进连廊", "连廊中", "进 G7", "G7 内"
            };

            _log.AppendLine();
            _log.AppendLine("──── 逐段真实行走 ────");
            _log.AppendLine($"起点 = {waypoints[0]}");

            // 传送到起点
            Teleport(waypoints[0]);
            yield return new WaitForSeconds(0.3f);

            for (int i = 1; i < waypoints.Length; i++)
            {
                Vector3 from = waypoints[i - 1];
                Vector3 to = waypoints[i];
                float dist = Vector3.Distance(new Vector3(from.x, 0, from.z), new Vector3(to.x, 0, to.z));

                float t = 0f;
                bool stalled = false;
                Vector3 lastPos = _cc.transform.position;
                float stallTimer = 0f;

                // 最多给 8 秒走这一段（同时限制 3000 帧，防止 batchmode 卡死）
                int frameGuard = 0;
                while (t < 8f && frameGuard < 3000)
                {
                    frameGuard++;
                    Vector3 cur = _cc.transform.position;
                    Vector3 target = new Vector3(to.x, cur.y, to.z);
                    Vector3 flat = new Vector3(target.x - cur.x, 0, target.z - cur.z);

                    if (flat.magnitude < 0.35f) break;

                    // 用 CharacterController 移动（和玩家完全一样）
                    float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                    _cc.Move(flat.normalized * 2.5f * dt);
                    t += dt;

                    // 卡住检测
                    if (Vector3.Distance(_cc.transform.position, lastPos) < 0.005f)
                    {
                        stallTimer += dt;
                        if (stallTimer > 1.5f) { stalled = true; break; }
                    }
                    else stallTimer = 0f;
                    lastPos = _cc.transform.position;

                    yield return null;
                }

                Vector3 end = _cc.transform.position;
                float progress = Vector3.Distance(new Vector3(from.x, 0, from.z), new Vector3(end.x, 0, end.z));
                float remain = Vector3.Distance(new Vector3(end.x, 0, end.z), new Vector3(to.x, 0, to.z));

                bool ok = remain < 0.6f && !stalled;
                if (ok) _pass++; else _fail++;

                _log.AppendLine($"  [{i}] {names[i - 1]} → {names[i]}");
                _log.AppendLine($"      目标 {to}  实际到达 ({end.x:F1},{end.y:F2},{end.z:F1})");
                _log.AppendLine($"      段长 {dist:F1}m  走完 {progress:F1}m  剩余 {remain:F1}m  {(stalled ? "⚠️卡住" : "")}  {(ok ? "✅通过" : "❌未到")}");
            }

            // ══ 最终位置检查 ══
            _log.AppendLine();
            Vector3 final = _cc.transform.position;
            _log.AppendLine($"最终位置 = ({final.x:F1},{final.y:F2},{final.z:F1})");
            bool inG7 = final.x > -10.5f && final.z > 7f;
            Check("玩家成功进入 G7", inG7, $"x={final.x:F1} z={final.z:F1}");
            Check("玩家没有掉出地板", final.y > -1f, $"y={final.y:F2}");

            _log.AppendLine();
            Unverified("U1 真人用键鼠走这条路的实际手感");

            Dump();
        }

        private void Teleport(Vector3 pos)
        {
            if (_cc != null) _cc.enabled = false;
            _motor.transform.position = pos;
            _motor.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            if (_cc != null) _cc.enabled = true;
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
            Debug.Log("<b>[Ch4WalkRealTest]</b>\n" + text);

            try
            {
                Directory.CreateDirectory(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports");
                File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Ch4WalkRealTest.txt",
                                  text, new UTF8Encoding(false));
            }
            catch { }

            if (!Application.isEditor) Application.Quit(_fail == 0 ? 0 : 1);
        }
    }
}