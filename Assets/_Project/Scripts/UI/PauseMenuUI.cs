using AbyssEcho.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AbyssEcho.UI
{
    /// <summary>
    /// 暂停菜单：按 Esc 打开，包含继续 / 设置 / 重新开始 / 退出。
    ///
    /// 关键设计：
    ///   · **不暂停时间**（Time.timeScale 保持 1）
    ///     原因：本项目的世界事件（灯光闪烁协程、怪物寻路）都依赖时间流动，
    ///     暂停会让怪物卡在半路、灯光停止闪，恢复时出现不自然的跳变。
    ///     这里改为"冻结玩家输入"，世界继续运转——这是很多恐怖游戏的选择
    ///     （玩家暂停时并不是绝对安全）。
    ///   · 打开时释放鼠标，方便点击
    ///   · Esc 在游戏中打开菜单，在菜单中关闭
    /// </summary>
    public class PauseMenuUI : MonoBehaviour
    {
        [Header("样式")]
        public float panelWidth = 460f;
        public float panelHeight = 420f;
        public int titleFontSize = 32;
        public int buttonFontSize = 19;

        public Color panelColor = new Color(0.03f, 0.05f, 0.07f, 0.96f);
        public Color borderColor = new Color(0.35f, 0.62f, 0.85f, 0.85f);
        public Color titleColor = new Color(0.85f, 0.94f, 1f);
        public Color buttonColor = new Color(0.13f, 0.17f, 0.22f, 1f);
        public Color buttonHoverColor = new Color(0.22f, 0.32f, 0.42f, 1f);
        public Color textColor = new Color(0.88f, 0.92f, 0.96f);
        public Color dangerColor = new Color(0.95f, 0.45f, 0.40f);

        public bool IsOpen { get; private set; }

        // 设置页状态
        private bool _settingsOpen;
        private float _sensitivity;
        private bool _invertY;
        private float _masterVolume;

        private PlayerInputReader _input;
        private PlayerMotor _motor;
        private PlayerLook _look;
        private AbyssEcho.Interaction.InteractionDetector _detector;

        private GUIStyle _titleStyle, _btnStyle, _labelStyle, _sliderLabelStyle;
        private Texture2D _white;

        private void Awake()
        {
            _input = FindFirstObjectByType<PlayerInputReader>();
            _motor = FindFirstObjectByType<PlayerMotor>();
            _look = FindFirstObjectByType<PlayerLook>();
            _detector = FindFirstObjectByType<AbyssEcho.Interaction.InteractionDetector>();
            _white = Texture2D.whiteTexture;

            LoadSettings();
        }

        private void LoadSettings()
        {
            _sensitivity = PlayerPrefs.GetFloat("settings.mouseSensitivity", 2.0f);
            _invertY = PlayerPrefs.GetInt("settings.invertY", 0) == 1;
            _masterVolume = PlayerPrefs.GetFloat("settings.masterVolume", 0.8f);
        }

        private void SaveSettings()
        {
            PlayerPrefs.SetFloat("settings.mouseSensitivity", _sensitivity);
            PlayerPrefs.SetInt("settings.invertY", _invertY ? 1 : 0);
            PlayerPrefs.SetFloat("settings.masterVolume", _masterVolume);
            PlayerPrefs.Save();

            if (_look != null)
            {
                _look.MouseSensitivity = _sensitivity;
                _look.InvertY = _invertY;
            }

            if (AbyssEcho.Audio.AudioManager.Instance != null)
                AbyssEcho.Audio.AudioManager.Instance.masterVolume = _masterVolume;

            Debug.Log($"[PauseMenu] 设置已保存：灵敏度={_sensitivity:F2} 反转Y={_invertY} 音量={_masterVolume:F2}");
        }

        private void Update()
        {
            // Esc 开关（其他面板打开时不抢输入）
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (IsOpen) Close();
                else if (!IsOtherPanelOpen()) Open();
            }
        }

        private bool IsOtherPanelOpen()
        {
            var inv = FindFirstObjectByType<InventoryUI>();
            if (inv != null && inv.IsOpen) return true;

            var narrative = FindFirstObjectByType<NarrativeUI>();
            if (narrative != null && narrative.IsOpen) return true;

            var keypad = FindFirstObjectByType<KeypadUI>();
            if (keypad != null && keypad.IsOpen) return true;

            return false;
        }

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            _settingsOpen = false;
            ApplyInputLock(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            _settingsOpen = false;
            ApplyInputLock(false);
        }

        private void ApplyInputLock(bool locked)
        {
            if (_motor != null) _motor.enabled = !locked;
            if (_look != null) _look.enabled = !locked;
            if (_detector != null) _detector.enabled = !locked;

            if (_input != null) _input.RefreshCursor();

            Cursor.lockState = locked ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = locked;
        }

        // ────────────────────────── 绘制 ──────────────────────────

        private void OnGUI()
        {
            if (!IsOpen) return;
            EnsureStyles();

            var prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _white);
            GUI.color = prev;

            float w = Mathf.Min(panelWidth, Screen.width - 80);
            float h = Mathf.Min(panelHeight, Screen.height - 80);
            var panel = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

            DrawPanelBackground(panel);

            var inner = new Rect(panel.x + 30, panel.y + 24, panel.width - 60, panel.height - 48);

            _titleStyle.normal.textColor = titleColor;
            GUI.Label(new Rect(inner.x, inner.y, inner.width, 44),
                      _settingsOpen ? "设置" : "已暂停", _titleStyle);

            if (_settingsOpen) DrawSettings(inner);
            else DrawMainMenu(inner);
        }

        private void DrawMainMenu(Rect inner)
        {
            float y = inner.y + 70;
            float bh = 50f, gap = 12f;

            if (Button(new Rect(inner.x, y, inner.width, bh), "继续游戏")) Close();
            y += bh + gap;

            if (Button(new Rect(inner.x, y, inner.width, bh), "设置")) { _settingsOpen = true; }
            y += bh + gap;

            if (Button(new Rect(inner.x, y, inner.width, bh), "重新开始本章", dangerColor))
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                var sc = SceneManager.GetActiveScene();
                if (sc.buildIndex >= 0) SceneManager.LoadScene(sc.buildIndex);
                else SceneManager.LoadScene(sc.name);
            }
            y += bh + gap;

            // S29 新增：返回主菜单
            //
            // 以前的暂停菜单只有"重新开始本章"和"退出游戏"，
            // 玩家想换章节必须整个关掉游戏重开。
            if (Button(new Rect(inner.x, y, inner.width, bh), "返回主菜单"))
            {
                GoToMainMenu();
            }
            y += bh + gap;

            if (Button(new Rect(inner.x, y, inner.width, bh), "退出游戏", dangerColor))
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }

            _labelStyle.normal.textColor = new Color(0.55f, 0.62f, 0.70f);
            GUI.Label(new Rect(inner.x, inner.yMax - 24, inner.width, 22), "Esc 关闭菜单", _labelStyle);
        }

        /// <summary>
        /// 返回主菜单。
        /// 先恢复输入状态（否则光标还是锁定的，主菜单点不了），再加载主菜单场景。
        /// </summary>
        private void GoToMainMenu()
        {
            // 恢复时间缩放（暂停时被设为 0）
            Time.timeScale = 1f;

            // 恢复输入
            if (_input != null) _input.InputEnabled = true;

            // 恢复光标
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            SceneManager.LoadScene("MainMenu");
        }

        private void DrawSettings(Rect inner)
        {
            float y = inner.y + 66;

            // 鼠标灵敏度
            GUI.Label(new Rect(inner.x, y, inner.width, 26), "鼠标灵敏度", _sliderLabelStyle);
            _sensitivity = GUI.HorizontalSlider(new Rect(inner.x, y + 28, inner.width, 20),
                                               _sensitivity, 0.5f, 6f);
            GUI.Label(new Rect(inner.x, y + 50, inner.width, 22), _sensitivity.ToString("F2"), _labelStyle);
            y += 88;

            // 音量
            GUI.Label(new Rect(inner.x, y, inner.width, 26), "主音量", _sliderLabelStyle);
            _masterVolume = GUI.HorizontalSlider(new Rect(inner.x, y + 28, inner.width, 20),
                                                 _masterVolume, 0f, 1f);
            GUI.Label(new Rect(inner.x, y + 50, inner.width, 22),
                      Mathf.RoundToInt(_masterVolume * 100) + "%", _labelStyle);
            y += 88;

            // 反转 Y 轴
            bool newInvert = GUI.Toggle(new Rect(inner.x, y, inner.width, 26), _invertY, " 反转 Y 轴（上下颠倒）", _sliderLabelStyle);
            if (newInvert != _invertY)
            {
                _invertY = newInvert;
                SaveSettings();
            }
            y += 44;

            // 保存 / 返回
            float bh = 46f;
            if (Button(new Rect(inner.x, inner.yMax - bh - 6, inner.width * 0.48f, bh), "保存并返回"))
            {
                SaveSettings();
                _settingsOpen = false;
            }
            if (Button(new Rect(inner.x + inner.width * 0.52f, inner.yMax - bh - 6, inner.width * 0.48f, bh), "返回"))
            {
                LoadSettings();   // 放弃未保存的修改
                _settingsOpen = false;
            }

            // 滑动后实时应用（音量即时反馈）
            if (AbyssEcho.Audio.AudioManager.Instance != null)
                AbyssEcho.Audio.AudioManager.Instance.masterVolume = _masterVolume;
        }

        private bool Button(Rect r, string text, Color? overrideTextColor = null)
        {
            bool hover = r.Contains(Event.current.mousePosition);

            var prev = GUI.color;
            GUI.color = hover ? buttonHoverColor : buttonColor;
            GUI.DrawTexture(r, _white);
            GUI.color = prev;

            _btnStyle.normal.textColor = overrideTextColor ?? textColor;
            GUI.Label(r, text, _btnStyle);

            if (hover && Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                Event.current.Use();
                return true;
            }
            return false;
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
                _titleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = titleFontSize,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                _titleStyle.normal.textColor = titleColor;
            }
            if (_btnStyle == null)
            {
                _btnStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = buttonFontSize,
                    alignment = TextAnchor.MiddleCenter
                };
                _btnStyle.normal.textColor = textColor;
            }
            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    alignment = TextAnchor.MiddleCenter
                };
            }
            if (_sliderLabelStyle == null)
            {
                _sliderLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 16,
                    alignment = TextAnchor.MiddleLeft
                };
                _sliderLabelStyle.normal.textColor = textColor;
            }
            ChineseFont.Apply(_titleStyle);
            ChineseFont.Apply(_btnStyle);
            ChineseFont.Apply(_labelStyle);
            ChineseFont.Apply(_sliderLabelStyle);
        }
    }
}
