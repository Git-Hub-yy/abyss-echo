using System;
using System.Collections.Generic;

namespace AbyssEcho.Inventory
{
    /// <summary>物品定义（轻量数据，后续可改为 ScriptableObject 资产）。</summary>
    [Serializable]
    public class ItemDef
    {
        public string Id;
        public string DisplayName;
        public string Description;

        public ItemDef(string id, string displayName, string description = "")
        {
            Id = id;
            DisplayName = displayName;
            Description = description;
        }
    }

    /// <summary>
    /// 玩家背包：记录持有的物品，派发增删事件供 UI 订阅。
    ///
    /// 设计要点：
    ///   - 纯 C# 逻辑（MonoBehaviour 仅作挂载壳），便于单元测试
    ///   - 同一物品可叠加计数（count）
    ///   - 不负责"拾取判定"，那是 PickupItem 的职责
    /// </summary>
    public class PlayerInventory : UnityEngine.MonoBehaviour
    {
        /// <summary>物品变化时触发（物品 ID, 当前数量）。</summary>
        public event Action<string, int> OnItemChanged;

        private readonly Dictionary<string, int> _items = new Dictionary<string, int>();

        /// <summary>是否为单例（场景中应只有一个背包）。</summary>
        public static PlayerInventory Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                UnityEngine.Debug.LogWarning("[PlayerInventory] 场景中存在多个背包，已销毁多余的");
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>添加物品。</summary>
        public void Add(string itemId, int count = 1)
        {
            if (string.IsNullOrEmpty(itemId) || count <= 0) return;

            _items.TryGetValue(itemId, out int current);
            _items[itemId] = current + count;
            OnItemChanged?.Invoke(itemId, _items[itemId]);
            UnityEngine.Debug.Log($"[Inventory] 获得 {itemId} ×{count}（当前 {_items[itemId]}）");
        }

        /// <summary>移除物品；数量不足时返回 false 且不改变状态。</summary>
        public bool Remove(string itemId, int count = 1)
        {
            if (string.IsNullOrEmpty(itemId) || count <= 0) return false;
            if (!_items.TryGetValue(itemId, out int current) || current < count) return false;

            int left = current - count;
            if (left <= 0) _items.Remove(itemId);
            else _items[itemId] = left;

            OnItemChanged?.Invoke(itemId, left);
            return true;
        }

        /// <summary>当前持有数量。</summary>
        public int Count(string itemId)
        {
            return _items.TryGetValue(itemId, out int c) ? c : 0;
        }

        /// <summary>
        /// S79：遍历已持有的所有物品（供 UI 显示，不再需要硬编码 ID 列表）。
        ///
        /// 为什么加这个：
        ///   InventoryUI.RefreshCache() 原来硬编码只查 3 个 ID，
        ///   导致录音带 / 灯泡 / 工作手套永远不显示在物品栏里。
        ///
        ///   有了这个方法，UI 可以直接遍历背包里的真实内容，
        ///   以后新增物品不需要改 UI 代码。
        /// </summary>
        public IEnumerable<KeyValuePair<string, int>> GetAll()
        {
            foreach (var kv in _items) yield return kv;
        }

        /// <summary>已持有物品的种类数。</summary>
        public int KindCount => _items.Count;

        public bool Has(string itemId, int count = 1) => Count(itemId) >= count;

        /// <summary>清空背包（换章节/重开时用）。</summary>
        public void Clear()
        {
            var ids = new List<string>(_items.Keys);
            _items.Clear();
            foreach (var id in ids) OnItemChanged?.Invoke(id, 0);
        }
    }

    /// <summary>第一章使用的物品 ID 常量（集中定义，避免拼写错误）。</summary>
    public static class ItemIds
    {
        public const string Fuse = "item.fuse";           // 保险丝
        public const string AccessCard = "item.accesscard"; // 门禁卡
        public const string Note = "item.note";           // 笔记

        // ── 第三章（S35）新增 ──
        //   录音带：从第二章 D6 卧房带下来的，是供奉选项之一（→ 结局 A）
        public const string Tape = "item.tape";
        //   灯泡：第三章 F4 工作间可拾取，用来修好 F 区的坏灯（→ 影响模仿症）
        public const string Bulb = "item.bulb";

        // ── 第二章（S43）新增 ──
        //   工作手套：D3 泵房可拾取。抽水泵的操作道具。
        public const string Glove = "item.glove";

        /// <summary>
        /// S79：**所有可拾取物品 ID 的完整列表**。
        ///
        /// 为什么需要它：
        ///   InventoryUI 原来硬编码只查 3 个 ID（Fuse / AccessCard / Note），
        ///   导致录音带、灯泡、工作手套捡到后**不出现在物品栏**。
        ///
        ///   新增物品时只需在这里加一行，UI 会自动识别。
        /// </summary>
        public static readonly string[] All =
        {
            Fuse,          // 保险丝
            AccessCard,    // 门禁卡
            Note,          // 笔记
            Tape,          // 录音带
            Bulb,          // 灯泡
            Glove,         // 工作手套
        };

        /// <summary>
        /// 物品 ID 到显示名的映射（UI 用）。
        ///
        /// 为什么需要这张表：
        ///   背包里只存 ID，UI 需要显示中文名。
        ///   如果让 PickupItem 直接写中文名，同一种物品在不同地方可能写成不同名字，
        ///   集中在一处维护更可靠。
        /// </summary>
        public static string GetDisplayName(string id)
        {
            switch (id)
            {
                case Fuse: return "保险丝";
                case AccessCard: return "门禁卡";
                case Note: return "笔记";
                case Tape: return "录音带";
                case Bulb: return "灯泡";
                case Glove: return "工作手套";
                default: return id;
            }
        }

        /// <summary>物品描述（显示在物品栏的副标题）。</summary>
        public static string GetDescription(string id)
        {
            switch (id)
            {
                case Fuse: return "一段老化的陶瓷保险丝，还能用。";
                case AccessCard: return "过期的门禁卡。";
                case Note: return "写着数字的便条。";
                case Tape: return "从下面带上来的一卷旧磁带。标签已经模糊了。";
                case Bulb: return "一颗还在包装里的灯泡。";
                case Glove: return "一双厚实的橡胶手套，指尖磨破了。";

                default: return "";
            }
        }

        /// <summary>物品在物品栏里的简要标记色（用于左侧小方块）。</summary>
        public static UnityEngine.Color GetTint(string id)
        {
            switch (id)
            {
                case Fuse: return new UnityEngine.Color(1f, 0.72f, 0.18f);
                case AccessCard: return new UnityEngine.Color(0.45f, 0.80f, 1f);
                case Note: return new UnityEngine.Color(0.95f, 0.92f, 0.80f);
                default: return new UnityEngine.Color(0.7f, 0.7f, 0.7f);
            }
        }
    }
}