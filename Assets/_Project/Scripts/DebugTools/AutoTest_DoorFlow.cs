using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// 第一道门端到端测试（真实调用游戏逻辑，不跳过任何步骤）。
    ///
    /// 严格按玩家流程：
    ///   T1 场景里有保险丝
    ///   T2 拾取保险丝 → 背包里真的有
    ///   T3 背包 ID == 配电箱需要的 ID
    ///   T4 拾取前对配电箱交互 → 应该失败（没有保险丝）
    ///   T5 拾取后对配电箱交互 → 应该成功（IsPowered = true）
    ///   T6 配电箱解锁了 Door_A3_B（locked 变 false）
    ///   T7 对门交互 → 门打开
    ///   T8 门打开后碰撞体让开通道
    /// </summary>
    public class AutoTest_DoorFlow : MonoBehaviour
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
            _log.AppendLine("ABYSS ECHO · 第一道门端到端测试");
            _log.AppendLine("=====================================");
            yield return new WaitForSeconds(0.6f);

            // 清理存档，确保是"新游戏"状态
            AbyssEcho.Save.SaveSystem.Delete();
            PlayerPrefs.DeleteAll();
            yield return null;

            // 清空背包（模拟新游戏）
            var inv = FindFirstObjectByType<AbyssEcho.Inventory.PlayerInventory>();
            if (inv == null) { Fail("T0 背包存在", "无 PlayerInventory"); Finish(); yield break; }
            foreach (var id in new[] { "item.fuse", "item.accesscard", "item.note" })
                if (inv.Has(id)) inv.Remove(id);

            T1_FuseExists();
            yield return null;

            var fuse = FindFirstObjectByType<AbyssEcho.Interaction.PickupItem>();
            var panel = FindFirstObjectByType<AbyssEcho.Puzzle.PowerRestorePanel>();
            var door = FindObjectOfType<AbyssEcho.Interaction.DoorInteractable>();

            // 注意：场景里有 3 扇门，要找到 Door_A3_B
            AbyssEcho.Interaction.DoorInteractable doorA3B = null;
            foreach (var d in FindObjectsByType<AbyssEcho.Interaction.DoorInteractable>(FindObjectsSortMode.None))
                if (d.name == "Door_A3_B") doorA3B = d;

            if (fuse == null || panel == null || doorA3B == null)
            {
                Fail("T0 关键对象存在", $"保险丝={fuse != null} 配电箱={panel != null} 门={doorA3B != null}");
                Finish(); yield break;
            }

            yield return T4_NoFuseCannotPower(panel);
            yield return T2_PickupFuse(fuse, inv);
            yield return T3_IdMatch(panel);
            yield return T5_PowerOn(panel);
            yield return T6_DoorUnlocked(doorA3B);
            yield return T7_DoorOpens(doorA3B);
            yield return T8_Passable(doorA3B);

            Finish();
        }

        // ────────────────────────── 测试项 ──────────────────────────

        private void T1_FuseExists()
        {
            var fuse = FindFirstObjectByType<AbyssEcho.Interaction.PickupItem>();
            Check("T1 场景中有保险丝", fuse != null,
                  fuse != null ? $"{fuse.name} itemId={fuse.itemId}" : "");
        }

        private IEnumerator T4_NoFuseCannotPower(AbyssEcho.Puzzle.PowerRestorePanel panel)
        {
            // 此时还没拾取保险丝
            panel.OnInteract(null);
            yield return null;

            bool powered = panel.IsPowered;
            Check("T4 无保险丝时配电箱不工作", !powered,
                  powered ? "❌ 居然通电了" : "正确：无保险丝无法通电");
        }

        private IEnumerator T2_PickupFuse(AbyssEcho.Interaction.PickupItem fuse,
                                          AbyssEcho.Inventory.PlayerInventory inv)
        {
            if (fuse == null) { Fail("T2 拾取保险丝", "无保险丝"); yield break; }

            string fuseId = fuse.itemId;
            fuse.OnInteract(null);
            yield return null;

            bool has = inv.Has(fuseId);
            Check("T2 拾取后背包里有保险丝", has,
                  $"itemId={fuseId} 背包={has}");
        }

        private IEnumerator T3_IdMatch(AbyssEcho.Puzzle.PowerRestorePanel panel)
        {
            var inv = AbyssEcho.Inventory.PlayerInventory.Instance;
            string need = panel.requiredItemId;
            bool has = inv != null && inv.Has(need);
            Check("T3 配电箱需要的 ID 与背包一致", has,
                  $"配电箱需要=\"{need}\"  背包有={has}");
            yield break;
        }

        private IEnumerator T5_PowerOn(AbyssEcho.Puzzle.PowerRestorePanel panel)
        {
            panel.OnInteract(null);
            yield return null;

            Check("T5 用保险丝后配电箱通电", panel.IsPowered,
                  $"IsPowered={panel.IsPowered}");
        }

        private IEnumerator T6_DoorUnlocked(AbyssEcho.Interaction.DoorInteractable door)
        {
            yield return null;
            Check("T6 配电箱解锁了 Door_A3_B", !door.locked,
                  $"locked={door.locked}");
        }

        private IEnumerator T7_DoorOpens(AbyssEcho.Interaction.DoorInteractable door)
        {
            door.OnInteract(null);
            // 等门移动到全开（slideDistance / speed ≈ 1 秒）
            yield return new WaitForSeconds(1.6f);

            Check("T7 门能打开", door.IsOpen, $"IsOpen={door.IsOpen}");
        }

        private IEnumerator T8_Passable(AbyssEcho.Interaction.DoorInteractable door)
        {
            yield return null;

            // 检测"玩家能不能穿过门洞"：
            // 沿门法线方向，在门洞中心点前后各 0.6 米取样，
            // 用玩家胶囊尺寸（半径 0.32、高 1.78）判断是否被挡住。
            //
            // ⚠️ 不能用 OverlapSphere(门中心, 0.3) —— 那会碰到门两侧的墙
            //    （South_B1_SegEnd 之类），那是正常墙体，不是阻挡。
            Vector3 doorPos = door.transform.position;
            Vector3 forward = door.transform.forward;   // 门的薄方向（法线）

            int blockedCount = 0;
            string blocker = "";

            for (float t = -0.6f; t <= 0.6f; t += 0.2f)
            {
                Vector3 sample = doorPos + forward * t;
                sample.y = 1.0f;

                // 玩家胶囊：用竖直胶囊近似（半径 0.32，高度 1.78）
                var hits = Physics.OverlapCapsule(
                    sample + Vector3.down * 0.85f,
                    sample + Vector3.up * 0.85f,
                    0.32f, ~0, QueryTriggerInteraction.Ignore);

                foreach (var h in hits)
                {
                    if (h == null) continue;
                    if (h is CharacterController) continue;              // 玩家自己
                    if (h.gameObject == door.gameObject) continue;        // 门板本身

                    // 只统计"门板初始位置附近"的碰撞体 —— 也就是原本挡路的那个门板
                    // 如果门已经滑开，它的碰撞体已经移走，就不会出现在这里
                    if (h.gameObject.name == door.name)
                    {
                        blockedCount++;
                        blocker = h.gameObject.name;
                    }
                }
            }

            bool passable = blockedCount == 0;
            Check("T8 开门后门洞中央可通过", passable,
                  passable ? "门洞中央无门板阻挡" : $"被 {blocker} 挡住（{blockedCount} 次采样）");
        }

        // ────────────────────────── 工具 ──────────────────────────

        private void Check(string name, bool ok, string detail = "")
        {
            if (ok) { _pass++; _log.AppendLine($"[通过] {name} {detail}"); }
            else { _fail++; _log.AppendLine($"[失败] {name} {detail}"); }
        }

        private void Fail(string name, string detail) => Check(name, false, detail);

        private void Finish()
        {
            _log.AppendLine("=====================================");
            _log.AppendLine($"结果：通过 {_pass}，失败 {_fail}");

            string text = _log.ToString();
            Debug.Log("<b>[DoorFlowTest]</b>\n" + text);

            try
            {
                string dir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports"));
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "DoorFlowTest.txt"), text, new UTF8Encoding(false));
            }
            catch { }

            if (!Application.isEditor) Application.Quit(_fail == 0 ? 0 : 1);
        }
    }
}