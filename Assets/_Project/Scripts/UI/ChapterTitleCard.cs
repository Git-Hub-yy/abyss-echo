using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AbyssEcho.UI
{
    /// <summary>
    /// 开场章节标题卡：进入场景后先黑屏显示「第一章 · 深潜」，
    /// 然后淡出，玩家开始探索。
    ///
    /// 为什么需要：
    ///   现在一进游戏就直接站在 A0 深潜舱里，没有任何"开始"的感觉。
    ///   标题卡是叙事游戏建立仪式感最廉价有效的手段。
    ///
    /// 时序：
    ///   黑屏 1.2 秒 → 标题淡入 0.8 秒 → 停留 1.8 秒 → 整体淡出 1.2 秒 → 开始游戏
    ///   总时长约 5 秒，可按 Esc 跳过。
    ///
    /// 注意：只在**首次进入**时播放（用 PlayerPrefs 标记），
    ///       重开/读档时不再重复播放，避免打断节奏。
    /// </summary>
    public class ChapterTitleCard : MonoBehaviour
    {
        [Header("文本")]
        public string chapterNumber = "第一章";
        public string chapterName = "深潜";
        public string subtitle = "ABYSS ECHO";

        [Header("时序（秒）")]
        public float blackHold = 1.2f;
        public float titleFadeIn = 0.8f;
        public float titleHold = 1.8f;
        public float fadeOut = 1.2f;

        [Header("样式")]
        public int numberFontSize = 22;
        public int nameFontSize = 46;
        public int subtitleFontSize = 15;
        public Color numberColor = new Color(0.55f, 0.70f, 0.85f);
        public Color nameColor = new Color(0.90f, 0.94f, 0.98f);
        public Color subtitleColor = new Color(0.45f, 0.52f, 0.60f);

        [Header("行为")]
        [Tooltip("是否只在第一次进入时播放")]
        public bool onlyFirstTime = true;

        [Tooltip("允许按 Esc 跳过")]
        public bool allowSkip = true;

        private const string SeenKey = "intro.chapter1.seen";

        private bool _playing;
        private float _totalTime;
        private float _elapsed;

        private GUIStyle _numStyle, _nameStyle, _subStyle;
        private Texture2D _white;

        private AbyssEcho.Player.PlayerMotor _motor;
        private AbyssEcho.Player.PlayerLook _look;

        private void Awake()
        {
            _white = Texture2D.whiteTexture;
            _motor = FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
            _look = FindFirstObjectByType<AbyssEcho.Player.PlayerLook>();

            if (onlyFirstTime && PlayerPrefs.GetInt(SeenKey, 0) == 1)
            {
                enabled = false;
                return;
            }

            // 按当前章节设置标题（第二章显示"第二章 · 回声"）
            var chap = AbyssEcho.LevelGen.ChapterLayout.Current;
            string full = AbyssEcho.LevelGen.ChapterLayout.GetChapterName(chap);   // 例："第一章 · 深潜"
            var parts = full.Split('·');
            if (parts.Length >= 2)
            {
                chapterNumber = parts[0].Trim();
                chapterName = parts[1].Trim();
            }

            _totalTime = blackHold + titleFadeIn + titleHold + fadeOut;
        }

        private void Start()
        {
            if (!enabled) return;

            _playing = true;
            _elapsed = 0f;

            if (_motor != null) _motor.enabled = false;
            if (_look != null) _look.enabled = false;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = false;
        }

        private void Update()
        {
            if (!_playing) return;

            _elapsed += Time.deltaTime;

            if (allowSkip && Input.GetKeyDown(KeyCode.Escape))
            {
                Finish();
                return;
            }

            if (_elapsed >= _totalTime) Finish();
        }

        private void Finish()
        {
            _playing = false;

            if (_motor != null) _motor.enabled = true;
            if (_look != null) _look.enabled = true;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            PlayerPrefs.SetInt(SeenKey, 1);
            PlayerPrefs.Save();

            enabled = false;
            Debug.Log("[TitleCard] 开场结束");
        }

        // ────────────────────────── 绘制 ──────────────────────────

        private void OnGUI()
        {
            if (!_playing) return;
            EnsureStyles();

            float t = _elapsed;

            // ① 黑幕不透明度
            float black;
            if (t < blackHold + titleFadeIn)
                black = 1f;                                  // 完全黑
            else if (t < blackHold + titleFadeIn + titleHold)
                black = 1f;                                  // 标题显示期间仍黑
            else
            {
                float k = (t - blackHold - titleFadeIn - titleHold) / fadeOut;
                black = 1f - Mathf.Clamp01(k);               // 淡出
            }

            // ② 标题不透明度
            float titleA = 0f;
            if (t >= blackHold)
            {
                float k = (t - blackHold) / titleFadeIn;
                titleA = Mathf.Clamp01(k);
            }

            var prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, black);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _white);
            GUI.color = prev;

            if (titleA <= 0.001f) return;

            float cy = Screen.height * 0.5f;

            _numStyle.normal.textColor = new Color(numberColor.r, numberColor.g, numberColor.b, titleA);
            GUI.Label(new Rect(0f, cy - 78f, Screen.width, 30f), chapterNumber, _numStyle);

            _nameStyle.normal.textColor = new Color(nameColor.r, nameColor.g, nameColor.b, titleA);
            GUI.Label(new Rect(0f, cy - 46f, Screen.width, 62f), chapterName, _nameStyle);

            _subStyle.normal.textColor = new Color(subtitleColor.r, subtitleColor.g, subtitleColor.b, titleA * 0.85f);
            GUI.Label(new Rect(0f, cy + 30f, Screen.width, 24f), subtitle, _subStyle);

            if (allowSkip)
            {
                _subStyle.normal.textColor = new Color(0.35f, 0.40f, 0.46f, titleA * 0.7f);
                GUI.Label(new Rect(0f, Screen.height - 60f, Screen.width, 22f), "按 Esc 跳过", _subStyle);
            }
        }

        private void EnsureStyles()
        {
            if (_numStyle == null)
            {
                _numStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = numberFontSize,
                    alignment = TextAnchor.MiddleCenter
                };
            }
            if (_nameStyle == null)
            {
                _nameStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = nameFontSize,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
            }
            if (_subStyle == null)
            {
                _subStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = subtitleFontSize,
                    alignment = TextAnchor.MiddleCenter
                };
            }
            ChineseFont.Apply(_numStyle);
            ChineseFont.Apply(_nameStyle);
            ChineseFont.Apply(_subStyle);
        }
    }
}
