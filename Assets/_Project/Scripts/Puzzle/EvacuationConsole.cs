using System;
using AbyssEcho.Interaction;
using UnityEngine;

namespace AbyssEcho.Puzzle
{
    /// <summary>
    /// 第五章终局选择：撤离协议（H7 出口舱）。
    ///
    /// ══════════════════════ 叙事 ══════════════════════
    ///
    ///  第五章的问题是"你要带走什么"。
    ///
    ///  出口舱的控制台提供三个选项，每个都对应一个结局：
    ///
    ///    「只带自己走」   → 结局 A · 独行
    ///        你一个人出去了。门在你身后关上。
    ///        什么都没带出来。但你也什么都没留下。
    ///
    ///    「带走记录」     → 结局 B · 证词（推荐/完整结局）
    ///        你带出了数据/录音。
    ///        世界会知道下面有什么。
    ///
    ///    「留下，关上它」 → 结局 C · 封门（隐藏/最沉重）
    ///        你选择留在下面，把入口封死。
    ///        因为你想起工牌上的日期是十九年前。
    ///
    /// ══════════════════════ 设计意图 ══════════════════════
    ///
    ///  第三章已经有"供奉三结局"（选物品）。
    ///  为避免重复，第五章的选择**不消耗物品**，而是**决定带走/留下**，
    ///  语义上更接近"价值观选择"而不是"资源交换"。
    ///
    ///  三个选项都有明确的道德重量，没有"正确答案"。
    /// </summary>
    public class EvacuationConsole : InteractableBase
    {
        [Header("选项结果")]
        [Tooltip("结局 A：只带自己走")]
        public string endingIdAlone = "A";

        [Tooltip("结局 B：带走记录")]
        public string endingIdRecord = "B";

        [Tooltip("结局 C：留下封门")]
        public string endingIdSeal = "C";

        [Header("联动")]
        [Tooltip("选择后解锁的门（出口舱门）")]
        public DoorInteractable exitDoor;

        [Header("提示")]
        public string promptIdle = "撤离协议 —— 做出你的选择";
        public string promptDone = "协议已确认";

        /// <summary>玩家选了什么（空 = 未选）。</summary>
        public string ChosenEnding { get; private set; } = "";

        /// <summary>是否已经做出选择。</summary>
        public bool HasChosen => !string.IsNullOrEmpty(ChosenEnding);

        /// <summary>选择事件（参数为结局 ID）。</summary>
        public static event Action<string> OnChosen;

        // ────────── UI 状态 ──────────

        private bool _panelOpen;
        private float _panelAlpha;
        private GUIStyle _titleStyle, _btnStyle, _footerStyle;

        private AbyssEcho.Player.PlayerMotor _motor;
        private AbyssEcho.Player.PlayerLook _look;
        private AbyssEcho.Interaction.InteractionDetector _detector;
        private AbyssEcho.Player.PlayerInputReader _input;

        protected override string DefaultPrompt =>
            HasChosen ? promptDone : promptIdle;

        protected override void Awake()
        {
            base.Awake();
            _motor = FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
            _look = FindFirstObjectByType<AbyssEcho.Player.PlayerLook>();
            _detector = FindFirstObjectByType<AbyssEcho.Interaction.InteractionDetector>();
            _input = FindFirstObjectByType<AbyssEcho.Player.PlayerInputReader>();
        }

        public override void OnInteract(GameObject interactor)
        {
            if (HasChosen) return;

            OpenPanel();
        }

        private void OpenPanel()
        {
            _panelOpen = true;
            Time.timeScale = 1f;   // 不用暂停，保留紧张感

            if (_motor != null) _motor.enabled = false;
            if (_look != null) _look.enabled = false;
            if (_detector != null) _detector.enabled = false;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            Debug.Log("[Evac] 撤离协议界面已打开");
        }

        private void ClosePanel()
        {
            _panelOpen = false;
            _panelAlpha = 0f;

            if (!HasChosen)
            {
                if (_motor != null) _motor.enabled = true;
                if (_look != null) _look.enabled = true;
                if (_detector != null) _detector.enabled = true;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                if (_input != null) _input.RefreshCursor();
            }
        }

        /// <summary>做出选择。</summary>
        public void Choose(string endingId)
        {
            if (HasChosen) return;

            ChosenEnding = endingId;
            _panelOpen = false;

            if (exitDoor != null) exitDoor.locked = false;

            if (AbyssEcho.Audio.AudioManager.Instance != null)
                AbyssEcho.Audio.AudioManager.Instance.PlayUiBlip();

            Debug.Log($"[Evac] 玩家选择结局：{endingId}");
            OnChosen?.Invoke(endingId);

            // 选完之后恢复玩家控制，让他自己走向舱门
            if (_motor != null) _motor.enabled = true;
            if (_look != null) _look.enabled = true;
            if (_detector != null) _detector.enabled = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            if (_input != null) _input.RefreshCursor();
        }

        private void Update()
        {
            if (_panelOpen)
            {
                _panelAlpha = Mathf.MoveTowards(_panelAlpha, 1f, Time.deltaTime * 4f);

                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    ClosePanel();
                }
            }
            else if (_panelAlpha > 0f)
            {
                _panelAlpha = Mathf.MoveTowards(_panelAlpha, 0f, Time.deltaTime * 6f);
            }
        }

        private void OnGUI()
        {
            if (_panelAlpha <= 0.01f) return;

            EnsureStyles();

            float a = _panelAlpha;

            // 遮罩
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.82f * a);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = old;

            float w = Mathf.Min(760f, Screen.width * 0.86f);
            float h = 460f;
            float x = (Screen.width - w) * 0.5f;
            float y = (Screen.height - h) * 0.5f;

            _titleStyle.normal.textColor = new Color(0.92f, 0.86f, 0.72f, a);
            GUI.Label(new Rect(x, y, w, 44f), "撤 离 协 议", _titleStyle);

            _footerStyle.normal.textColor = new Color(0.62f, 0.68f, 0.74f, a);
            GUI.Label(new Rect(x, y + 44f, w, 26f), "你只能带走一样东西，或者什么都不带。", _footerStyle);

            float by = y + 96f;
            float bh = 74f;
            float gap = 16f;

            if (Button(new Rect(x + 60f, by, w - 120f, bh), "只带自己走", a,
                       "你一个人出去。门在你身后关上。"))
            {
                Choose(endingIdAlone);
                return;
            }
            by += bh + gap;

            if (Button(new Rect(x + 60f, by, w - 120f, bh), "带走记录", a,
                       "你把数据和录音带上去了。世界会知道下面有什么。"))
            {
                Choose(endingIdRecord);
                return;
            }
            by += bh + gap;

            if (Button(new Rect(x + 60f, by, w - 120f, bh), "留下，关上它", a,
                       "你想起工牌上的日期是十九年前。你选择留下来。"))
            {
                Choose(endingIdSeal);
                return;
            }

            _footerStyle.normal.textColor = new Color(0.45f, 0.50f, 0.55f, a);
            GUI.Label(new Rect(x, y + h - 34f, w, 24f), "Esc 返回（不做选择）", _footerStyle);
        }

        private bool Button(Rect r, string title, float alpha, string subtitle)
        {
            var oldColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);

            bool clicked = GUI.Button(r, "");

            GUI.color = oldColor;

            _btnStyle.normal.textColor = new Color(0.92f, 0.92f, 0.95f, alpha);
            _btnStyle.hover.textColor = new Color(1f, 0.85f, 0.45f, alpha);
            GUI.Label(new Rect(r.x + 20f, r.y + 10f, r.width - 40f, 26f), title, _btnStyle);

            _footerStyle.normal.textColor = new Color(0.62f, 0.66f, 0.70f, alpha * 0.9f);
            GUI.Label(new Rect(r.x + 20f, r.y + 38f, r.width - 40f, 24f), subtitle, _footerStyle);

            return clicked;
        }

        private void EnsureStyles()
        {
            if (_titleStyle == null)
            {
                _titleStyle = AbyssEcho.UI.ChineseFont.CreateStyle(26, FontStyle.Bold);
                _titleStyle.alignment = TextAnchor.MiddleCenter;
                AbyssEcho.UI.ChineseFont.Apply(_titleStyle);
            }
            if (_btnStyle == null)
            {
                _btnStyle = AbyssEcho.UI.ChineseFont.CreateStyle(20, FontStyle.Bold);
                _btnStyle.alignment = TextAnchor.MiddleLeft;
                AbyssEcho.UI.ChineseFont.Apply(_btnStyle);
            }
            if (_footerStyle == null)
            {
                _footerStyle = AbyssEcho.UI.ChineseFont.CreateStyle(15, FontStyle.Normal);
                _footerStyle.alignment = TextAnchor.MiddleCenter;
                AbyssEcho.UI.ChineseFont.Apply(_footerStyle);
            }
        }

        /// <summary>重置（测试用）。</summary>
        public void ResetConsole()
        {
            ChosenEnding = "";
            _panelOpen = false;
            _panelAlpha = 0f;
        }
    }
}