using System.Collections;
using System.IO;
using System.Text;
using AbyssEcho.Interaction;
using AbyssEcho.Inventory;
using AbyssEcho.Player;
using AbyssEcho.Puzzle;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// S4 交互系统自动测试。
    ///
    /// 测试策略：直接调用组件 API（不模拟物理射线），验证逻辑正确性。
    /// 射线检测本身属于 Unity 引擎功能，另有手动测试步骤覆盖。
    ///
    /// 通过标准：全部断言通过 → 退出码 0
    /// </summary>
    public class AutoTest_S4 : MonoBehaviour
    {
        public bool runOnStart = true;

        private int _pass, _fail;
        private readonly StringBuilder _log = new StringBuilder();

        private void Start()
        {
            if (runOnStart) StartCoroutine(RunAll());
        }

        private IEnumerator RunAll()
        {
            _log.AppendLine("ABYSS ECHO · S4 交互系统自动测试");
            _log.AppendLine("=====================================");

            yield return new WaitForSeconds(0.5f);

            T01_InventoryExists();
            T02_InventoryAddRemove();
            T03_PickupExists();
            T04_PickupWorks();
            T05_PowerPanelExists();
            T06_PowerPanelRequiresFuse();
            T07_PowerRestored();
            T08_DoorExists();
            T09_DoorLocked();
            T10_DetectorExists();
            T11_CrosshairExists();
            T12_InteractableCount();

            _log.AppendLine("=====================================");
            _log.AppendLine($"结果：通过 {_pass}，失败 {_fail}");

            string text = _log.ToString();
            Debug.Log("<b>[AutoTest_S4]</b>\n" + text);
            WriteReport(text);

            yield return new WaitForSeconds(0.2f);
            if (!Application.isEditor) Application.Quit(_fail == 0 ? 0 : 1);
        }

        // ────────────────────────── 测试用例 ──────────────────────────

        private void T01_InventoryExists()
        {
            Check("T01 背包存在", PlayerInventory.Instance != null);
        }

        private void T02_InventoryAddRemove()
        {
            var inv = PlayerInventory.Instance;
            if (inv == null) { Fail("T02 背包增删", "背包为空"); return; }

            inv.Clear();
            inv.Add("test.item", 2);
            bool added = inv.Count("test.item") == 2;
            inv.Add("test.item", 1);
            bool stacked = inv.Count("test.item") == 3;
            bool removed = inv.Remove("test.item", 2) && inv.Count("test.item") == 1;
            bool failRemove = !inv.Remove("nonexistent", 1);
            inv.Clear();
            bool cleared = inv.Count("test.item") == 0;

            Check("T02 背包增删", added && stacked && removed && failRemove && cleared,
                  $"added={added} stacked={stacked} removed={removed} failRemove={failRemove} cleared={cleared}");
        }

        private void T03_PickupExists()
        {
            var pickups = FindObjectsByType<PickupItem>(FindObjectsSortMode.None);
            Check("T03 保险丝拾取物已放置", pickups.Length >= 1, $"找到 {pickups.Length} 个");
        }

        private void T04_PickupWorks()
        {
            var pickups = FindObjectsByType<PickupItem>(FindObjectsSortMode.None);
            if (pickups.Length == 0) { Fail("T04 拾取功能", "无拾取物"); return; }

            var inv = PlayerInventory.Instance;
            inv.Clear();
            var p = pickups[0];
            string id = p.itemId;
            p.OnInteract(null);

            bool got = inv.Has(id);
            Check("T04 拾取功能", got, $"拾取 {id} 后背包有该物品={got}");
        }

        private void T05_PowerPanelExists()
        {
            var panels = FindObjectsByType<PowerRestorePanel>(FindObjectsSortMode.None);
            Check("T05 配电箱已放置", panels.Length >= 1, $"找到 {panels.Length} 个");
        }

        private void T06_PowerPanelRequiresFuse()
        {
            var panels = FindObjectsByType<PowerRestorePanel>(FindObjectsSortMode.None);
            if (panels.Length == 0) { Fail("T06 无保险丝时不能通电", "无配电箱"); return; }

            var inv = PlayerInventory.Instance;
            var panel = panels[0];

            // 没有保险丝 → 不应通电
            inv.Clear();
            panel.OnInteract(null);
            bool blocked = !panel.IsPowered;

            // 有保险丝 → 应该通电
            inv.Add(panel.requiredItemId, 1);
            panel.OnInteract(null);
            bool powered = panel.IsPowered;

            Check("T06 无保险丝时不能通电 / 有则通电", blocked && powered,
                  $"blocked={blocked} powered={powered}");
        }

        private void T07_PowerRestored()
        {
            var panels = FindObjectsByType<PowerRestorePanel>(FindObjectsSortMode.None);
            if (panels.Length == 0) { Fail("T07 通电后解锁门", "无配电箱"); return; }

            var panel = panels[0];
            bool panelPowered = panel.IsPowered;

            // 检查配电箱关联的门是否解锁
            bool doorUnlocked = true;
            if (panel.doorsToUnlock != null && panel.doorsToUnlock.Length > 0)
            {
                doorUnlocked = true;
                foreach (var d in panel.doorsToUnlock)
                    if (d != null && d.locked) doorUnlocked = false;
            }

            Check("T07 通电后状态正确", panelPowered && doorUnlocked,
                  $"panelPowered={panelPowered} doorUnlocked={doorUnlocked}");
        }

        private void T08_DoorExists()
        {
            var doors = FindObjectsByType<DoorInteractable>(FindObjectsSortMode.None);
            Check("T08 交互门已放置", doors.Length >= 1, $"找到 {doors.Length} 个");
        }

        private void T09_DoorLocked()
        {
            var doors = FindObjectsByType<DoorInteractable>(FindObjectsSortMode.None);
            if (doors.Length == 0) { Fail("T09 门开合逻辑", "无门"); return; }

            var d = doors[0];
            bool wasOpen = d.IsOpen;
            d.OnInteract(null);
            yield_return_delay();
            bool changed = d.IsOpen != wasOpen;

            // 还原
            d.OnInteract(null);

            Check("T09 门可以开合", changed, $"之前={wasOpen} 之后={d.IsOpen}");
        }

        private void yield_return_delay() { }

        private void T10_DetectorExists()
        {
            var det = FindFirstObjectByType<InteractionDetector>();
            Check("T10 交互检测器已挂载", det != null);
        }

        private void T11_CrosshairExists()
        {
            var ui = FindFirstObjectByType<AbyssEcho.UI.CrosshairUI>();
            Check("T11 准星 UI 已挂载", ui != null);
        }

        private void T12_InteractableCount()
        {
            var all = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            int n = 0;
            foreach (var m in all) if (m is IInteractable) n++;
            Check("T12 可交互物数量 ≥ 3", n >= 3, $"找到 {n} 个");
        }

        // ────────────────────────── 工具 ──────────────────────────

        private void Check(string name, bool ok, string detail = "")
        {
            if (ok) { _pass++; _log.AppendLine($"[通过] {name} {detail}"); }
            else { _fail++; _log.AppendLine($"[失败] {name} {detail}"); }
        }

        private void Fail(string name, string detail)
        {
            _fail++;
            _log.AppendLine($"[失败] {name} {detail}");
        }

        private static void WriteReport(string text)
        {
            try
            {
                string dir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports"));
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "S4_AutoTestResult.txt"), text, new UTF8Encoding(false));
            }
            catch { }
        }
    }
}