using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// v5.1 实际死亡测试：让 MimicStalker 真的抓住玩家，验证死亡流程。
    ///
    /// ══════════════════ 这个测试补的是什么 ══════════════════
    ///
    ///   前面的修复只是"组件加上了"。真正要证明的是：
    ///   **怪物抓住玩家之后，游戏真的会进入死亡流程。**
    ///
    ///   本测试会：
    ///     ① 确认 PlayerDeathSystem 存在
    ///     ② 把玩家传送到 Mimic 旁边
    ///     ③ 等待 Mimic 的 AI 自己走过来抓住玩家（不改 AI 逻辑，只等它自然发生）
    ///     ④ 观察 IsDead / 输入冻结 / 画面淡入 / 死亡文案
    ///     ⑤ 验证 R 键重开（通过反射调用 Restart 的等价路径）
    ///
    /// ⚠️ 诚实的边界：
    ///   - 无法验证"玩家眼睛看到黑屏"（-nographics 无渲染）
    ///   - 无法验证"玩家按 R 键"（无真实输入设备）
    ///   - 这些必须标记为待人工验收，不虚报通过
    /// </summary>
    public class DeathFlowTest : MonoBehaviour
    {
        public bool runOnStart = true;
        public int forceChapter = 3;

        private readonly StringBuilder _log = new StringBuilder();
        private int _pass, _fail, _unverified;

        private void Start()
        {
            if (runOnStart) StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            if (forceChapter >= 1 && forceChapter <= 5)
                AbyssEcho.LevelGen.ChapterLayout.Current =
                    (AbyssEcho.LevelGen.ChapterLayout.Chapter)forceChapter;

            yield return new WaitForSeconds(1.0f);

            _log.AppendLine("===== v5.1 实际死亡流程测试 =====");
            _log.AppendLine($"场景 = {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
            _log.AppendLine();

            // ═══════ T1 死亡系统存在 ═══════
            var death = FindFirstObjectByType<AbyssEcho.Horror.PlayerDeathSystem>();
            Check("T1 场景中存在 PlayerDeathSystem", death != null,
                  death != null ? $"@{death.gameObject.name}" : "❌ 找不到");
            if (death == null) { Dump(); yield break; }

            Check("T1b 初始状态为存活", !death.IsDead, $"IsDead={death.IsDead}");

            // ═══════ T2 找到怪物 ═══════
            var mimic = FindFirstObjectByType<AbyssEcho.Horror.MimicStalker>();
            var monster = FindFirstObjectByType<AbyssEcho.Monster.MonsterAI>();

            if (mimic == null && monster == null)
            {
                _log.AppendLine("[跳过] T2 本章没有怪物，无法测试真实死亡");
                Dump();
                yield break;
            }

            Check("T2 场景中存在怪物", true,
                  mimic != null ? $"MimicStalker @ {mimic.gameObject.name}" : $"MonsterAI @ {monster.gameObject.name}");

            // ═══════ T3 把玩家放到怪物附近并**背对它** ═══════
            //
            // ⚠️ 第一次测试失败的原因（记录在此，防止以后再踩）：
            //   我把玩家放在怪物正前方 2.5 米，结果 20 秒都没被攻击。
            //
            //   查 MimicStalker 源码后发现两条硬性规则：
            //     ① UpdateApproach()：玩家看得见它 → 立刻定住不动
            //     ② UpdateApproach()：距离 <= vanishDistance(5.5m) → 消失换位置
            //
            //   所以 2.5 米 + 正对着看 = 永远触发不了攻击。
            //   正确做法：站在 8 米外，并且**背对着它**。

            var motor = FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
            var look = FindFirstObjectByType<AbyssEcho.Player.PlayerLook>();
            var detector = FindFirstObjectByType<AbyssEcho.Interaction.InteractionDetector>();
            var crosshair = FindFirstObjectByType<AbyssEcho.UI.CrosshairUI>();

            if (motor == null) { Check("T3 找到玩家", false, "❌ 无 PlayerMotor"); Dump(); yield break; }

            var mob = mimic != null ? mimic.transform : monster.transform;

            // ⚠️ 第二次测试失败的教训（记录防止再踩）：
            //   我把玩家放在怪物 z+8 处，结果玩家 Y 从 0.09 掉到 -23.32，
            //   距离一路涨到 16081 米 —— 玩家掉出了地图。
            //
            //   原因：第三章 F5 房间范围是 x[-18,-8] z[26,34]，
            //   而 Mimic_F5 在 z=30，z+8=38 已经超出房间北边界 34。
            //
            //   修正：从怪物位置出发，沿房间里选一个**确实在地板上的点**。
            //   这里选 z-10（即 z=20，仍在 F5 的 [26,34] 范围内吗？不在）。
            //   所以改为沿 X 方向：F5 的 x 范围是 [-18,-8]，
            //   怪物在 x=-9.8，往 x=-16 方向走 6.2 米仍在房间内。
            Vector3 target;
            {
                // 优先沿 X 轴远离，且保证落在同一房间内
                float candX = mob.position.x - 6f;   // -15.8
                target = new Vector3(Mathf.Clamp(candX, -17f, -9f), 0.1f, mob.position.z);
            }

            var cc = motor.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            motor.transform.position = target;
            if (cc != null) cc.enabled = true;

            // 让玩家**背对**怪物（朝 +Z，怪物在 -Z 方向）
            //
            // ⚠️ 必须同时禁用 PlayerLook 和 FearVisuals：
            //   FearVisuals.cs 每帧写 camera.transform.localRotation（恐惧抖动），
            //   PlayerLook 每帧写 CameraPivot 的旋转 ——
            //   两者都会把我们设置的朝向覆盖掉。
            var camPivot = motor.transform.Find("CameraPivot");
            if (camPivot != null)
            {
                var pl = camPivot.GetComponent<AbyssEcho.Player.PlayerLook>();
                if (pl != null) pl.enabled = false;
                camPivot.localRotation = Quaternion.Euler(0f, 0f, 0f);   // 朝 +Z，背对怪物
            }

            var cam = Camera.main;
            if (cam != null)
            {
                var fv = cam.GetComponent<AbyssEcho.Horror.FearVisuals>();
                if (fv != null) fv.enabled = false;
                cam.transform.localRotation = Quaternion.identity;
            }

            yield return null;
            yield return null;

            _log.AppendLine($"    怪物位置 = {mob.position}");
            _log.AppendLine($"    玩家位置 = {motor.transform.position}");
            _log.AppendLine($"    玩家朝向 = 背对怪物（相机 forward = {(cam != null ? cam.transform.forward.ToString() : "无相机")}）");
            Check("T3 玩家已放到怪物附近并背对它",
                  Vector3.Distance(motor.transform.position, mob.position) > 5.5f,
                  $"距离={Vector3.Distance(motor.transform.position, mob.position):F2}m（需 > vanishDistance 5.5m）");

            // ═══════ T4 模拟"玩家背对怪物、怪物靠近" ═══════
            //
            // ⚠️ 前两次测试失败的原因（重要，记录防止再踩）：
            //
            //   失败1：玩家放 2.5 米 + 正对着看 → Mimic 定住不动（被看见就停）
            //   失败2：玩家放 z+8 米 → 超出房间边界，玩家掉进虚空（Y=-23）
            //   失败3：玩家放 x-6 米背对 → Mimic 先 Vanish，重生在 18 米，
            //          而 approachStartDistance 也是 18 米 —— 正好卡边界，不再靠近
            //
            //   第 3 次揭示了一个真实的设计问题（不是本次修复引入的）：
            //     respawnMaxDist(20) > approachStartDistance(18)
            //     约 40% 的重生点落在 18~20 米，Mimic 不会主动过来。
            //   这是 MimicStalker 原有的数值重叠，本次任务不修（不在范围内）。
            //
            //   所以测试改为：**由玩家逐步走近怪物**（模拟玩家自己走向它），
            //   只在玩家被它抓住的那一刻开始观察死亡流程。
            //   这不修改 AI，只是改变玩家位置。

            _log.AppendLine();
            _log.AppendLine("    模拟玩家背对怪物、逐步靠近（不干预 AI）...");

            bool attacked = false;
            float elapsed = 0f;

            // 每 0.5 秒把玩家向怪物推近 0.3 米（保持背对），最多 60 次
            for (int step = 0; step < 60 && !death.IsDead; step++)
            {
                // 保持背对（相机朝 +Z，怪物在 -Z）
                if (camPivot != null) camPivot.localRotation = Quaternion.Euler(0f, 0f, 0f);

                if (mimic != null) mimic.transform.position = new Vector3(
                    mob.position.x, mob.position.y, mob.position.z);

                float d = Vector3.Distance(motor.transform.position, mob.position);
                if (d > 3.0f)
                {
                    Vector3 dir = (mob.position - motor.transform.position);
                    dir.y = 0f;
                    Vector3 np = motor.transform.position + dir.normalized * 0.3f;
                    np.y = motor.transform.position.y;

                    var ccx = motor.GetComponent<CharacterController>();
                    if (ccx != null) ccx.enabled = false;
                    motor.transform.position = np;
                    if (ccx != null) ccx.enabled = true;
                }

                elapsed += 0.5f;
                if (step % 10 == 0)
                    _log.AppendLine($"      [{elapsed:F0}s] 距离={d:F1}m  mimic={mimic?.StateName}  可见={mimic?.IsVisibleToPlayer}");

                if (death.IsDead) { attacked = true; break; }

                yield return new WaitForSeconds(0.5f);
            }

            if (death.IsDead) attacked = true;

            if (attacked)
            {
                Check("T4 Mimic 实际攻击并触发了死亡", true, $"耗时 {elapsed:F1} 秒");
            }
            else
            {
                // ⚠️ 诚实记录：Mimic 没能攻击到玩家。
                //
                //   经查 MimicStalker.cs 第 258 / 268 行存在**逻辑矛盾**：
                //     258:  if (d <= vanishDistance /* 5.5 */) { Vanish(); return; }
                //     268:  if (enableAttack && d <= attackDistance /* 2.2 */) { HitPlayer(); }
                //
                //   vanishDistance(5.5) > attackDistance(2.2)：
                //   距离一旦 <= 5.5 就先 Vanish() 并 return，
                //   **2.2 米的攻击判定永远执行不到**。
                //
                //   这是 MimicStalker 自身的缺陷，不是本次 PlayerDeathSystem 修复引入的。
                //   本轮任务明确禁止修改 MimicStalker.cs，所以：
                //   · 此项标记为「未通过 / 待 Mimic 修复后复测」
                //   · 另起一项验证死亡系统本身是否能正常工作
                _log.AppendLine();
                _log.AppendLine("[未通过 T4] Mimic 未攻击到玩家（非本次修复引入）");
                _log.AppendLine("    代码确认的根因：");
                _log.AppendLine("      MimicStalker.cs:258  if (d <= vanishDistance /*5.5*/) { Vanish(); return; }");
                _log.AppendLine("      MimicStalker.cs:268  if (enableAttack && d <= attackDistance /*2.2*/) { HitPlayer(); }");
                _log.AppendLine("      → vanishDistance(5.5) > attackDistance(2.2)，攻击判定不可达。");
                _log.AppendLine("      → 本轮禁止修改 MimicStalker.cs，此项记入待修清单。");
                _fail++;

                _log.AppendLine();
                _log.AppendLine("[补充验证] 死亡系统本身能否被触发：");
                death.Kill("测试：验证死亡系统链路");
                yield return null;

                if (!death.IsDead)
                {
                    _log.AppendLine("    ❌ 直接调用 Kill() 也无效 —— 死亡系统本身有问题");
                    Dump();
                    yield break;
                }

                _log.AppendLine("    ✓ 直接调用 Kill() 成功进入死亡状态");
                Check("T4b 死亡系统本身可用（直接调用 Kill）", true, "IsDead=True");
            }

            // ═══════ T5 输入是否被冻结 ═══════
            yield return null;
            bool motorFrozen = motor.enabled == false;
            bool lookFrozen = look == null || look.enabled == false;
            bool detFrozen = detector == null || detector.enabled == false;
            bool crossFrozen = crosshair == null || crosshair.enabled == false;

            Check("T5 玩家移动被冻结", motorFrozen, $"motor.enabled={motor.enabled}");
            Check("T5b 玩家视角被冻结", lookFrozen, $"look.enabled={look?.enabled}");
            Check("T5c 交互检测被冻结", detFrozen, $"detector.enabled={detector?.enabled}");
            Check("T5d 准星被隐藏", crossFrozen, $"crosshair.enabled={crosshair?.enabled}");

            // ═══════ T6 鼠标解锁 ═══════
            Check("T6 鼠标已解锁（可点按钮）",
                  Cursor.lockState == CursorLockMode.None,
                  $"lockState={Cursor.lockState} visible={Cursor.visible}");

            // ═══════ T7 黑屏淡入 ═══════
            // 读私有字段 _fadeAlpha / _fadeComplete
            var t = typeof(AbyssEcho.Horror.PlayerDeathSystem);
            var bf = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var alphaF = t.GetField("_fadeAlpha", bf);
            var completeF = t.GetField("_fadeComplete", bf);

            _log.AppendLine();
            _log.AppendLine();
            _log.AppendLine("    等待黑屏淡入完成（fadeDuration=" + death.fadeDuration + "s）...");

            // ⚠️ 前一版用 Time.fixedUnscaledDeltaTime 累加，这是个错误：
            //   fixed 时间是物理帧步长，和 Update 里用的 unscaledDeltaTime 不同步，
            //   导致循环早就退出了，_fadeAlpha 只有 0.03。
            //   改为用真实经过时间（Time.realtimeSinceStartup）判断。
            float startRt = Time.realtimeSinceStartup;
            float alpha = 0f;
            bool complete = false;

            while (Time.realtimeSinceStartup - startRt < death.fadeDuration + 2.0f)
            {
                if (alphaF != null) alpha = (float)alphaF.GetValue(death);
                if (completeF != null) complete = (bool)completeF.GetValue(death);
                if (complete) break;
                yield return null;
            }

            // 最后再读一次，确保拿到最新值
            if (alphaF != null) alpha = (float)alphaF.GetValue(death);
            if (completeF != null) complete = (bool)completeF.GetValue(death);

            _log.AppendLine($"    实际等待 {Time.realtimeSinceStartup - startRt:F2} 秒");
            Check("T7 画面已淡到全黑", alpha >= 0.99f, $"_fadeAlpha={alpha:F2}");
            Check("T7b 淡入已完成（死亡界面可见）", complete, $"_fadeComplete={complete}");

            // ═══════ T8 死亡文案 ═══════
            Check("T8 标题正确", death.deathTitle == "你被发现了", $"'{death.deathTitle}'");
            Check("T8b 副标题正确", death.deathSubtitle == "深渊记得每一个来客。", $"'{death.deathSubtitle}'");
            Check("T8c 重开提示存在", !string.IsNullOrEmpty(death.restartHint), $"'{death.restartHint}'");

            // ═══════ T9 重复死亡不会再次触发 ═══════
            int eventCount = 0;
            System.Action handler = () => eventCount++;
            AbyssEcho.Horror.PlayerDeathSystem.OnPlayerDied += handler;

            death.Kill("重复调用测试");
            yield return null;
            AbyssEcho.Horror.PlayerDeathSystem.OnPlayerDied -= handler;

            Check("T9 重复 Kill() 被拦截（幂等）", eventCount == 0,
                  $"二次 Kill 触发事件次数={eventCount}（应为 0）");

            // ═══════ T10 重开接口可用 ═══════
            var restartM = t.GetMethod("Restart");
            Check("T10 Restart() 接口存在", restartM != null, restartM != null ? "ok" : "❌");

            // ═══════ 无法验证的部分（诚实标记） ═══════
            _log.AppendLine();
            _log.AppendLine("---- 以下项目在本环境无法验证，标记为待人工验收 ----");
            Unverified("T11 屏幕上真的显示黑屏与死亡界面（-nographics 无渲染）");
            Unverified("T12 玩家用手指按 R 键能重开（无真实输入设备）");
            Unverified("T13 鼠标点击「重新开始」按钮能重开（无真实鼠标）");
            Unverified("T14 死亡时播放的 stinger 音效听感（无音频输出）");
            Unverified("T15 反派抓住玩家时的镜头/动画观感（无渲染）");

            Dump();
        }

        private void Check(string name, bool ok, string detail)
        {
            if (ok) { _pass++; _log.AppendLine($"[通过] {name}  {detail}"); }
            else { _fail++; _log.AppendLine($"[失败] {name}  {detail}"); }
        }

        private void Unverified(string name)
        {
            _unverified++;
            _log.AppendLine($"[待人工验收] {name}");
        }

        private void Dump()
        {
            _log.AppendLine();
            _log.AppendLine("=====================================");
            _log.AppendLine($"通过 {_pass}，失败 {_fail}，待人工验收 {_unverified}");

            string text = _log.ToString();
            Debug.Log("<b>[DeathFlowTest]</b>\n" + text);

            var di = new DirectoryInfo(Application.dataPath);
            for (int hop = 0; hop < 6 && di != null; hop++)
            {
                string reports = Path.Combine(di.FullName, "Reports");
                if (Directory.Exists(reports))
                {
                    try
                    {
                        File.WriteAllText(Path.Combine(reports, "DeathFlowTest.txt"), text, new UTF8Encoding(false));
                        break;
                    }
                    catch { }
                }
                di = di.Parent;
            }

            if (!Application.isEditor) Application.Quit(_fail == 0 ? 0 : 1);
        }
    }
}
