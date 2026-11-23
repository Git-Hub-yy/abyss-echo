using AbyssEcho.Interaction;
using AbyssEcho.Player;
using AbyssEcho.Puzzle;
using UnityEngine;

namespace AbyssEcho.UI
{
    /// <summary>
    /// 门禁数字键盘 UI（纯 OnGUI 绘制，3×4 网格布局）。
    ///
    /// 操作：
    ///   · 鼠标点击数字 或 直接按键盘 0-9 输入
    ///   · Backspace 删除一位，Enter 提交，Esc 关闭
    ///   · 打开时释放鼠标锁定，关闭时恢复
    ///
    /// 为什么同时支持鼠标与键盘：
    ///   只支持鼠标时玩家要精准移动到小按钮上，手感差；
    ///   只支持键盘时新手可能不知道该按什么。两者都支持最稳。
    /// </summary>
    public class KeypadUI : MonoBehaviour
    {
        [Header("样式")]
        public float panelWidth = 300f;
        public float panelHeight = 420f;
        public int digitFontSize = 26;
        public int inputFontSize = 34;
        public Color panelColor = new Color(0.05f, 0.07f, 0.09f, 0.96f);
        public Color borderColor = new Color(0.35f, 0.62f, 0.85f, 0.9f);
        public Color keyColor = new Color(0.14f, 0.18f, 0.23f, 1f);
        public Color keyHoverColor = new Color(0.24f, 0.34f, 0.45f, 1f);
        public Color textColor = new Color(0.85f, 0.93f, 1f);
        public Color errorColor = new Color(0.95f, 0.35f, 0.28f);
        public Color successColor = new Color(0.3f, 1f, 0.55f);

        private KeypadPanel _panel;
        private string _input = "";
        private string _feedback = "";
        private Color _feedbackColor;
        private float _feedbackUntil;

        private PlayerInputReader _inputReader;
        private PlayerMotor _motor;
        private PlayerLook _look;
        private InteractionDetector _detector;

        private GUIStyle _digitStyle, _inputStyle, _feedbackStyle, _labelStyle;

        public bool IsOpen => _panel != null;

        private void Awake()
        {
            _inputReader = FindFirstObjectByType<PlayerInputReader>();
            _motor = FindFirstObjectByType<PlayerMotor>();
            _look = FindFirstObjectByType<PlayerLook>();
            _detector = FindFirstObjectByType<InteractionDetector>();
        }

        public void Open(KeypadPanel panel)
        {
            _panel = panel;
            _input = "";
            _feedback = "";
            ApplyInputLock(true);
        }

        public void Close()
        {
            _panel = null;
            _input = "";
            _feedback = "";
            ApplyInputLock(false);
        }

        private void ApplyInputLock(bool open)
        {
            if (_motor != null) _motor.enabled = !open;
            if (_look != null) _look.enabled = !open;
            if (_detector != null) _detector.enabled = !open;

            Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = open;

            if (_inputReader != null) _inputReader.RefreshCursor();
        }

        private void Update()
        {
            if (!IsOpen) return;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Backspace) && _input.Length > 0)
            {
                _input = _input.Substring(0, _input.Length - 1);
                return;
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                Submit();
                return;
            }

            // 数字键
            for (int i = 0; i <= 9; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha0 + i) || Input.GetKeyDown(KeyCode.Keypad0 + i))
                {
                    AppendDigit(i.ToString());
                    return;
                }
            }
        }

        private void AppendDigit(string d)
        {
            if (_panel == null) return;
            if (_input.Length >= _panel.maxDigits) return;

            _input += d;

            // 输满自动提交，减少一次操作
            if (_input.Length >= _panel.maxDigits) Submit();
        }

        private void Submit()
        {
            if (_panel == null) return;

            bool ok = _panel.Submit(_input);

            if (ok)
            {
                _feedback = _panel.successMessage;
                _feedbackColor = successColor;
                _feedbackUntil = Time.time + 1.5f;
                _input = "";
                // 成功后延迟关闭，让玩家看到反馈
                Invoke(nameof(Close), 0.9f);
            }
            else
            {
                _feedback = _panel.failMessage;
                _feedbackColor = errorColor;
                _feedbackUntil = Time.time + 1.5f;
                _input = "";
            }
        }

        // ────────────────────────── 绘制 ──────────────────────────

        private void OnGUI()
        {
            if (!IsOpen) return;

            EnsureStyles();

            // 遮罩
            var prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.5f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = prev;

            float w = panelWidth;
            float h = panelHeight;
            var panel = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

            // 面板背景
            GUI.color = panelColor;
            GUI.DrawTexture(panel, Texture2D.whiteTexture);

            // 边框
            GUI.color = borderColor;
            const float t = 2f;
            GUI.DrawTexture(new Rect(panel.x, panel.y, panel.width, t), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(panel.x, panel.yMax - t, panel.width, t), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(panel.x, panel.y, t, panel.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(panel.xMax - t, panel.y, t, panel.height), Texture2D.whiteTexture);
            GUI.color = prev;

            // 标题
            GUI.Label(new Rect(panel.x, panel.y + 14, panel.width, 26), "门禁面板", _labelStyle);

            // 输入显示框
            var inputRect = new Rect(panel.x + 28, panel.y + 48, panel.width - 56, 52);
            GUI.color = new Color(0.02f, 0.03f, 0.04f, 1f);
            GUI.DrawTexture(inputRect, Texture2D.whiteTexture);
            GUI.color = prev;

            string shown = _input;
            while (shown.Length < _panel.maxDigits) shown += "_";
            var display = string.Join(" ", shown.ToCharArray());

            _inputStyle.normal.textColor = textColor;
            GUI.Label(inputRect, display, _inputStyle);

            // 反馈文字
            if (Time.time < _feedbackUntil && !string.IsNullOrEmpty(_feedback))
            {
                _feedbackStyle.normal.textColor = _feedbackColor;
                GUI.Label(new Rect(panel.x, inputRect.yMax + 6, panel.width, 24), _feedback, _feedbackStyle);
            }

            // 数字键盘 3×4
            float padTop = inputRect.yMax + 36;
            float padW = panel.width - 56;
            float btnW = (padW - 2 * 10) / 3f;
            float btnH = 56f;
            float gap = 10f;

            for (int i = 0; i < 9; i++)
            {
                int row = i / 3, col = i % 3;
                var r = new Rect(panel.x + 28 + col * (btnW + gap), padTop + row * (btnH + gap), btnW, btnH);
                DrawKey(r, (i + 1).ToString(), () => AppendDigit((i + 1).ToString()));
            }

            // 底排：删除 / 0 / 确认
            float bottomY = padTop + 3 * (btnH + gap);
            var delRect = new Rect(panel.x + 28, bottomY, btnW, btnH);
            var zeroRect = new Rect(panel.x + 28 + btnW + gap, bottomY, btnW, btnH);
            var okRect = new Rect(panel.x + 28 + 2 * (btnW + gap), bottomY, btnW, btnH);

            DrawKey(delRect, "←", () =>
            {
                if (_input.Length > 0) _input = _input.Substring(0, _input.Length - 1);
            });
            DrawKey(zeroRect, "0", () => AppendDigit("0"));
            DrawKey(okRect, "确认", Submit);

            // 底部提示
            GUI.Label(new Rect(panel.x, bottomY + btnH + 10, panel.width, 22),
                      "键盘 0-9 可直接输入 · Esc 关闭", _labelStyle);
        }

        private void DrawKey(Rect r, string label, System.Action onClick)
        {
            bool hover = r.Contains(Event.current.mousePosition);

            var prev = GUI.color;
            GUI.color = hover ? keyHoverColor : keyColor;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = prev;

            var style = _digitStyle;
            style.normal.textColor = textColor;
            GUI.Label(r, label, style);

            if (hover && Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                onClick?.Invoke();
                Event.current.Use();
            }
        }

        private void EnsureStyles()
        {
            if (_digitStyle == null)
            {
                _digitStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = digitFontSize,
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold
                };
            }
            if (_inputStyle == null)
            {
                _inputStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = inputFontSize,
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold
                };
            }
            if (_feedbackStyle == null)
            {
                _feedbackStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 16,
                    alignment = TextAnchor.UpperCenter
                };
            }
            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 15,
                    alignment = TextAnchor.UpperCenter
                };
                _labelStyle.normal.textColor = new Color(0.62f, 0.74f, 0.86f);
            }
            ChineseFont.Apply(_digitStyle);
            ChineseFont.Apply(_inputStyle);
            ChineseFont.Apply(_feedbackStyle);
            ChineseFont.Apply(_labelStyle);
        }
    }
}
