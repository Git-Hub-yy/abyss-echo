using System;
using AbyssEcho.Interaction;
using UnityEngine;

namespace AbyssEcho.Puzzle
{
    /// <summary>
    /// 长明灯座（第三章 F9 祭室）。
    ///
    /// ══════════════════════ 为什么需要 ══════════════════════
    ///
    ///   第三章场景里本来就有两个空灯座：
    ///     长明灯座1 @ (11.6, 0.4, 32.4)
    ///     长明灯座2 @ (16.4, 0.4, 32.4)
    ///
    ///   而玩家能在 F4 工作间捡到「灯泡」(Pickup_Bulb)。
    ///   但供奉台只认录音带和保险丝 —— **灯泡捡了没有任何用途**，
    ///   是个死道具。
    ///
    ///   本组件把灯泡接进结局系统：
    ///     走到灯座前按 E → 拧上灯泡 → 灯座亮起 → 结局 D「照亮」
    ///
    /// ══════════════════════ 与供奉台的区别 ══════════════════════
    ///
    ///   供奉台 = 「献出」：把东西放下，交给它
    ///   灯座   = 「点亮」：把光装上，看清这里
    ///
    ///   两者语义不同，所以结局也不同：
    ///     A 回声（录音带） / B 离开（空手） / C 深入（保险丝） / D 照亮（灯泡）
    ///
    /// ══════════════════════ 互斥 ══════════════════════
    ///
    ///   灯座和供奉台共用同一套结局判定（ChapterEndingSystem.ResolveEndingId）。
    ///   谁先被激活，谁决定结局：
    ///     · 先放灯泡 → endingId = "D"
    ///     · 先供奉   → endingId = A/B/C
    ///   都激活过时以先发生的为准，不会来回切换。
    /// </summary>
    public class LampSocket : InteractableBase
    {
        [Header("接受的道具")]
        [Tooltip("放这个道具 → 点亮灯座 → 结局 D")]
        public string requiredItemId = "item.bulb";

        [Header("联动")]
        [Tooltip("点亮后打开的灯（灯座本身的灯泡发光）")]
        public Light lampLight;

        [Tooltip("灯泡亮起的视觉标记")]
        public Transform bulbMarker;

        [Tooltip("第二个灯座的联动（两个灯座可以一起亮）")]
        public LampSocket sibling;

        [Header("提示文案")]
        public string promptNeedItem = "灯座是空的 —— 需要一颗灯泡";
        public string promptAlready = "长明灯已经亮了";

        /// <summary>是否已点亮。</summary>
        public bool IsLit { get; private set; }

        /// <summary>点亮完成事件。</summary>
        public static event Action<LampSocket> OnLit;

        /// <summary>
        /// 提示文本。
        ///
        /// S66：根据背包状态给出明确提示，让玩家知道「会装上去的是什么」。
        /// </summary>
        protected override string DefaultPrompt
        {
            get
            {
                if (IsLit) return promptAlready;

                var inv = AbyssEcho.Inventory.PlayerInventory.Instance;
                if (inv == null) return promptNeedItem;

                if (!string.IsNullOrEmpty(requiredItemId) && inv.Has(requiredItemId))
                {
                    string name = requiredItemId == AbyssEcho.Inventory.ItemIds.Bulb
                        ? "灯泡" : requiredItemId;
                    return $"按 E 装上「{name}」";
                }

                return promptNeedItem;
            }
        }

        public override void OnInteract(GameObject interactor)
        {
            if (IsLit) return;

            var inv = AbyssEcho.Inventory.PlayerInventory.Instance;
            if (inv == null)
            {
                Debug.LogWarning("[LampSocket] 找不到 PlayerInventory");
                return;
            }

            if (string.IsNullOrEmpty(requiredItemId) || !inv.Has(requiredItemId))
            {
                if (AbyssEcho.Audio.AudioManager.Instance != null)
                    AbyssEcho.Audio.AudioManager.Instance.PlayError();

                Debug.Log($"[LampSocket] 玩家没有 {requiredItemId}");
                return;
            }

            Light(requiredItemId);
        }

        /// <summary>装上灯泡，点亮。</summary>
        public void Light(string itemId)
        {
            if (IsLit) return;

            var inv = AbyssEcho.Inventory.PlayerInventory.Instance;
            if (inv != null) inv.Remove(itemId);

            IsLit = true;

            if (bulbMarker != null) bulbMarker.gameObject.SetActive(true);

            if (lampLight != null)
            {
                lampLight.enabled = true;
                lampLight.intensity = Mathf.Max(lampLight.intensity, 6f);
            }

            if (AbyssEcho.Audio.AudioManager.Instance != null)
                AbyssEcho.Audio.AudioManager.Instance.PlayUiBlip();

            if (AbyssEcho.Horror.FearSystem.Instance != null)
                AbyssEcho.Horror.FearSystem.Instance.AddFear(0.1f);

            Debug.Log($"[LampSocket] {name} 已点亮（{itemId}）");

            OnLit?.Invoke(this);
        }

        /// <summary>重置（测试用）。</summary>
        public void ResetSocket()
        {
            IsLit = false;
            if (bulbMarker != null) bulbMarker.gameObject.SetActive(false);
            if (lampLight != null) lampLight.enabled = false;
        }
    }
}