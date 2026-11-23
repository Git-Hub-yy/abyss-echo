using System;
using AbyssEcho.Interaction;
using UnityEngine;

namespace AbyssEcho.Puzzle
{
    /// <summary>
    /// 供奉台（第三章 F9 祭室）。
    ///
    /// ══════════════════════ 设计意图 ══════════════════════
    ///
    ///  这是全游戏**第一次真正影响结局的选择**。
    ///
    ///  前面的章节都是"开门 → 前进"的线性流程，
    ///  这里玩家必须主动"给出一样东西"，而且给什么决定了结局。
    ///
    /// ══════════════════════ 三个结局 ══════════════════════
    ///
    ///   放录音带 → 结局 A · 回声
    ///     你被接受，成为新的"下面的人"。循环闭合。
    ///
    ///   放保险丝 → 结局 C · 深入
    ///     灯全亮，露出更深的通道 → 通向第四章。
    ///
    ///   空手离开 → 结局 B · 离开
    ///     你转身往回走，灯一盏盏熄灭。开放式。
    ///
    /// ══════════════════════ 为什么需要"离开"这条路径 ══════════════════════
    ///
    ///  如果只有"放东西"才能触发结局，那玩家没带东西就会卡关。
    ///  所以放置台旁边有一个"转身离开"的出口 —— 什么都不做本身就是选择。
    /// </summary>
    public class OfferingAltar : InteractableBase
    {
        [Header("接受的供品")]
        [Tooltip("放这个 → 结局 A（回声）")]
        public string itemForEndingA = "item.tape";

        [Tooltip("放这个 → 结局 C（深入）")]
        public string itemForEndingC = "item.fuse";

        [Header("联动")]
        [Tooltip("放置成功后解锁的门（通往 F7 镜沼）")]
        public DoorInteractable doorToUnlock;

        [Tooltip("放置成功后点亮的灯")]
        public Light[] lightsToEnable;

        [Tooltip("供品放上去后的视觉标记")]
        public Transform offeringMarker;

        [Header("提示文案")]
        public string promptNeedItem = "供奉台是空的 —— 把一样东西放上去";
        public string promptAlready = "供奉台已经放上了东西";

        /// <summary>是否已经供奉过。</summary>
        public bool HasOffered { get; private set; }

        /// <summary>玩家供奉了什么（空字符串 = 没放）。</summary>
        public string OfferedItemId { get; private set; } = "";

        /// <summary>供奉完成事件（参数为物品 ID）。</summary>
        public static event Action<string> OnOffered;

        /// <summary>
        /// 提示文本。
        ///
        /// ══════════════════ S66：让玩家知道「会放下哪一件」══════════════════
        ///
        ///   玩家反馈："捡到多个物体之后，无法知晓放的是哪个"
        ///
        ///   原来提示永远是"供奉台是空的 —— 把一样东西放上去"，
        ///   完全不反映背包状态。而实际放置逻辑是硬编码优先级
        ///   （录音带 > 保险丝），玩家带两样东西时永远只放录音带，
        ///   自己却不知道。
        ///
        ///   现在改成：根据背包里实际有的东西，明确写出"将放下：XXX"。
        /// </summary>
        protected override string DefaultPrompt
        {
            get
            {
                if (HasOffered) return promptAlready;

                var inv = AbyssEcho.Inventory.PlayerInventory.Instance;
                if (inv == null) return promptNeedItem;

                // 按与 OnInteract 相同的优先级，算出"按下去会放什么"
                if (!string.IsNullOrEmpty(itemForEndingA) && inv.Has(itemForEndingA))
                    return $"按 E 放下「{ItemDisplayName(itemForEndingA)}」";

                if (!string.IsNullOrEmpty(itemForEndingC) && inv.Has(itemForEndingC))
                    return $"按 E 放下「{ItemDisplayName(itemForEndingC)}」";

                // 有灯泡但这里不收 —— 明确告诉玩家该去哪
                if (inv.Has(AbyssEcho.Inventory.ItemIds.Bulb))
                    return "供奉台不收灯泡 —— 去祭室北侧的长明灯座";

                return promptNeedItem;
            }
        }

        /// <summary>把物品 ID 转成中文名（找不到就用原始 ID）。</summary>
        private static string ItemDisplayName(string itemId)
        {
            try
            {
                var inv = AbyssEcho.Inventory.PlayerInventory.Instance;
                if (inv != null)
                {
                    var m = inv.GetType().GetMethod("GetDisplayName",
                        System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic);
                    if (m != null) return m.Invoke(inv, new object[] { itemId }) as string ?? itemId;
                }
            }
            catch { }

            switch (itemId)
            {
                case AbyssEcho.Inventory.ItemIds.Tape: return "录音带";
                case AbyssEcho.Inventory.ItemIds.Fuse: return "保险丝";
                case AbyssEcho.Inventory.ItemIds.Bulb: return "灯泡";
                default: return itemId;
            }
        }

        public override void OnInteract(GameObject interactor)
        {
            if (HasOffered) return;

            var inv = AbyssEcho.Inventory.PlayerInventory.Instance;
            if (inv == null)
            {
                Debug.LogWarning("[OfferingAltar] 找不到 PlayerInventory");
                return;
            }

            // 按优先级检查：录音带优先，其次保险丝
            if (!string.IsNullOrEmpty(itemForEndingA) && inv.Has(itemForEndingA))
            {
                Offer(itemForEndingA);
                return;
            }

            if (!string.IsNullOrEmpty(itemForEndingC) && inv.Has(itemForEndingC))
            {
                Offer(itemForEndingC);
                return;
            }

            // 什么都没有 → 给出明确提示，不消耗、不卡关
            if (AbyssEcho.Audio.AudioManager.Instance != null)
                AbyssEcho.Audio.AudioManager.Instance.PlayError();

            Debug.Log("[OfferingAltar] 玩家没有可供奉的物品（录音带 / 保险丝）");
        }

        /// <summary>放上供品。</summary>
        public void Offer(string itemId)
        {
            if (HasOffered) return;

            var inv = AbyssEcho.Inventory.PlayerInventory.Instance;
            if (inv != null) inv.Remove(itemId);

            HasOffered = true;
            OfferedItemId = itemId;

            // 视觉反馈
            if (offeringMarker != null) offeringMarker.gameObject.SetActive(true);

            if (lightsToEnable != null)
                foreach (var l in lightsToEnable)
                    if (l != null) l.enabled = true;

            if (doorToUnlock != null) doorToUnlock.locked = false;

            if (AbyssEcho.Audio.AudioManager.Instance != null)
                AbyssEcho.Audio.AudioManager.Instance.PlayUiBlip();

            if (AbyssEcho.Horror.FearSystem.Instance != null)
                AbyssEcho.Horror.FearSystem.Instance.AddFear(0.15f);

            Debug.Log($"[OfferingAltar] 已供奉：{itemId}");

            OnOffered?.Invoke(itemId);
        }

        /// <summary>判断供奉的是哪个结局分支。</summary>
        public string GetEndingId()
        {
            if (!HasOffered) return "B";                      // 没放 → 离开
            if (OfferedItemId == itemForEndingA) return "A";  // 录音带 → 回声
            if (OfferedItemId == itemForEndingC) return "C";  // 保险丝 → 深入
            return "B";
        }

        /// <summary>重置（测试用）。</summary>
        public void ResetAltar()
        {
            HasOffered = false;
            OfferedItemId = "";
            if (offeringMarker != null) offeringMarker.gameObject.SetActive(false);
        }
    }
}