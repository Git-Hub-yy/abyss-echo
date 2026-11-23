using System;
using AbyssEcho.Interaction;
using UnityEngine;

namespace AbyssEcho.Puzzle
{
    /// <summary>
    /// 声波频率终端（第二章 E3 控制室）。
    ///
    /// 谜题逻辑：
    ///   · 混响舱（E2）里会播放一段"回声"——一个特定频率
    ///   · 玩家需要把终端频率调到与回声一致
    ///   · 正确答案（220 Hz）藏在 D4 档案室的记录里
    ///   · 也在 E2 混响舱能"直接听"
    ///
    /// 交互方式：
    ///   · 上下方向键 / 鼠标滚轮：调节频率（每步 10 Hz）
    ///   · 回车 / 点击确认：提交
    ///   · 正确 → 门开 + 解锁；错误 → 蜂鸣 + 恐惧上升
    ///
    /// 为什么用频率而不是数字密码：
    ///   与"回声"主题直接呼应，比纯数字更有代入感；
    ///   而且频率可以"听"——部分玩家靠耳朵就能解，降低卡关风险。
    /// </summary>
    public class FrequencyConsole : InteractableBase
    {
        [Header("谜题参数")]
        [Tooltip("正确频率（Hz）")]
        public float correctFrequency = 220f;

        [Tooltip("最小频率")]
        public float minFrequency = 100f;

        [Tooltip("最大频率")]
        public float maxFrequency = 400f;

        [Tooltip("调节步长")]
        public float stepSize = 10f;

        [Tooltip("容差（±Hz 内算正确）")]
        public float tolerance = 5f;

        [Header("联动")]
        public DoorInteractable[] doorsToUnlock;
        public Renderer[] indicatorRenderers;
        public Color indicatorOffColor = new Color(0.9f, 0.25f, 0.15f);
        public Color indicatorOnColor = new Color(0.25f, 0.95f, 0.55f);

        [Header("提示")]
        public string promptIdle = "操作频率终端";

        /// <summary>当前设置的频率。</summary>
        public float CurrentFrequency { get; private set; }

        /// <summary>是否已解锁。</summary>
        public bool IsUnlocked { get; private set; }

        /// <summary>是否正在操作面板。</summary>
        public bool IsOpen { get; private set; }

        /// <summary>解锁事件。</summary>
        public static event Action OnFrequencyMatched;

        private AbyssEcho.Player.PlayerMotor _motor;
        private AbyssEcho.Player.PlayerLook _look;
        private AbyssEcho.Player.PlayerInputReader _input;
        private AbyssEcho.Interaction.InteractionDetector _detector;

        private GUIStyle _titleStyle, _freqStyle, _labelStyle, _hintStyle, _feedbackStyle;
        private Texture2D _white;
        private string _feedback = "";
        private float _feedbackUntil;
        private bool _feedbackOk;

        protected override string DefaultPrompt =>
            IsUnlocked ? "终端已解锁" : promptIdle;

        protected override void Awake()
        {
            base.Awake();
            _white = Texture2D.whiteTexture;
            CurrentFrequency = minFrequency;

            _motor = FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
            _look = FindFirstObjectByType<AbyssEcho.Player.PlayerLook>();
            _input = FindFirstObjectByType<AbyssEcho.Player.PlayerInputReader>();
            _detector = FindFirstObjectByType<AbyssEcho.Interaction.InteractionDetector>();
        }

        public override void OnInteract(GameObject interactor)
        {
            if (IsUnlocked || IsOpen) return;
            Open();
        }

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;

            if (_motor != null) _motor.enabled = false;
            if (_look != null) _look.enabled = false;
            if (_detector != null) _detector.enabled = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;

            if (_motor != null) _motor.enabled = true;
            if (_look != null) _look.enabled = true;
            if (_detector != null) _detector.enabled = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (_input != null) _input.RefreshCursor();
        }

        /// <summary>提交当前频率（供 UI 与测试调用）。</summary>
        public bool Submit(float freq)
        {
            CurrentFrequency = Mathf.Clamp(freq, minFrequency, maxFrequency);

            bool ok = Mathf.Abs(CurrentFrequency - correctFrequency) <= tolerance;

            if (ok)
            {
                IsUnlocked = true;
                _feedback = "频率匹配 —— 门锁解除";
                _feedbackOk = true;
                _feedbackUntil = Time.time + 3f;

                UpdateIndicator(true);

                if (doorsToUnlock != null)
                    foreach (var d in doorsToUnlock)
                        if (d != null) d.locked = false;

                if (AbyssEcho.Audio.AudioManager.Instance != null)
                {
                    AbyssEcho.Audio.AudioManager.Instance.PlayUiBlip();
                    AbyssEcho.Audio.AudioManager.Instance.PlayStinger(0.35f);
                }

                // 解谜成功 → 恐惧下降（松了一口气）
                if (AbyssEcho.Horror.FearSystem.Instance != null)
                    AbyssEcho.Horror.FearSystem.Instance.AddFear(-0.15f);

                OnFrequencyMatched?.Invoke();
                Debug.Log($"[FrequencyConsole] 频率匹配成功：{CurrentFrequency} Hz");
            }
            else
            {
                _feedback = $"{(CurrentFrequency < correctFrequency ? "偏低" : "偏高")} —— 不是这个声音";
                _feedbackOk = false;
                _feedbackUntil = Time.time + 2f;

                if (AbyssEcho.Audio.AudioManager.Instance != null)
                    AbyssEcho.Audio.AudioManager.Instance.PlayError();

                if (AbyssEcho.Horror.FearSystem.Instance != null)
                    AbyssEcho.Horror.FearSystem.Instance.AddFear(0.05f);

                Debug.Log($"[FrequencyConsole] 频率错误：{CurrentFrequency} Hz（正确 {correctFrequency}）");
            }

            return ok;
        }

        private void UpdateIndicator(bool on)
        {
            if (indicatorRenderers == null) return;
            var c = on ? indicatorOnColor : indicatorOffColor;
            foreach (var r in indicatorRenderers)
            {
                if (r == null) continue;
                var mat = r.material;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
                mat.color = c;
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", c * 1.8f);
                }
            }
        }

        private void Update()
        {
            if (!IsOpen) return;

            // 关闭
            if (Input.GetKeyDown(KeyCode.Escape)) { Close(); return; }

            // 调节
            float dir = 0f;
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) dir = 1f;
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) dir = -1f;

            if (dir != 0f)
            {
                CurrentFrequency = Mathf.Clamp(CurrentFrequency + dir * stepSize, minFrequency, maxFrequency);
            }

            // 提交
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                Submit(CurrentFrequency);
            }
        }

        private void OnDisable()
        {
            if (IsOpen) Close();
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

            float w = 560f, h = 360f;
            var panel = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

            GUI.color = new Color(0.05f, 0.07f, 0.09f, 0.96f);
            GUI.DrawTexture(panel, _white);

            GUI.color = new Color(0.35f, 0.75f, 0.95f, 0.85f);
            const float t = 2f;
            GUI.DrawTexture(new Rect(panel.x, panel.y, panel.width, t), _white);
            GUI.DrawTexture(new Rect(panel.x, panel.yMax - t, panel.width, t), _white);
            GUI.DrawTexture(new Rect(panel.x, panel.y, t, panel.height), _white);
            GUI.DrawTexture(new Rect(panel.xMax - t, panel.y, t, panel.height), _white);
            GUI.color = prev;

            float cy = panel.y + 40f;

            _titleStyle.normal.textColor = new Color(0.70f, 0.88f, 1f);
            GUI.Label(new Rect(panel.x, cy, panel.width, 30f), "声波频率终端", _titleStyle);

            // 大号频率数值
            cy += 64f;
            _freqStyle.normal.textColor = IsUnlocked
                ? new Color(0.3f, 1f, 0.6f)
                : new Color(1f, 0.75f, 0.35f);
            GUI.Label(new Rect(panel.x, cy, panel.width, 70f), $"{CurrentFrequency:F0} Hz", _freqStyle);

            // 频率条
            cy += 76f;
            float barW = panel.width - 100f;
            var bar = new Rect(panel.x + 50f, cy, barW, 14f);
            GUI.color = new Color(0.12f, 0.15f, 0.19f);
            GUI.DrawTexture(bar, _white);

            float k = Mathf.InverseLerp(minFrequency, maxFrequency, CurrentFrequency);
            GUI.color = IsUnlocked ? new Color(0.3f, 1f, 0.6f) : new Color(0.35f, 0.75f, 0.95f);
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * k, bar.height), _white);
            GUI.color = prev;

            _labelStyle.normal.textColor = new Color(0.55f, 0.62f, 0.70f);
            GUI.Label(new Rect(bar.x, bar.yMax + 4f, 100f, 20f), $"{minFrequency:F0}", _labelStyle);
            GUI.Label(new Rect(bar.xMax - 100f, bar.yMax + 4f, 100f, 20f), $"{maxFrequency:F0}", _labelStyle);

            // 反馈
            cy += 70f;
            if (Time.time < _feedbackUntil)
            {
                _feedbackStyle.normal.textColor = _feedbackOk
                    ? new Color(0.3f, 1f, 0.6f)
                    : new Color(1f, 0.45f, 0.35f);
                GUI.Label(new Rect(panel.x, cy, panel.width, 26f), _feedback, _feedbackStyle);
            }

            // 操作提示
            _hintStyle.normal.textColor = new Color(0.45f, 0.52f, 0.60f);
            GUI.Label(new Rect(panel.x, panel.yMax - 46f, panel.width, 24f),
                      "↑↓ / W S 调节　·　回车提交　·　Esc 退出", _hintStyle);
        }

        private void EnsureStyles()
        {
            if (_titleStyle == null)
            {
                _titleStyle = AbyssEcho.UI.ChineseFont.CreateStyle(22, FontStyle.Bold);
                _titleStyle.alignment = TextAnchor.MiddleCenter;
                AbyssEcho.UI.ChineseFont.Apply(_titleStyle);
            }
            if (_freqStyle == null)
            {
                _freqStyle = AbyssEcho.UI.ChineseFont.CreateStyle(50, FontStyle.Bold);
                _freqStyle.alignment = TextAnchor.MiddleCenter;
                AbyssEcho.UI.ChineseFont.Apply(_freqStyle);
            }
            if (_labelStyle == null)
            {
                _labelStyle = AbyssEcho.UI.ChineseFont.CreateStyle(13);
                _labelStyle.alignment = TextAnchor.MiddleCenter;
                AbyssEcho.UI.ChineseFont.Apply(_labelStyle);
            }
            if (_hintStyle == null)
            {
                _hintStyle = AbyssEcho.UI.ChineseFont.CreateStyle(14);
                _hintStyle.alignment = TextAnchor.MiddleCenter;
                AbyssEcho.UI.ChineseFont.Apply(_hintStyle);
            }
            if (_feedbackStyle == null)
            {
                _feedbackStyle = AbyssEcho.UI.ChineseFont.CreateStyle(16, FontStyle.Bold);
                _feedbackStyle.alignment = TextAnchor.MiddleCenter;
                AbyssEcho.UI.ChineseFont.Apply(_feedbackStyle);
            }
        }
    }
}