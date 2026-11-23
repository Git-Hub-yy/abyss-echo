using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// S45 攻击判定修复验证：按指定距离精确测试 MimicStalker 的行为。
    ///
    /// ══════════════════ 测试设计 ══════════════════
    ///
    ///   逐个距离档位测试，每次都把状态复位：
    ///     6.0 米  → 期望：Approach 接近，**不攻击**，**不消失**
    ///     4.0 米  → 期望：Approach 接近（在 vanishDistance 5.5 以内会被消失，属原有设计）
    ///     3.0 米  → 期望：原有接近行为正常
    ///     2.0 米  → 期望：**触发攻击**（本次修复的核心）
    ///     2.5 米  → 期望：不攻击（略大于 attackDistance 2.2）
    ///
    ///   关键约束（避免重蹈覆辙）：
    ///     · 玩家必须**背对**怪物（被看见会切回 Idle 定住）
    ///     · 必须禁用 PlayerLook 与 FearVisuals（它们每帧覆盖相机旋转）
    ///     · 玩家位置必须**落在房间内**（否则会掉出地图）
    /// </summary>
    public class MimicAttackTest : MonoBehaviour
    {
        public bool runOnStart = true;
        public int forceChapter = 3;

        private readonly StringBuilder _log = new StringBuilder();
        private int _pass, _fail, _unverified;

        private AbyssEcho.Horror.MimicStalker _mimic;
        private AbyssEcho.Player.PlayerMotor _motor;
        private Transform _camPivot;
        private Vector3 _mimicHome;

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

            var sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            _log.AppendLine("===== S45 攻击判定修复验证 =====");
            _log.AppendLine($"场景 = {sceneName}");
            _log.AppendLine();

            // ── 准备 ──
            _mimic = FindFirstObjectByType<AbyssEcho.Horror.MimicStalker>();
            _motor = FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();

            if (_mimic == null) { _log.AppendLine("❌ 本章没有 MimicStalker，测试终止"); Dump(); yield break; }
            if (_motor == null) { _log.AppendLine("❌ 找不到玩家，测试终止"); Dump(); yield break; }

            _mimicHome = _mimic.transform.position;

            _log.AppendLine($"Mimic = {_mimic.gameObject.name} @ {_mimicHome}");
            _log.AppendLine($"  attackDistance = {_mimic.attackDistance}");
            _log.AppendLine($"  vanishDistance = {_mimic.vanishDistance}");
            _log.AppendLine($"  enableAttack   = {_mimic.enableAttack}");
            _log.AppendLine();

            Check("T0 enableAttack 已开启", _mimic.enableAttack, $"enableAttack={_mimic.enableAttack}");
            Check("T0b 参数关系正确（attack < vanish）",
                  _mimic.attackDistance < _mimic.vanishDistance,
                  $"attack={_mimic.attackDistance} < vanish={_mimic.vanishDistance}");

            // 禁用会覆盖相机旋转的组件
            _camPivot = _motor.transform.Find("CameraPivot");
            var cam = Camera.main;

            if (_camPivot != null)
            {
                var pl = _camPivot.GetComponent<AbyssEcho.Player.PlayerLook>();
                if (pl != null) pl.enabled = false;
            }
            if (cam != null)
            {
                var fv = cam.GetComponent<AbyssEcho.Horror.FearVisuals>();
                if (fv != null) fv.enabled = false;
            }

            _log.AppendLine("（已禁用 PlayerLook / FearVisuals，避免它们覆盖相机朝向）");
            _log.AppendLine();

            // ── 逐距离测试 ──
            //
            // ★ 顺序很重要：先跑不会致死的档位，最后跑致死档位。
            //   因为 PlayerDeathSystem.IsDead 一旦变为 true，在本场景内无法重置。
            //
            // ⚠️ 各章参数不同：
            //     Ch3: attack 2.2 / vanish 5.5
            //     Ch4: attack 2.0 / vanish 5.0
            //     Ch5: attack 2.0 / vanish 5.0
            //   所以测试距离必须按每章实际参数计算，不能写死。
            float atk = _mimic.attackDistance;
            float van = _mimic.vanishDistance;

            yield return TestDistance(van + 2.0f, $"vanish+2 米（远处）");
            yield return TestDistance((atk + van) * 0.5f, $"attack~vanish 中点");

            // ★ 核心：致死档位。必须**严格小于** attackDistance。
            //   留 0.3 米余量，避免 CharacterController 被推开后距离不达标。
            float lethal = Mathf.Max(1.2f, atk - 0.3f);
            yield return TestDistance(lethal, $"攻击区间内 ★（attack={atk}）");

            // ── 攻击后的完整死亡链路（不复位，直接验证） ──
            yield return TestDeathChain();

            Dump();
        }

        /// <summary>把玩家放在指定距离并观察 3 秒内的行为。</summary>
        private IEnumerator TestDistance(float distance, string label)
        {
            ResetAll();

            // ⚠️ CharacterController 有重力，玩家会持续下坠导致距离漂移。
            //   所以不能只传送一次，要**每帧把玩家锁在指定距离**。
            //   （上一版就是因为这个，设 2.5 米实际变成 1.95 米，测试全部失真。）
            Vector3 anchor = new Vector3(_mimicHome.x - distance, 0.1f, _mimicHome.z);

            Teleport(anchor);
            FaceAwayFrom(_mimicHome);

            yield return null;
            yield return null;

            float realDist = Vector3.Distance(_motor.transform.position, _mimic.transform.position);
            string startState = _mimic.StateName;

            bool attacked = false;
            bool vanished = false;
            float elapsed = 0f;

            // ⚠️ 观察窗口必须小于 maxVisibleTime(2.5s)：
            //   原机制规定"被持续直视 2.5 秒就 Vanish"。
            //   上一版观察 3 秒，超过了这个阈值，怪物被判定为"被看见太久"而消失，
            //   于是 6 米档误报"意外消失"。
            //   这里用 2.0 秒，既够看到攻击触发，又不会踩到 maxVisibleTime。
            while (elapsed < 2.0f)
            {
                // 每帧重新锁位置（抵消重力位移），并保持背对
                Teleport(anchor);
                FaceAwayFrom(_mimic.transform.position);

                if (_mimic.StateName == "Dead") attacked = true;
                if (_mimic.StateName == "Vanish") vanished = true;

                if (attacked) break;

                elapsed += Time.deltaTime;
                yield return null;
            }

            _log.AppendLine($"─── {label} ───");
            _log.AppendLine($"    初始实际距离 = {realDist:F2}m   初始状态 = {startState}   最终状态 = {_mimic.StateName}");
            _log.AppendLine($"    attack={_mimic.attackDistance}  vanish={_mimic.vanishDistance}");

            // ⚠️ 用**实测距离**判定，不用设定距离：
            //   CharacterController 半径 0.32，玩家会被怪物 CapsuleCollider 推开，
            //   设定 2.5 米实测会变成 1.95 米。用实测值才不会错判。
            bool shouldAttack = realDist < _mimic.attackDistance;

            if (shouldAttack)
                Check($"实测 {realDist:F2}m（< attack {_mimic.attackDistance}）应当触发攻击", attacked,
                      attacked ? "✓ 已扑击（State.Dead）" : "❌ 未攻击");
            else
                Check($"实测 {realDist:F2}m（>= attack {_mimic.attackDistance}）不应触发攻击", !attacked,
                      attacked ? "❌ 误攻击" : "✓ 未攻击");

            if (realDist > _mimic.vanishDistance)
            {
                Check($"实测 {realDist:F2}m（> vanish {_mimic.vanishDistance}）不应消失", !vanished,
                      vanished ? "❌ 意外消失" : "✓ 未消失");
            }
            else if (!shouldAttack)
            {
                _log.AppendLine($"    实测 {realDist:F2}m 在 vanish 区间内且未达攻击距离 → 消失={vanished}（原有设计，不判失败）");
            }

            _log.AppendLine();
        }

        /// <summary>近距离攻击后的完整死亡链路验证。</summary>
        private IEnumerator TestDeathChain()
        {
            var death = FindFirstObjectByType<AbyssEcho.Horror.PlayerDeathSystem>();
            if (death == null) { Check("T-D 死亡系统存在", false, "❌ 无 PlayerDeathSystem"); yield break; }

            // ⚠️ 这里**绝不能调 ResetAll()**：
            //   ResetAll() 会把 motor / detector / crosshair 重新 enabled = true，
            //   而 PlayerDeathSystem.Kill() 只在死亡那一刻冻结它们一次。
            //   重置后再检查冻结状态，必然全部是 true → 假失败。
            //   上一版就是因为这里残留了 ResetAll()，导致 T-D2/T-D4/T-D5 误报。
            //
            //   正确做法：上一档（攻击区间内）刚刚触发过攻击，冻结状态还在，直接验证。

            float lethalD = Mathf.Max(1.2f, _mimic.attackDistance - 0.3f);
            Vector3 playerPos = new Vector3(_mimicHome.x - lethalD, 0.1f, _mimicHome.z);
            Teleport(playerPos);
            FaceAwayFrom(_mimicHome);

            yield return null;
            yield return null;

            _log.AppendLine("─── 攻击后完整死亡链路 ───");

            // 等攻击发生
            float waited = 0f;
            while (!death.IsDead && waited < 5f)
            {
                FaceAwayFrom(_mimic.transform.position);
                waited += Time.deltaTime;
                yield return null;
            }

            Check("T-D1 攻击成功调用了 PlayerDeathSystem.Kill()", death.IsDead,
                  death.IsDead ? $"耗时 {waited:F2}s  IsDead=True" : "❌ IsDead=False");

            if (!death.IsDead) { _log.AppendLine(); yield break; }

            yield return null;

            // 输入冻结
            var look = _camPivot != null ? _camPivot.GetComponent<AbyssEcho.Player.PlayerLook>() : null;
            var detector = FindFirstObjectByType<AbyssEcho.Interaction.InteractionDetector>();
            var crosshair = FindFirstObjectByType<AbyssEcho.UI.CrosshairUI>();

            Check("T-D2 玩家移动被冻结", !_motor.enabled, $"motor.enabled={_motor.enabled}");
            Check("T-D3 玩家视角被冻结", look == null || !look.enabled, $"look.enabled={look?.enabled}");
            Check("T-D4 交互检测被冻结", detector == null || !detector.enabled, $"detector.enabled={detector?.enabled}");
            Check("T-D5 准星被隐藏", crosshair == null || !crosshair.enabled, $"crosshair.enabled={crosshair?.enabled}");
            Check("T-D6 鼠标已解锁", Cursor.lockState == CursorLockMode.None, $"lockState={Cursor.lockState}");

            // 黑屏与文案
            var t = typeof(AbyssEcho.Horror.PlayerDeathSystem);
            var bf = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var alphaF = t.GetField("_fadeAlpha", bf);
            var completeF = t.GetField("_fadeComplete", bf);

            float startRt = Time.realtimeSinceStartup;
            float alpha = 0f;
            bool complete = false;
            while (Time.realtimeSinceStartup - startRt < death.fadeDuration + 2f)
            {
                if (alphaF != null) alpha = (float)alphaF.GetValue(death);
                if (completeF != null) complete = (bool)completeF.GetValue(death);
                if (complete) break;
                yield return null;
            }
            if (alphaF != null) alpha = (float)alphaF.GetValue(death);
            if (completeF != null) complete = (bool)completeF.GetValue(death);

            Check("T-D7 黑屏淡入已完成", alpha >= 0.99f && complete, $"_fadeAlpha={alpha:F2} _fadeComplete={complete}");
            Check("T-D8 中文死亡标题正确", death.deathTitle == "你被发现了", $"'{death.deathTitle}'");
            Check("T-D9 中文副标题正确", death.deathSubtitle == "深渊记得每一个来客。", $"'{death.deathSubtitle}'");

            // 重复攻击不会重复死亡
            int evtCount = 0;
            System.Action h = () => evtCount++;
            AbyssEcho.Horror.PlayerDeathSystem.OnPlayerDied += h;
            _mimic.SendMessage("HitPlayer", SendMessageOptions.DontRequireReceiver);
            yield return null;
            AbyssEcho.Horror.PlayerDeathSystem.OnPlayerDied -= h;
            Check("T-D10 重复攻击不会重复触发死亡", evtCount == 0, $"二次触发事件={evtCount}（应为0）");

            // Restart 接口
            Check("T-D11 Restart() 接口可用", t.GetMethod("Restart") != null, "ok");

            _log.AppendLine();
            Unverified("T-D12 屏幕上真的显示黑屏与死亡界面（-nographics 无渲染）");
            Unverified("T-D13 真人按 R 键能重开当前章节（无真实输入设备）");
            Unverified("T-D14 鼠标点击「重新开始」按钮（无真实鼠标）");
            Unverified("T-D15 扑击时的音效与镜头观感（无音频/渲染）");
        }

        // ══════════════════ 工具 ══════════════════

        private void ResetAll()
        {
            // 复位怪物
            _mimic.ResetStalker();
            _mimic.transform.position = _mimicHome;

            // 复位玩家
            _motor.enabled = true;
            if (_camPivot != null)
            {
                var pl = _camPivot.GetComponent<AbyssEcho.Player.PlayerLook>();
                if (pl != null) pl.enabled = false;   // 保持禁用以防覆盖朝向
            }

            var detector = FindFirstObjectByType<AbyssEcho.Interaction.InteractionDetector>();
            if (detector != null) detector.enabled = true;
            var crosshair = FindFirstObjectByType<AbyssEcho.UI.CrosshairUI>();
            if (crosshair != null) crosshair.enabled = true;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Teleport(Vector3 pos)
        {
            var cc = _motor.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            _motor.transform.position = pos;
            if (cc != null) cc.enabled = true;
        }

        /// <summary>让玩家背对指定位置（相机朝 +Z，目标在 -Z 方向）。</summary>
        private void FaceAwayFrom(Vector3 target)
        {
            if (_camPivot == null) return;
            Vector3 dir = target - _motor.transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;

            // 朝目标的反方向
            Quaternion look = Quaternion.LookRotation(-dir.normalized, Vector3.up);
            _camPivot.rotation = look;
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
            _log.AppendLine("=====================================");
            _log.AppendLine($"通过 {_pass}，失败 {_fail}，待人工验收 {_unverified}");

            string text = _log.ToString();
            Debug.Log("<b>[MimicAttackTest]</b>\n" + text);

            var di = new DirectoryInfo(Application.dataPath);
            for (int hop = 0; hop < 6 && di != null; hop++)
            {
                string reports = Path.Combine(di.FullName, "Reports");
                if (Directory.Exists(reports))
                {
                    try
                    {
                        string fn = $"MimicAttackTest_Ch{(int)AbyssEcho.LevelGen.ChapterLayout.Current}.txt";
                        File.WriteAllText(Path.Combine(reports, fn), text, new UTF8Encoding(false));
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
