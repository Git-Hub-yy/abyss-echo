using AbyssEcho.Inventory;
using UnityEngine;

namespace AbyssEcho.Interaction
{
    /// <summary>
    /// 可拾取物品：注视后按交互键收入背包。
    ///
    /// 拾取后：
    ///   - 物体消失（Destroy）
    ///   - 派发 OnPickedUp 事件供任务系统/音效订阅
    /// </summary>
    public class PickupItem : InteractableBase
    {
        [Header("物品")]
        [Tooltip("物品 ID（与 PlayerInventory 中保持一致）")]
        public string itemId = ItemIds.Fuse;

        [Tooltip("显示名")]
        public string displayName = "保险丝";

        [Tooltip("拾取数量")]
        public int amount = 1;

        [Tooltip("拾取后是否销毁物体")]
        public bool destroyOnPickup = true;

        /// <summary>拾取完成事件（物品 ID）。</summary>
        public static event System.Action<string> OnPickedUp;

        protected override string DefaultPrompt => "拾取 " + displayName;

        public override void OnInteract(GameObject interactor)
        {
            if (!CanInteract) return;

            var inv = PlayerInventory.Instance;
            if (inv == null)
            {
                Debug.LogWarning("[PickupItem] 场景中没有 PlayerInventory，无法拾取");
                return;
            }

            inv.Add(itemId, amount);
            OnPickedUp?.Invoke(itemId);

            // ══════════════════ S79：拾取后写入日志 ══════════════════
            //
            //   玩家反馈："有的章节捡到物品无法显示在日志之中"
            //
            //   根因：原代码只调用了 inv.Add()（写背包），
            //         **完全没有写日志** —— 物品从来没进过日志系统。
            //
            //   修复：拾取成功后写入一条日志。
            //   日志正文用 ItemIds.GetDescription()，读起来像"玩家记下了一条线索"。
            if (AbyssEcho.Narrative.JournalSystem.Instance != null)
            {
                string title = ItemIds.GetDisplayName(itemId);
                if (amount > 1) title += $" ×{amount}";
                string body = ItemIds.GetDescription(itemId);
                if (string.IsNullOrEmpty(body)) body = "你把它收进了背包。";

                AbyssEcho.Narrative.JournalSystem.Instance.AddEntry(title, body);
            }

            interactable = false;
            Debug.Log($"[PickupItem] 已拾取 {displayName}（{itemId} ×{amount}）");

            // 音效 + 轻微恐惧缓解（拿到东西让玩家有安全感）
            if (AbyssEcho.Audio.AudioManager.Instance != null)
            {
                AbyssEcho.Audio.AudioManager.Instance.PlayUiBlip();
            }
            if (AbyssEcho.Horror.FearSystem.Instance != null)
            {
                AbyssEcho.Horror.FearSystem.Instance.AddFear(-0.08f);
            }

            if (destroyOnPickup)
            {
                OnFocusExit();
                Destroy(gameObject);
            }
        }
    }
}