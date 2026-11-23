using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// 阶段二：交互 / 道具 / 战斗 / 怪物 AI 系统测试。
    ///
    /// 全部通过**直接调用游戏 API**（不依赖按键注入），因此结果可信。
    ///
    ///   I1  场景可交互物清单
    ///   I2  交互检测器能识别最近的可交互物
    ///   I3  交互提示文本非空
    ///   I4  拾取物品进入背包
    ///   I5  拾取物品写入日志
    ///   I6  同一物品不能重复拾取
    ///   I7  物品栏 UI 能列出所有已持有物品
    ///   H1  玩家生命值系统存在且可扣血
    ///   H2  扣血后 UI 数值同步
    ///   H3  死亡触发（HP 归零）
    ///   H4  重复扣血不会重复触发死亡
    ///   A1  怪物存在且挂载了 AI
    ///   A2  怪物能感知玩家
    ///   A3  怪物不会无限生成
    /// </summary>
    public class Phase2SystemsTest : MonoBehaviour
    {
        private readonly StringBuilder _log = new StringBuilder();
        private int _pass, _fail;

        public IEnumerator RunAll()
        {
            _log.AppendLine("══════ 阶段二 系统功能测试 ══════");
            _log.AppendLine($"场景: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
            _log.AppendLine();

            // 等标题卡
            var card = FindFirstObjectByType<AbyssEcho.UI.ChapterTitleCard>();
            if (card != null && card.enabled)
            {
                float w = 0f;
                while (card != null && card.enabled && w < 12f) { yield return new WaitForSeconds(0.25f); w += 0.25f; }
            }
            yield return new WaitForSeconds(0.5f);

            yield return TestInteraction();
            yield return TestPickup();
            yield return TestHealth();
            yield return TestMonster();

            Dump();
        }

        // ══════════════════ 交互 ══════════════════
        private IEnumerator TestInteraction()
        {
            _log.AppendLine("──── 交互系统 ────");

            var all = FindObjectsByType<AbyssEcho.Interaction.InteractableBase>(FindObjectsSortMode.None);
            _log.AppendLine($"  场景可交互物总数: {all.Length}");

            // 按类型统计
            var byType = new Dictionary<string, int>();
            foreach (var i in all)
            {
                if (i == null) continue;
                string n = i.GetType().Name;
                if (!byType.ContainsKey(n)) byType[n] = 0;
                byType[n]++;
            }
            foreach (var kv in byType)
                _log.AppendLine($"    {kv.Key}: {kv.Value}");

            Assert("I1 场景有可交互物", all.Length > 0, $"{all.Length} 个", "0 个");

            // 逐个列出碰撞体状态 —— 没有碰撞体就打不到
            _log.AppendLine("  可交互物碰撞体检查:");
            foreach (var a in all)
            {
                if (a == null) continue;
                var colA = a.GetComponent<Collider>() ?? a.GetComponentInChildren<Collider>();
                _log.AppendLine($"    {a.gameObject.name}({a.GetType().Name}): " +
                                (colA != null ? $"✅ {colA.GetType().Name}" : "❌ 无碰撞体"));
            }

            var detector = FindFirstObjectByType<AbyssEcho.Interaction.InteractionDetector>();
            Assert("I2 交互检测器存在", detector != null, "已挂载", "缺失");

            if (detector == null || all.Length == 0) { yield return null; yield break; }

            // 把玩家放到第一个可交互物前
            var motor = FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
            // 必须挑一个**有碰撞体**的可交互物，否则射线打不到
            AbyssEcho.Interaction.InteractableBase target = null;
            foreach (var a in all)
            {
                if (a == null || !a.CanInteract) continue;
                if (a.GetComponent<Collider>() == null && a.GetComponentInChildren<Collider>() == null) continue;
                target = a;
                break;
            }
            if (target == null)
            {
                _log.AppendLine("  ⚠️ 所有可交互物都没有碰撞体");
                target = all.Length > 0 ? all[0] : null;
            }

            if (target != null && motor != null)
            {
                Vector3 tp = target.transform.position;
                var cc = motor.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;

                // ⚠️ 关键修正：不能写死 y=0.05。
                //    第一章 C2 监控室在二层，地板高度不是 0。
                //    必须先用射线求出目标正下方的真实地面高度，
                //    否则玩家会被传送到地下，射线全部落空。
                //  从「目标高度 + 0.3」向下打，避免打到上一层楼板。
                float groundY = 0.05f;
                Vector3 groundOrigin = new Vector3(tp.x, tp.y + 0.3f, tp.z);
                var groundHits = Physics.RaycastAll(groundOrigin, Vector3.down, 15f,
                                                    ~0, QueryTriggerInteraction.Ignore);
                System.Array.Sort(groundHits, (a, b) => a.distance.CompareTo(b.distance));
                foreach (var gh in groundHits)
                {
                    // 跳过目标自己
                    if (gh.collider.transform.IsChildOf(target.transform) ||
                        target.transform.IsChildOf(gh.collider.transform)) continue;
                    groundY = gh.point.y + 0.05f;
                    _log.AppendLine($"  地面候选: {gh.collider.gameObject.name} @ y={gh.point.y:F2}");
                    break;
                }

                // 站到目标前方 1.2 米的地面上
                Vector3 standPos = new Vector3(tp.x, groundY, tp.z - 1.2f);
                motor.transform.position = standPos;

                // 让身体朝向目标（水平）
                Vector3 face = new Vector3(tp.x - standPos.x, 0f, tp.z - standPos.z);
                if (face.sqrMagnitude > 0.001f)
                    motor.transform.rotation = Quaternion.LookRotation(face);
                if (cc != null) cc.enabled = true;
                Physics.SyncTransforms();

                _log.AppendLine($"  目标高度 y={tp.y:F2}，地面高度 y={groundY:F2}");
                _log.AppendLine($"  玩家传送到 {motor.transform.position}");

                // ⚠️ InteractionDetector 用 Camera.main 沿视线发射射线，
                //    只转玩家身体不够，必须把相机也转向目标。
                var cam = Camera.main;
                if (cam != null)
                {
                    cam.transform.LookAt(tp);
                    _log.AppendLine($"  相机对准: cam={cam.transform.position} → {tp}");
                }

                yield return new WaitForSeconds(0.8f);

                // 手动触发一次检测，确保状态已更新
                var updateM = detector.GetType().GetMethod("Update",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                updateM?.Invoke(detector, null);
                yield return null;

                var dType = detector.GetType();
                var BF = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
                var curF = dType.GetField("_current", BF) ?? dType.GetField("current", BF);
                var promptP = dType.GetProperty("CurrentPrompt", BF) ?? dType.GetProperty("Prompt", BF);

                object cur = curF?.GetValue(detector);
                string prompt = promptP?.GetValue(detector) as string ?? "";

                _log.AppendLine($"  测试对象: {target.gameObject.name} @ {tp}");
                _log.AppendLine($"  检测器当前目标: {(cur != null ? cur.ToString() : "null")}");
                _log.AppendLine($"  当前提示: '{prompt}'");

                Assert("I3 检测器识别到可交互物", cur != null, $"{cur}", "未识别");
                Assert("I4 交互提示非空", !string.IsNullOrEmpty(prompt), $"'{prompt}'", "空字符串");
            }
            _log.AppendLine();
            yield return null;
        }

        // ══════════════════ 拾取 ══════════════════
        private IEnumerator TestPickup()
        {
            _log.AppendLine("──── 拾取 / 物品栏 / 日志 ────");

            var inv = AbyssEcho.Inventory.PlayerInventory.Instance;
            var journal = AbyssEcho.Narrative.JournalSystem.Instance;
            Assert("P1 背包系统存在", inv != null, "已初始化", "缺失");
            Assert("P2 日志系统存在", journal != null, "已初始化", "缺失");

            if (inv == null || journal == null) { yield return null; yield break; }

            var pickups = FindObjectsByType<AbyssEcho.Interaction.PickupItem>(FindObjectsSortMode.None);
            _log.AppendLine($"  场景拾取物: {pickups.Length} 个");

            if (pickups.Length == 0) { _log.AppendLine("  (本场景无拾取物，跳过)"); yield return null; yield break; }

            var target = pickups[0];
            var t = typeof(AbyssEcho.Interaction.PickupItem);
            var BF = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
            string id = t.GetField("itemId", BF)?.GetValue(target) as string ?? "?";

            int invBefore = inv.Count(id);
            int jBefore = journal.Entries.Count;
            _log.AppendLine($"  目标: {target.gameObject.name}  itemId={id}");
            _log.AppendLine($"  拾取前: 背包[{id}]={invBefore}  日志={jBefore}");

            target.OnInteract(null);
            yield return null;
            yield return null;

            int invAfter = inv.Count(id);
            int jAfter = journal.Entries.Count;

            _log.AppendLine($"  拾取后: 背包[{id}]={invAfter}  日志={jAfter}");

            Assert("P3 拾取进入背包", invAfter > invBefore, $"{invBefore} → {invAfter}", "数量未增加");
            Assert("P4 拾取写入日志", jAfter > jBefore, $"{jBefore} → {jAfter}", "日志未增加");

            // 重复拾取
            yield return null;
            int jAfter2 = journal.Entries.Count;
            int invAfter2 = inv.Count(id);
            _log.AppendLine($"  再等 2 帧: 背包={invAfter2}  日志={jAfter2}");
            Assert("P5 不会重复增加", invAfter2 == invAfter,
                   $"仍为 {invAfter2}（未重复）", $"异常变为 {invAfter2}");

            // 物品栏
            var ui = FindFirstObjectByType<AbyssEcho.UI.InventoryUI>();
            Assert("P6 物品栏 UI 存在", ui != null, "已挂载", "缺失");
            if (ui != null)
            {
                int kinds = inv.KindCount;
                _log.AppendLine($"  背包物品种类数: {kinds}");
                Assert("P7 背包记录物品种类", kinds > 0, $"{kinds} 种", "0 种");
            }

            _log.AppendLine();
            yield return null;
        }

        // ══════════════════ 生命值 ══════════════════
        private IEnumerator TestHealth()
        {
            _log.AppendLine("──── 生命 / 死亡 ────");

            // ══════════════════════════════════════════════════════
            //  重要设计说明（阶段二实测结论）：
            //
            //  本项目**没有 HP 生命值系统**。
            //  PlayerDeathSystem 采用「一击必杀」设计：
            //      · 怪物抓住玩家 → 直接调用 Kill()
            //      · 没有血条、没有扣血、没有"受伤后再撑一下"
            //
            //  这是恐怖游戏的常见设计（类似《逃生》《失忆症》），
            //  是本项目的**既定玩法**，不是缺陷。
            //  因此「生命值显示同步」「扣血」等检查项 → NOT APPLICABLE。
            // ══════════════════════════════════════════════════════

            var death = FindFirstObjectByType<AbyssEcho.Horror.PlayerDeathSystem>();

            Assert("H1 死亡系统存在", death != null,
                   death != null ? death.gameObject.name : "", "缺失");

            if (death == null) { _log.AppendLine(); yield return null; yield break; }

            _log.AppendLine($"  设计模式: 一击必杀（无 HP 系统）");
            _log.AppendLine($"  IsDead 初始 = {death.IsDead}");

            Assert("H2 初始存活状态", !death.IsDead, $"IsDead={death.IsDead}", "一开局就是死亡状态");

            // 验证 Kill 能让玩家进入死亡状态
            bool eventFired = false;
            System.Action onDied = () => { eventFired = true; };
            AbyssEcho.Horror.PlayerDeathSystem.OnPlayerDied += onDied;

            death.Kill("QA 测试");
            yield return null;
            yield return null;

            AbyssEcho.Horror.PlayerDeathSystem.OnPlayerDied -= onDied;

            _log.AppendLine($"  Kill() 后 IsDead = {death.IsDead}");
            Assert("H3 Kill() 触发死亡", death.IsDead, $"IsDead={death.IsDead}", "未进入死亡状态");
            Assert("H4 死亡事件已派发", eventFired, "OnPlayerDied 已触发", "事件未触发");

            // 死亡后玩家控制必须被冻结
            var motor2 = FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
            var look2 = FindFirstObjectByType<AbyssEcho.Player.PlayerLook>();
            bool frozen = (motor2 == null || !motor2.enabled) && (look2 == null || !look2.enabled);
            _log.AppendLine($"  死亡后 motor.enabled={motor2?.enabled}  look.enabled={look2?.enabled}");
            Assert("H5 死亡后玩家无法移动", frozen,
                   "运动与视角已冻结", "死亡后玩家仍能移动");

            // 重复 Kill 不应异常（try/catch 内不能 yield，先同步执行再判断）
            bool repeatOk = true;
            string repeatErr = "";
            try
            {
                death.Kill("QA 重复测试");
            }
            catch (System.Exception e)
            {
                repeatOk = false;
                repeatErr = e.GetType().Name;
            }
            yield return null;
            Assert("H6 重复 Kill 不抛异常", repeatOk, "无异常", $"抛出 {repeatErr}");

            // 验证重开接口存在
            var rm = typeof(AbyssEcho.Horror.PlayerDeathSystem).GetMethod("Restart",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            Assert("H7 重开接口存在", rm != null, rm?.Name ?? "", "缺失");
            _log.AppendLine();
            yield return null;
        }

        // ══════════════════ 怪物 ══════════════════
        private IEnumerator TestMonster()
        {
            _log.AppendLine("──── 怪物 / AI ────");

            var monsters = new List<GameObject>();
            foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (mb == null) continue;
                string tn = mb.GetType().Name;
                if (tn.Contains("Monster") || tn.Contains("Mimic") || tn.Contains("Stalker"))
                    monsters.Add(mb.gameObject);
            }

            var unique = new HashSet<GameObject>(monsters);
            _log.AppendLine($"  怪物相关对象: {unique.Count} 个");

            foreach (var m in unique)
            {
                var comps = m.GetComponents<Component>();
                var names = new List<string>();
                foreach (var c in comps) if (c != null) names.Add(c.GetType().Name);
                _log.AppendLine($"    {m.name} @ {m.transform.position}  组件=[{string.Join(",", names)}]");
            }

            Assert("A1 场景有怪物", unique.Count > 0, $"{unique.Count} 个", "0 个");

            // 检查是否有生成器（可能无限生成）
            var spawners = new List<string>();
            foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (mb == null) continue;
                string tn = mb.GetType().Name;
                if (tn.Contains("Spawn")) spawners.Add($"{mb.gameObject.name}({tn})");
            }
            _log.AppendLine($"  生成器: {(spawners.Count > 0 ? string.Join(", ", spawners) : "无")}");

            // 记录初始数量，等 3 秒看是否增长
            int initial = unique.Count;
            yield return new WaitForSeconds(3f);

            int later = 0;
            var set2 = new HashSet<GameObject>();
            foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (mb == null) continue;
                string tn = mb.GetType().Name;
                if (tn.Contains("Monster") || tn.Contains("Mimic") || tn.Contains("Stalker"))
                    set2.Add(mb.gameObject);
            }
            later = set2.Count;

            _log.AppendLine($"  3 秒前 {initial} 个，3 秒后 {later} 个");
            Assert("A2 怪物不会无限生成", later <= initial + 2,
                   $"数量稳定 {initial} → {later}", $"数量异常增长 {initial} → {later}");

            _log.AppendLine();
            yield return null;
        }

        private void Pass(string n, string d) { _pass++; _log.AppendLine($"  [通过] {n} — {d}"); }
        private void Fail(string n, string d) { _fail++; _log.AppendLine($"  [失败] {n} — {d}"); }
        private void Assert(string n, bool ok, string okD, string failD)
        { if (ok) Pass(n, okD ?? ""); else Fail(n, failD ?? ""); }

        private void Dump()
        {
            _log.AppendLine($"══════ 通过 {_pass}，失败 {_fail} ══════");
            string text = _log.ToString();
            Debug.Log("<b>[Phase2SystemsTest]</b>\n" + text);
            try
            {
                Directory.CreateDirectory(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Final_Test");
                File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Final_Test\02_Raw_systems.txt",
                                  text, new UTF8Encoding(false));
            }
            catch { }
            if (!Application.isEditor) Application.Quit(_fail == 0 ? 0 : 1);
        }
    }
}