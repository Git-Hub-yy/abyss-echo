using System.Collections;
using System.Collections.Generic;
using AbyssEcho.Inventory;
using AbyssEcho.Player;
using UnityEngine;

namespace AbyssEcho.UI
{
    /// <summary>
    /// 物品栏 UI：按 Tab 打开，显示背包里的所有物品。
    ///
    /// 界面结构：
    ///   · 左侧：物品列表（色块 + 名称 + 数量）
    ///   · 右侧：选中物品的描述
    ///
    /// 交互：
    ///   · Tab 或 Esc 关闭
    ///   · 鼠标点击 / 滚轮切换选中项
    ///   · 新拾取物品时自动弹出 2 秒的提示条（不用手动开物品栏）
    ///
    /// 为什么用 OnGUI：
    ///   与已有 CrosshairUI / NarrativeUI / KeypadUI 保持一致，
    ///   避免混用 uGUI 造成层级冲突，后续统一替换成本低。
    /// </summary>
    public class InventoryUI : MonoBehaviour
    {
        [Header("样式")]
        public float panelWidth = 620f;
        public float panelHeight = 400f;
        public int titleFontSize = 28;
        public int itemFontSize = 19;
        public int descFontSize = 15;

        public Color panelColor = new Color(0.04f, 0.06f, 0.08f, 0.95f);
        public Color borderColor = new Color(0.35f, 0.62f, 0.85f, 0.85f);
        public Color titleColor = new Color(0.85f, 0.94f, 1f);
        public Color itemColor = new Color(0.88f, 0.92f, 0.96f);
        public Color selectedColor = new Color(0.45f, 0.85f, 1f);
        public Color descColor = new Color(0.70f, 0.78f, 0.86f);

        [Header("拾取提示")]
        public float toastDuration = 2.2f;
        public Color toastColor = new Color(0.45f, 1f, 0.65f);

        // ── 状态 ──
        private bool _isOpen;
        private int _selectedIndex;
        private readonly List<KeyValuePair<string, int>> _cachedItems = new List<KeyValuePair<string, int>>();

        // 拾取提示
        private string _toastText = "";
        private float _toastUntil;

        private PlayerInputReader _input;
        private PlayerMotor _motor;
        private PlayerLook _look;
        private AbyssEcho.Interaction.InteractionDetector _detector;

        private GUIStyle _titleStyle, _itemStyle, _selectedStyle, _descStyle, _toastStyle;
        private Texture2D _white;

        public bool IsOpen => _isOpen;

        private void Awake()
        {
            _input = FindFirstObjectByType<PlayerInputReader>();
            _motor = FindFirstObjectByType<PlayerMotor>();
            _look = FindFirstObjectByType<PlayerLook>();
            _detector = FindFirstObjectByType<AbyssEcho.Interaction.InteractionDetector>();
            _white = Texture2D.whiteTexture;
        }

        private void OnEnable()
        {
            AbyssEcho.Interaction.PickupItem.OnPickedUp += HandlePickedUp;
        }

        private void OnDisable()
        {
            AbyssEcho.Interaction.PickupItem.OnPickedUp -= HandlePickedUp;
        }

        private void HandlePickedUp(string itemId)
        {
            _toastText = "获得 " + ItemIds.GetDisplayName(itemId);
            _toastUntil = Time.time + toastDuration;
        }

        // ────────────────────────── 开关 ──────────────────────────

        public void Toggle()
        {
            if (_isOpen) Close();
            else Open();
        }

        public void Open()
        {
            // 其它面板打开时不叠加
            var narrative = FindFirstObjectByType<NarrativeUI>();
            if (narrative != null && narrative.IsOpen) return;

            var keypad = FindFirstObjectByType<KeypadUI>();
            if (keypad != null && keypad.IsOpen) return;

            var pause = FindFirstObjectByType<PauseMenuUI>();
            if (pause != null && pause.IsOpen) return;

            _isOpen = true;
            _selectedIndex = 0;
            RefreshCache();
            ApplyInputLock(true);
        }

        public void Close()
        {
            if (!_isOpen) return;
            _isOpen = false;
            ApplyInputLock(false);
        }

        private void ApplyInputLock(bool open)
        {
            if (_motor != null) _motor.enabled = !open;
            if (_look != null) _look.enabled = !open;
            if (_detector != null) _detector.enabled = !open;

            if (_input != null) _input.RefreshCursor();

            Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = open;
        }

        private void RefreshCache()
        {
            _cachedItems.Clear();
            var inv = PlayerInventory.Instance;
            if (inv == null) return;

            // ══════════════════ S79：物品列表补全 ══════════════════
            //
            //   玩家反馈："有的章节捡到物品无法显示在物品栏之中"
            //
            //   根因（第 142 行）：
            //     原代码只枚举 3 个 ID：
            //         string[] knownIds = { Fuse, AccessCard, Note };
            //
            //     但项目实际有 6 种可拾取物品。
            //     以下三种**永远不会出现在物品栏**：
            //         item.tape   录音带  （第二、三章）
            //         item.bulb   灯泡    （第三章）
            //         item.glove  工作手套（第二章）
            //
            //   修复：改为遍历 ItemIds.All 的完整列表。
            //   以后新增物品只要加到 ItemIds 就会自动显示，不再需要改这里。
            var knownIds = ItemIds.All;
            foreach (var id in knownIds)
            {
                int c = inv.Count(id);
                if (c > 0) _cachedItems.Add(new KeyValuePair<string, int>(id, c));
            }

            if (_selectedIndex >= _cachedItems.Count) _selectedIndex = Mathf.Max(0, _cachedItems.Count - 1);
        }

        private void Update()
        {
            // 开关
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                Toggle();
                return;
            }

            if (!_isOpen) return;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
                return;
            }

            // 上下切换
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
                _selectedIndex = Mathf.Max(0, _selectedIndex - 1);
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
                _selectedIndex = Mathf.Min(Mathf.Max(0, _cachedItems.Count - 1), _selectedIndex + 1);
        }

        // ────────────────────────── 绘制 ──────────────────────────

        private void OnGUI()
        {
            EnsureStyles();

            // 拾取提示（无论物品栏是否打开都显示）
            if (Time.time < _toastUntil && !string.IsNullOrEmpty(_toastText))
                DrawToast();

            if (!_isOpen) return;

            // 遮罩
            var prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _white);
            GUI.color = prev;

            float w = Mathf.Min(panelWidth, Screen.width - 60);
            float h = Mathf.Min(panelHeight, Screen.height - 100);
            var panel = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

            DrawPanelBackground(panel);

            var inner = new Rect(panel.x + 24, panel.y + 20, panel.width - 48, panel.height - 40);

            // 标题
            _titleStyle.normal.textColor = titleColor;
            GUI.Label(new Rect(inner.x, inner.y, inner.width, 34), "【物品栏】  ( Tab )", _titleStyle);

            // 左右分栏
            float listW = inner.width * 0.52f;
            var listRect = new Rect(inner.x, inner.y + 46, listW, inner.height - 76);
            var descRect = new Rect(inner.x + listW + 20, inner.y + 46,
                                    inner.width - listW - 20, inner.height - 76);

            DrawList(listRect);
            DrawDescription(descRect);

            // 底部提示
            _descStyle.normal.textColor = descColor;
            GUI.Label(new Rect(inner.x, inner.yMax - 26, inner.width, 22),
                      "Tab / Esc 关闭　·　↑↓ 或 W S 切换", _descStyle);
        }

        private void DrawList(Rect rect)
        {
            if (_cachedItems.Count == 0)
            {
                _descStyle.normal.textColor = descColor;
                GUI.Label(new Rect(rect.x, rect.y + 10, rect.width, 30), "（背包是空的）", _descStyle);
                return;
            }

            float rowH = 46f;
            for (int i = 0; i < _cachedItems.Count; i++)
            {
                var item = _cachedItems[i];
                var row = new Rect(rect.x, rect.y + i * (rowH + 6), rect.width, rowH);

                bool selected = i == _selectedIndex;
                bool hover = row.Contains(Event.current.mousePosition);

                // 背景
                var prevColor = GUI.color;
                GUI.color = selected ? new Color(0.15f, 0.28f, 0.40f, 0.95f)
                            : hover ? new Color(0.12f, 0.18f, 0.24f, 0.9f)
                                    : new Color(0.08f, 0.10f, 0.13f, 0.9f);
                GUI.DrawTexture(row, _white);
                GUI.color = prevColor;

                // 左侧色块
                var swatch = new Rect(row.x + 10, row.y + 11, 24, 24);
                GUI.color = ItemIds.GetTint(item.Key);
                GUI.DrawTexture(swatch, _white);
                GUI.color = prevColor;

                // 名称 + 数量
                string label = ItemIds.GetDisplayName(item.Key);
                if (item.Value > 1) label += "  ×" + item.Value;

                var style = selected ? _selectedStyle : _itemStyle;
                style.normal.textColor = selected ? selectedColor : itemColor;
                GUI.Label(new Rect(row.x + 44, row.y, row.width - 54, rowH), label, style);

                // 点击选择
                if (hover && Event.current.type == EventType.MouseDown && Event.current.button == 0)
                {
                    _selectedIndex = i;
                    Event.current.Use();
                }
            }
        }

        private void DrawDescription(Rect rect)
        {
            if (_cachedItems.Count == 0 || _selectedIndex >= _cachedItems.Count) return;

            string id = _cachedItems[_selectedIndex].Key;

            // 分隔线
            var prevColor = GUI.color;
            GUI.color = new Color(0.30f, 0.38f, 0.48f, 0.6f);
            GUI.DrawTexture(new Rect(rect.x - 10, rect.y, 1.5f, rect.height), _white);
            GUI.color = prevColor;

            _itemStyle.normal.textColor = selectedColor;
            GUI.Label(new Rect(rect.x + 6, rect.y, rect.width, 30), ItemIds.GetDisplayName(id), _itemStyle);

            _descStyle.normal.textColor = descColor;
            GUI.Label(new Rect(rect.x + 6, rect.y + 34, rect.width, rect.height - 34),
                      ItemIds.GetDescription(id), _descStyle);
        }

        private void DrawToast()
        {
            float alpha = Mathf.Clamp01((_toastUntil - Time.time) / 0.6f);
            var prev = GUI.color;

            _toastStyle.normal.textColor = new Color(toastColor.r, toastColor.g, toastColor.b, alpha);

            var content = new GUIContent(_toastText);
            Vector2 sz = _toastStyle.CalcSize(content);
            var r = new Rect((Screen.width - sz.x) * 0.5f, Screen.height * 0.30f, sz.x, sz.y);

            // 背景条
            GUI.color = new Color(0.05f, 0.10f, 0.08f, 0.75f * alpha);
            GUI.DrawTexture(new Rect(r.x - 14, r.y - 6, r.width + 28, r.height + 12), _white);
            GUI.color = prev;

            GUI.Label(r, content, _toastStyle);
            GUI.color = prev;
        }

        private void DrawPanelBackground(Rect r)
        {
            var prev = GUI.color;
            GUI.color = panelColor;
            GUI.DrawTexture(r, _white);

            GUI.color = borderColor;
            const float t = 2f;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, t), _white);
            GUI.DrawTexture(new Rect(r.x, r.yMax - t, r.width, t), _white);
            GUI.DrawTexture(new Rect(r.x, r.y, t, r.height), _white);
            GUI.DrawTexture(new Rect(r.xMax - t, r.y, t, r.height), _white);
            GUI.color = prev;
        }

        private void EnsureStyles()
        {
            if (_titleStyle == null)
            {
                _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = titleFontSize, fontStyle = FontStyle.Bold };
                _titleStyle.normal.textColor = titleColor;
            }
            if (_itemStyle == null)
            {
                _itemStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = itemFontSize,
                    alignment = TextAnchor.MiddleLeft
                };
                _itemStyle.normal.textColor = itemColor;
            }
            if (_selectedStyle == null)
            {
                _selectedStyle = new GUIStyle(_itemStyle) { fontStyle = FontStyle.Bold };
                _selectedStyle.normal.textColor = selectedColor;
            }
            if (_descStyle == null)
            {
                _descStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = descFontSize,
                    wordWrap = true,
                    alignment = TextAnchor.UpperLeft
                };
                _descStyle.normal.textColor = descColor;
            }
            if (_toastStyle == null)
            {
                _toastStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 20,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
            }
            ChineseFont.Apply(_titleStyle);
            ChineseFont.Apply(_itemStyle);
            ChineseFont.Apply(_selectedStyle);
            ChineseFont.Apply(_descStyle);
            ChineseFont.Apply(_toastStyle);
        }
    }
}
