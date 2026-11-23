using System;
using AbyssEcho.Interaction;
using AbyssEcho.Inventory;
using UnityEngine;

namespace AbyssEcho.Puzzle
{
    /// <summary>
    /// 谜题 1：电力恢复。
    ///
    /// 流程（对应《05 第一章流程》）：
    ///   1. 玩家在 A3 接待室找到【保险丝】（PickupItem）
    ///   2. 到 A3 配电箱前交互（本类）
    ///   3. 若背包有保险丝 → 安装成功 → 恢复供电
    ///   4. 恢复供电后：点亮区域内应急灯 + 解锁通往 B 区的门
    ///
    /// 状态机：Idle → Powered
    /// </summary>
    public class PowerRestorePanel : InteractableBase
    {
        [Header("需求")]
        [Tooltip("需要的物品 ID")]
        public string requiredItemId = ItemIds.Fuse;

        [Tooltip("是否消耗该物品")]
        public bool consumeItem = true;

        [Header("通电后要点亮的灯")]
        [Tooltip("通电后启用的光源（可从场景拖入）")]
        public Light[] lightsToEnable;

        [Tooltip("通电后要点亮的自发光物体（材质会自动切换）")]
        public Renderer[] indicatorRenderers;

        public Color indicatorOnColor = new Color(0.2f, 1f, 0.45f);
        public Color indicatorOffColor = new Color(0.9f, 0.2f, 0.15f);

        [Header("联动")]
        [Tooltip("通电后解锁的门")]
        public DoorInteractable[] doorsToUnlock;

        /// <summary>供电恢复事件（一次性）。</summary>
        public static event Action OnPowerRestored;

        /// <summary>是否已通电。</summary>
        public bool IsPowered { get; private set; }

        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        protected override string DefaultPrompt
        {
            get
            {
                if (IsPowered) return "电力已恢复";
                return "安装保险丝";
            }
        }

        protected override void Awake()
        {
            base.Awake();
            ApplyIndicator(false);
        }

        public override string GetPrompt()
        {
            if (IsPowered) return "电力已恢复";

            var inv = PlayerInventory.Instance;
            bool has = inv != null && inv.Has(requiredItemId);
            return has ? "安装保险丝" : "配电箱：缺少保险丝";
        }

        public override void OnInteract(GameObject interactor)
        {
            if (!CanInteract || IsPowered) return;

            var inv = PlayerInventory.Instance;
            if (inv == null || !inv.Has(requiredItemId))
            {
                Debug.Log("[Power] 没有保险丝，无法恢复电力");
                return;
            }

            if (consumeItem) inv.Remove(requiredItemId);

            IsPowered = true;
            ApplyIndicator(true);

            // 点亮灯光
            if (lightsToEnable != null)
                foreach (var l in lightsToEnable)
                    if (l != null) l.enabled = true;

            // 解锁门
            if (doorsToUnlock != null)
                foreach (var d in doorsToUnlock)
                    if (d != null) d.locked = false;

            Debug.Log("[Power] 电力已恢复！");
            OnPowerRestored?.Invoke();

            OnFocusExit();
        }

        private void ApplyIndicator(bool on)
        {
            if (indicatorRenderers == null) return;

            var mpb = new MaterialPropertyBlock();
            foreach (var r in indicatorRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(mpb);
                mpb.SetColor(EmissionId, on ? indicatorOnColor * 2f : indicatorOffColor * 1.2f);
                r.SetPropertyBlock(mpb);
            }
        }
    }
}