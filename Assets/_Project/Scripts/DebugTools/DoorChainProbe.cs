using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// 复核：第一道门 / 第二道门 / A2 停电事件的**实际交互验证**。
    ///
    /// ⚠️ 这是"实际交互验证"级别，会真实调用交互方法。
    ///   为了不污染存档，探测结束后会恢复关键状态。
    ///   但**不会保存场景**，退出即丢弃。
    ///
    /// 分三级报告：
    ///   ① 配置检查 —— 字段值是否正确
    ///   ② 代码逻辑 —— 调用链是否完整
    ///   ③ 实际交互 —— 真的调用并观察结果
    /// </summary>
    public class DoorChainProbe : MonoBehaviour
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

            _log.AppendLine("===== 门链路实际交互验证 =====");
            _log.AppendLine();

            // ═════════ 第一道门：保险丝 → 配电箱 → Door_A3_B ═════════
            _log.AppendLine("═════════ 第一道门链路 ═════════");
            _log.AppendLine();

            var fuse = FindFirstObjectByType<AbyssEcho.Interaction.PickupItem>();
            var panel = FindFirstObjectByType<AbyssEcho.Puzzle.PowerRestorePanel>();
            var inv = AbyssEcho.Inventory.PlayerInventory.Instance;

            DoorA3_B = null;
            foreach (var d in FindObjectsByType<AbyssEcho.Interaction.DoorInteractable>(FindObjectsSortMode.None))
                if (d.name.Contains("A3_B")) DoorA3_B = d;

            _log.AppendLine($"-- ① 配置 --");
            _log.AppendLine($"  PickupItem = {(fuse != null ? fuse.name : "无")}  itemId='{fuse?.itemId}'  amount={fuse?.amount}  destroyOnPickup={fuse?.destroyOnPickup}");
            _log.AppendLine($"  PowerRestorePanel = {(panel != null ? panel.name : "无")}  requiredItemId='{panel?.requiredItemId}'  consume={panel?.consumeItem}");
            _log.AppendLine($"  doorsToUnlock 数量 = {panel?.doorsToUnlock?.Length}");
            if (panel?.doorsToUnlock != null)
                foreach (var d in panel.doorsToUnlock)
                    _log.AppendLine($"     → {(d != null ? d.name : "NULL")}  locked={d?.locked}");
            _log.AppendLine($"  Door_A3_B = {(DoorA3_B != null ? DoorA3_B.name : "❌ 没找到")}  locked={DoorA3_B?.locked}  unlockable={DoorA3_B?.unlockableByInteraction}");
            _log.AppendLine($"  PlayerInventory = {(inv != null ? "有" : "❌无")}");

            if (fuse == null || panel == null || inv == null || DoorA3_B == null)
            {
                _log.AppendLine("  ❌ 关键对象缺失，无法继续");
            }
            else
            {
                _log.AppendLine();
                _log.AppendLine("-- ③ 实际交互 --");

                // 步骤 1：拾取保险丝
                _log.AppendLine($"  步骤1 拾取前: 背包 fuse = {inv.Count(AbyssEcho.Inventory.ItemIds.Fuse)}");
                fuse.OnInteract(gameObject);
                yield return null;
                int afterPickup = inv.Count(AbyssEcho.Inventory.ItemIds.Fuse);
                _log.AppendLine($"  步骤1 拾取后: 背包 fuse = {afterPickup}  {(afterPickup > 0 ? "✓" : "❌")}  Has()={inv.Has(AbyssEcho.Inventory.ItemIds.Fuse)}");

                // 步骤 2：配电箱交互
                bool lockedBefore = DoorA3_B.locked;
                _log.AppendLine($"  步骤2 安装前: Door_A3_B.locked = {lockedBefore}");
                _log.AppendLine($"         配电箱 GetPrompt = '{panel.GetPrompt()}'");

                panel.OnInteract(gameObject);
                yield return null;

                bool lockedAfter = DoorA3_B.locked;
                _log.AppendLine($"  步骤2 安装后: Door_A3_B.locked = {lockedAfter}  {(lockedBefore && !lockedAfter ? "✓ 已解锁" : (lockedBefore ? "❌ 未变化" : "本来就是开的"))}");
                _log.AppendLine($"         IsPowered = {panel.IsPowered}");
                _log.AppendLine($"         背包 fuse = {inv.Count(AbyssEcho.Inventory.ItemIds.Fuse)}（consume={panel.consumeItem}）");

                // 步骤 3：按 E 开门（模拟玩家再交互一次）
                _log.AppendLine($"  步骤3 开门前: IsOpen={DoorA3_B.IsOpen}");
                DoorA3_B.OnInteract(gameObject);
                yield return null;
                _log.AppendLine($"  步骤3 开门后: IsOpen={DoorA3_B.IsOpen}  {(DoorA3_B.IsOpen ? "✓" : "❌")}");

                // 等待滑动完成，检查碰撞体
                yield return new WaitForSeconds(1.2f);
                var col = DoorA3_B.GetComponent<Collider>();
                _log.AppendLine($"  步骤4 滑动后: 门位置={DoorA3_B.transform.position}");
                _log.AppendLine($"         Collider={(col != null ? col.GetType().Name : "无")} enabled={col?.enabled}");

                // 检查门洞是否真的通了（在门洞中心探测）
                Vector3 probe = DoorA3_B.transform.position;
                var hits = Physics.OverlapSphere(new Vector3(probe.x, 1.0f, probe.z), 0.3f, ~0, QueryTriggerInteraction.Ignore);
                int solid = 0;
                string names = "";
                foreach (var h in hits)
                {
                    if (h == null) continue;
                    if (h is CharacterController) continue;
                    if (h.bounds.max.y < 0.4f || h.bounds.min.y > 2.05f) continue;
                    solid++; names += h.gameObject.name + " ";
                }
                _log.AppendLine($"         门洞探测: {(solid == 0 ? "✓ 畅通" : $"❌ 仍有 {solid} 个阻挡 [{names}]")}");
            }

            // ═════════ 第二道门：键盘 → Door_C2 + Door_D_Gate ═════════
            _log.AppendLine();
            _log.AppendLine("═════════ 第二道门链路 ═════════");
            _log.AppendLine();

            var keypad = FindFirstObjectByType<AbyssEcho.Puzzle.KeypadPanel>();
            var keypadUI = FindFirstObjectByType<AbyssEcho.UI.KeypadUI>();

            _log.AppendLine("-- ① 配置 --");
            _log.AppendLine($"  KeypadPanel = {(keypad != null ? keypad.name : "❌无")}  code='{keypad?.correctCode}'");
            _log.AppendLine($"  KeypadUI = {(keypadUI != null ? keypadUI.name : "❌ 场景里没有 KeypadUI（无法输入密码！）")}");
            _log.AppendLine($"  doorsToUnlock 数量 = {keypad?.doorsToUnlock?.Length}");
            if (keypad?.doorsToUnlock != null)
                foreach (var d in keypad.doorsToUnlock)
                    _log.AppendLine($"     → {(d != null ? d.name : "NULL")}  locked={d?.locked}  IsOpen={d?.IsOpen}");

            if (keypad != null)
            {
                _log.AppendLine();
                _log.AppendLine("-- ③ 实际交互 --");
                _log.AppendLine($"  错误密码测试: Submit(\"0000\") = {keypad.Submit("0000")}  IsUnlocked={keypad.IsUnlocked}");
                yield return null;
                _log.AppendLine($"  正确密码测试: Submit(\"{keypad.correctCode}\") = {keypad.Submit(keypad.correctCode)}  IsUnlocked={keypad.IsUnlocked}");
                yield return null;

                if (keypad.doorsToUnlock != null)
                {
                    foreach (var d in keypad.doorsToUnlock)
                    {
                        if (d == null) continue;
                        _log.AppendLine($"  门 {d.name}: locked={d.locked} {(d.locked ? "❌ 仍锁着" : "✓ 已解锁")}");

                        // 尝试开门
                        d.OnInteract(gameObject);
                        yield return null;
                        _log.AppendLine($"    开门后 IsOpen={d.IsOpen} {(d.IsOpen ? "✓" : "❌")}");
                    }
                }
            }

            // ═════════ A2 停电事件 ═════════
            _log.AppendLine();
            _log.AppendLine("═════════ A2 停电事件 ═════════");
            _log.AppendLine();

            AbyssEcho.Horror.HorrorTrigger a2 = null;
            foreach (var t in FindObjectsByType<AbyssEcho.Horror.HorrorTrigger>(FindObjectsSortMode.None))
                if (t.name.Contains("A2")) a2 = t;

            if (a2 == null)
            {
                _log.AppendLine("❌ 没有名为 A2 的 HorrorTrigger");
            }
            else
            {
                _log.AppendLine("-- ① 配置 --");
                _log.AppendLine("-- ① 配置 --");
                var at = typeof(AbyssEcho.Horror.HorrorTrigger);
                var bf = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
                foreach (var fn in new[] { "delay", "oneShot", "playStinger", "fearJump",
                                           "dimAmbient", "dimAmbientTo", "dimDuration" })
                {
                    var fi = at.GetField(fn, bf);
                    _log.AppendLine($"  {fn} = {(fi != null ? fi.GetValue(a2)?.ToString() : "<无此字段>")}");
                }
                var litArr = at.GetField("lightsToKill", bf)?.GetValue(a2) as System.Array;
                var flickArr = at.GetField("lightsToFlicker", bf)?.GetValue(a2) as System.Array;
                _log.AppendLine($"  lightsToKill 数量 = {litArr?.Length ?? 0}");
                _log.AppendLine($"  lightsToFlicker 数量 = {flickArr?.Length ?? 0}");
                var trig = a2.GetComponent<Collider>();
                _log.AppendLine($"  触发器 Collider = {(trig != null ? trig.GetType().Name : "❌ 无")} isTrigger={trig?.isTrigger}");
                _log.AppendLine($"  触发器位置 = {a2.transform.position}");

                // 灯的状态
                var litF = typeof(AbyssEcho.Horror.HorrorTrigger).GetField("lightsToKill",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                var lights = litF?.GetValue(a2) as Light[];
                _log.AppendLine($"  关联灯 = {lights?.Length ?? 0}");
                if (lights != null)
                    foreach (var l in lights)
                        _log.AppendLine($"     {(l != null ? l.gameObject.name : "NULL")} enabled={l?.enabled}");

                _log.AppendLine();
                _log.AppendLine("-- ③ 实际触发 --");
                _log.AppendLine($"  触发前 ambientLight = {RenderSettings.ambientLight}  fog={RenderSettings.fog}");

                // 直接把玩家传送到触发器位置（模拟走进去）
                var motor = FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
                if (motor != null)
                {
                    var cc = motor.GetComponent<CharacterController>();
                    if (cc != null) cc.enabled = false;
                    motor.transform.position = a2.transform.position + Vector3.up * 0.1f;
                    if (cc != null) cc.enabled = true;
                    _log.AppendLine($"  已把玩家移到 {motor.transform.position}");
                }

                // 等待 OnTriggerEnter + delay
                yield return new WaitForSeconds(3.5f);

                _log.AppendLine($"  触发后 lightsToKill 状态：");
                if (lights != null)
                    foreach (var l in lights)
                        _log.AppendLine($"     {(l != null ? l.gameObject.name : "NULL")} enabled={l?.enabled}");

                _log.AppendLine($"  触发后 ambientLight = {RenderSettings.ambientLight}");

                // FearSystem 是否收到
                var fear = FindFirstObjectByType<AbyssEcho.Horror.FearSystem>();
                _log.AppendLine($"  FearSystem = {(fear != null ? $"有，currentFear={fear.currentFear:F2}" : "❌ 无（A2 的 fearJump 无处落地）")}");

                // 重复触发检查
                var hasFiredF = typeof(AbyssEcho.Horror.HorrorTrigger).GetField("_fired",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                _log.AppendLine($"  _fired = {(hasFiredF != null ? hasFiredF.GetValue(a2)?.ToString() : "<无此字段>")}");
            }

            Dump();
        }

        private AbyssEcho.Interaction.DoorInteractable DoorA3_B;

        private void Dump()
        {
            string text = _log.ToString();
            Debug.Log("<b>[DoorChainProbe]</b>\n" + text);

            var di = new DirectoryInfo(Application.dataPath);
            for (int hop = 0; hop < 6 && di != null; hop++)
            {
                string reports = Path.Combine(di.FullName, "Reports");
                if (Directory.Exists(reports))
                {
                    try
                    {
                        File.WriteAllText(Path.Combine(reports, "DoorChainProbe.txt"), text, new UTF8Encoding(false));
                        break;
                    }
                    catch { }
                }
                di = di.Parent;
            }

            if (!Application.isEditor) Application.Quit(0);
        }
    }
}
