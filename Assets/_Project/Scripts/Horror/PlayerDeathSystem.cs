using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AbyssEcho.Horror
{
    /// <summary>
    /// 死亡与重开系统：处理"被怪物抓住"的完整流程。
    ///
    /// 流程：
    ///   1. 怪物抓住玩家 → 调用 Kill()
    ///   2. 冻结玩家输入，屏幕快速变黑（0.8 秒）
    ///   3. 黑屏后显示死亡界面：标题 + 提示 +「重新开始」按钮
    ///   4. 玩家点击 → 重载当前场景（场景会自动重建到初始状态）
    ///   5. 按 R 键也可以直接重开
    ///
    /// 设计取舍：
    ///   · 不用 Time.timeScale = 0 暂停，而是冻结玩家组件的 enabled
    ///     （暂停时间会破坏正在播放的死亡音效与渐黑淡入）
    ///   · 死亡界面支持鼠标点击，也支持键盘 R
    ///   · 重开用 SceneManager.LoadScene 重载，保证所有状态干净复位
    /// </summary>
    public class PlayerDeathSystem : MonoBehaviour
    {
        public static PlayerDeathSystem Instance { get; private set; }

        [Header("死亡表现")]
        [Tooltip("黑屏淡入时长（秒）")]
        public float fadeDuration = 0.8f;

        [Tooltip("死亡标题")]
        public string deathTitle = "你被发现了";

        [Tooltip("副标题")]
        public string deathSubtitle = "深渊记得每一个来客。";

        [Tooltip("重开提示")]
        public string restartHint = "点击「重新开始」或按 R 键";

        [Header("样式")]
        public Color fadeColor = Color.black;
        public Color titleColor = new Color(0.85f, 0.25f, 0.20f);
        public Color textColor = new Color(0.80f, 0.84f, 0.88f);
        public int titleFontSize = 42;
        public int textFontSize = 18;

        /// <summary>是否已死亡。</summary>
        public bool IsDead { get; private set; }

        /// <summary>死亡事件（供存档/统计订阅）。</summary>
        public static event Action OnPlayerDied;

        /// <summary>重开事件。</summary>
        public static event Action OnPlayerRestart;

        private float _fadeAlpha;
        private bool _fadeComplete;

        private AbyssEcho.Player.PlayerInputReader _input;
        private AbyssEcho.Player.PlayerMotor _motor;
        private AbyssEcho.Player.PlayerLook _look;
        private AbyssEcho.Interaction.InteractionDetector _detector;
        private AbyssEcho.UI.CrosshairUI _crosshair;
        private AbyssEcho.Horror.FearVisuals _fearVisuals;

        // UI 样式
        private GUIStyle _titleStyle, _textStyle, _hintStyle;
        private Texture2D _whiteTex;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _input = FindFirstObjectByType<AbyssEcho.Player.PlayerInputReader>();
            _motor = FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
            _look = FindFirstObjectByType<AbyssEcho.Player.PlayerLook>();
            _detector = FindFirstObjectByType<AbyssEcho.Interaction.InteractionDetector>();
            _crosshair = FindFirstObjectByType<AbyssEcho.UI.CrosshairUI>();
            _fearVisuals = FindFirstObjectByType<AbyssEcho.Horror.FearVisuals>();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_whiteTex != null) Destroy(_whiteTex);
        }

        private void Update()
        {
            if (!IsDead) return;

            // 淡入
            if (!_fadeComplete)
            {
                _fadeAlpha = Mathf.MoveTowards(_fadeAlpha, 1f, Time.unscaledDeltaTime / fadeDuration);
                if (_fadeAlpha >= 1f) _fadeComplete = true;
            }

            // 重开
            if (_fadeComplete && Input.GetKeyDown(KeyCode.R))
                Restart();
        }

        // ────────────────────────── 公开接口 ──────────────────────────

        /// <summary>杀死玩家（由怪物调用）。</summary>
        public void Kill(string reason = "")
        {
            if (IsDead) return;
            IsDead = true;
            _fadeAlpha = 0f;
            _fadeComplete = false;

            FreezePlayer();

            if (!string.IsNullOrEmpty(reason))
                Debug.Log($"[Death] {reason}");

            Debug.Log("[Death] 玩家死亡");

            // 音效
            if (AbyssEcho.Audio.AudioManager.Instance != null)
                AbyssEcho.Audio.AudioManager.Instance.PlayStinger(0.9f);

            // 恐惧拉满
            if (FearSystem.Instance != null)
                FearSystem.Instance.SetFear(1f);

            OnPlayerDied?.Invoke();
        }

        /// <summary>重开当前场景。</summary>
        public void Restart()
        {
            Debug.Log("[Death] 重新开始");

            // 恢复鼠标与输入状态，避免重载后残留
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            OnPlayerRestart?.Invoke();

            var scene = SceneManager.GetActiveScene();
            if (scene.buildIndex >= 0)
                SceneManager.LoadScene(scene.buildIndex);
            else
                SceneManager.LoadScene(scene.name);
        }

        // ────────────────────────── 内部 ──────────────────────────

        private void FreezePlayer()
        {
            if (_motor != null) _motor.enabled = false;
            if (_look != null) _look.enabled = false;
            if (_detector != null) _detector.enabled = false;
            if (_crosshair != null) _crosshair.enabled = false;
            if (_fearVisuals != null) _fearVisuals.enabled = false;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // ────────────────────────── 绘制 ──────────────────────────

        private void OnGUI()
        {
            if (!IsDead) return;

            EnsureStyles();

            // 黑屏
            if (_fadeAlpha > 0.001f)
            {
                var prev = GUI.color;
                GUI.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, _fadeAlpha);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = prev;
            }

            if (!_fadeComplete) return;

            // 死亡界面
            float cy = Screen.height * 0.5f;

            _titleStyle.normal.textColor = titleColor;
            GUI.Label(new Rect(0f, cy - 90f, Screen.width, 60f), deathTitle, _titleStyle);

            _textStyle.normal.textColor = textColor;
            GUI.Label(new Rect(0f, cy - 24f, Screen.width, 34f), deathSubtitle, _textStyle);

            // 重新开始按钮
            float bw = 240f, bh = 54f;
            var btn = new Rect((Screen.width - bw) * 0.5f, cy + 40f, bw, bh);
            bool hover = btn.Contains(Event.current.mousePosition);

            var prevColor = GUI.color;
            GUI.color = hover ? new Color(0.55f, 0.20f, 0.18f, 0.95f)
                              : new Color(0.32f, 0.12f, 0.11f, 0.95f);
            GUI.DrawTexture(btn, Texture2D.whiteTexture);

            GUI.color = new Color(0.80f, 0.55f, 0.52f, 0.9f);
            const float t = 2f;
            GUI.DrawTexture(new Rect(btn.x, btn.y, btn.width, t), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(btn.x, btn.yMax - t, btn.width, t), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(btn.x, btn.y, t, btn.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(btn.xMax - t, btn.y, t, btn.height), Texture2D.whiteTexture);
            GUI.color = prevColor;

            GUI.Label(btn, "重新开始", _titleStyle);
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && hover)
            {
                Restart();
                Event.current.Use();
            }

            // 提示
            _hintStyle.normal.textColor = new Color(0.55f, 0.60f, 0.66f);
            GUI.Label(new Rect(0f, btn.yMax + 18f, Screen.width, 26f), restartHint, _hintStyle);
        }

        private void EnsureStyles()
        {
            if (_titleStyle == null)
            {
                _titleStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = titleFontSize,
                    fontStyle = FontStyle.Bold
                };
                _titleStyle.normal.textColor = titleColor;
            }

            if (_textStyle == null)
            {
                _textStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = textFontSize
                };
                _textStyle.normal.textColor = textColor;
            }

            if (_hintStyle == null)
            {
                _hintStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 15
                };
            }
            AbyssEcho.UI.ChineseFont.Apply(_titleStyle);
            AbyssEcho.UI.ChineseFont.Apply(_textStyle);
            AbyssEcho.UI.ChineseFont.Apply(_hintStyle);
        }
    }
}
