using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AbyssEcho.UI
{
    /// <summary>
    /// 主菜单：游戏启动后的第一个界面。
    ///
    /// 选项：
    ///   · 开始游戏     —— 有存档时显示「继续游戏」
    ///   · 新游戏       —— 清除存档，从 A0 深潜舱开始
    ///   · 退出
    ///
    /// 实现方式：
    ///   独立的 MainMenu 场景，只有相机 + 本脚本，不加载关卡。
    ///   点开始后 LoadScene("Level_Chapter1_Graybox")。
    ///
    /// 为什么用独立场景而不是叠加在关卡上：
    ///   关卡加载本身就比较重（259 个网格、35 个光源），
    ///   在主菜单阶段加载会拖慢启动。独立场景最轻。
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        [Header("场景名")]
        public string gameSceneName = "Level_Chapter1_Graybox";

        [Header("显示")]
        public string gameTitle = "ABYSS ECHO";
        public string gameSubtitle = "深渊回声";
        public string versionText = "第一章 · 垂直切片 v0.9";

        [Header("样式")]
        public int titleFontSize = 62;
        public int subtitleFontSize = 24;
        public int buttonFontSize = 21;
        public int hintFontSize = 14;

        public Color bgTop = new Color(0.06f, 0.08f, 0.11f);
        public Color bgBottom = new Color(0.01f, 0.02f, 0.03f);
        public Color titleColor = new Color(0.90f, 0.95f, 1f);
        public Color subtitleColor = new Color(0.45f, 0.60f, 0.75f);
        public Color buttonColor = new Color(0.10f, 0.13f, 0.17f);
        public Color buttonHoverColor = new Color(0.20f, 0.30f, 0.40f);
        public Color textColor = new Color(0.85f, 0.90f, 0.95f);
        public Color hintColor = new Color(0.35f, 0.40f, 0.47f);

        private bool _hasSave;
        private float _fadeIn;
        private bool _loading;

        /// <summary>章节选择面板。</summary>
        private ChapterSelectPanel _chapterPanel = new ChapterSelectPanel();

        private GUIStyle _titleStyle, _subStyle, _btnStyle, _hintStyle;
        private Texture2D _white;

        private void Awake()
        {
            _white = Texture2D.whiteTexture;
            _hasSave = AbyssEcho.Save.SaveSystem.HasSave();

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // 主菜单不限制帧率
            Application.targetFrameRate = 60;
        }

        private void Update()
        {
            if (_loading) return;

            _fadeIn = Mathf.MoveTowards(_fadeIn, 1f, Time.deltaTime * 1.2f);

            // 回车快捷开始
            if (_fadeIn >= 1f && Input.GetKeyDown(KeyCode.Return))
                StartGame(false);
        }

        private void StartGame(bool newGame)
        {
            if (_loading) return;
            _loading = true;

            // 应用章节选择（必须在加载场景前）
            _chapterPanel.Apply();
            Debug.Log("[MainMenu] 进入 " + AbyssEcho.LevelGen.ChapterLayout.GetChapterName(_chapterPanel.Selected));

            if (newGame)
            {
                AbyssEcho.Save.SaveSystem.Delete();
                // 让开场标题卡重新播放
                PlayerPrefs.DeleteKey("intro.chapter1.seen");
                PlayerPrefs.Save();
                Debug.Log("[MainMenu] 新游戏：存档已清除");
            }
            else
            {
                Debug.Log("[MainMenu] 继续游戏");
            }

            StartCoroutine(LoadGame());
        }

        private IEnumerator LoadGame()
        {
            yield return new WaitForSeconds(0.3f);

            // 按所选章节加载对应场景
            string targetScene = AbyssEcho.LevelGen.ChapterLayout.GetSceneName(_chapterPanel.Selected);
            Debug.Log("[MainMenu] 加载场景：" + targetScene);
            var op = SceneManager.LoadSceneAsync(targetScene);
            while (op != null && !op.isDone) yield return null;
        }

        private void OnGUI()
        {
            EnsureStyles();

            DrawBackground();

            float a = _fadeIn;
            float cy = Screen.height * 0.5f;

            // 标题
            _titleStyle.normal.textColor = new Color(titleColor.r, titleColor.g, titleColor.b, a);
            GUI.Label(new Rect(0f, cy - 190f, Screen.width, 80f), gameTitle, _titleStyle);

            _subStyle.normal.textColor = new Color(subtitleColor.r, subtitleColor.g, subtitleColor.b, a);
            GUI.Label(new Rect(0f, cy - 116f, Screen.width, 34f), gameSubtitle, _subStyle);

            // 分隔线
            var pc = GUI.color;
            GUI.color = new Color(0.30f, 0.45f, 0.60f, 0.35f * a);
            GUI.DrawTexture(new Rect(Screen.width * 0.5f - 120f, cy - 66f, 240f, 1.5f), _white);
            GUI.color = pc;

            // ── 章节选择（两个并排按钮）──
            float cw = 190f, ch = 44f, cgap = 12f;
            float totalW = cw * _chapterPanel.Chapters.Count + cgap * (_chapterPanel.Chapters.Count - 1);
            float cx0 = (Screen.width - totalW) * 0.5f;
            float cyRow = cy - 96f;

            for (int i = 0; i < _chapterPanel.Chapters.Count; i++)
            {
                var chap = _chapterPanel.Chapters[i];
                bool selected = chap == _chapterPanel.Selected;
                bool playable = _chapterPanel.IsPlayable(chap);

                var rect = new Rect(cx0 + i * (cw + cgap), cyRow, cw, ch);
                if (ChapterButton(rect, _chapterPanel.GetName(chap), a, selected, playable))
                {
                    _chapterPanel.Select(chap);
                    Debug.Log("[MainMenu] 选择章节：" + _chapterPanel.GetName(chap));
                }
            }

            // 按钮
            float bw = 300f, bh = 52f, gap = 12f;
            float bx = (Screen.width - bw) * 0.5f;
            float by = cy - 34f;

            if (_hasSave)
            {
                if (Button(new Rect(bx, by, bw, bh), "继续游戏", a)) StartGame(false);
                by += bh + gap;
            }

            if (Button(new Rect(bx, by, bw, bh), "开始游戏", a)) StartGame(true);
            by += bh + gap;

            if (Button(new Rect(bx, by, bw, bh), "退出", a, danger: true)) QuitGame();

            // 底部信息
            _hintStyle.normal.textColor = new Color(hintColor.r, hintColor.g, hintColor.b, a);
            GUI.Label(new Rect(0f, Screen.height - 84f, Screen.width, 22f), versionText, _hintStyle);

            string hint = _hasSave ? "回车继续　·　Esc 无操作" : "回车开始";
            GUI.Label(new Rect(0f, Screen.height - 58f, Screen.width, 22f), hint, _hintStyle);
        }

        private void DrawBackground()
        {
            // 垂直渐变（分段绘制，代价低）
            const int bands = 32;
            float bh = Screen.height / (float)bands;
            var prev = GUI.color;

            for (int i = 0; i < bands; i++)
            {
                float t = i / (float)(bands - 1);
                var c = Color.Lerp(bgTop, bgBottom, t);
                GUI.color = new Color(c.r, c.g, c.b, 1f);
                GUI.DrawTexture(new Rect(0f, i * bh, Screen.width, bh + 1f), _white);
            }

            GUI.color = prev;
        }

        /// <summary>章节选择按钮：选中时高亮，未开放时置灰。</summary>
        private bool ChapterButton(Rect r, string text, float alpha, bool selected, bool playable)
        {
            if (!playable) return false;

            bool hover = r.Contains(Event.current.mousePosition);

            var prev = GUI.color;

            // 背景
            Color bg;
            if (selected) bg = new Color(0.20f, 0.42f, 0.55f);
            else if (hover) bg = new Color(0.18f, 0.24f, 0.32f);
            else bg = new Color(0.09f, 0.12f, 0.16f);

            GUI.color = new Color(bg.r, bg.g, bg.b, alpha);
            GUI.DrawTexture(r, _white);

            // 选中时的下划线
            if (selected)
            {
                GUI.color = new Color(0.45f, 0.85f, 1f, alpha);
                GUI.DrawTexture(new Rect(r.x, r.yMax - 3f, r.width, 3f), _white);
            }

            GUI.color = prev;

            _chapterStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter
            };
            AbyssEcho.UI.ChineseFont.Apply(_chapterStyle);

            _chapterStyle.normal.textColor = selected
                ? new Color(0.90f, 0.96f, 1f, alpha)
                : new Color(0.62f, 0.70f, 0.78f, alpha);
            GUI.Label(r, text, _chapterStyle);

            if (hover && Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                Event.current.Use();
                return true;
            }
            return false;
        }

        private GUIStyle _chapterStyle;

        private bool Button(Rect r, string text, float alpha, bool danger = false)
        {
            bool hover = r.Contains(Event.current.mousePosition);

            var prev = GUI.color;
            GUI.color = new Color(
                (hover ? buttonHoverColor : buttonColor).r,
                (hover ? buttonHoverColor : buttonColor).g,
                (hover ? buttonHoverColor : buttonColor).b,
                alpha);
            GUI.DrawTexture(r, _white);

            // 左侧强调条
            GUI.color = new Color(danger ? 0.85f : 0.45f, danger ? 0.35f : 0.70f, danger ? 0.30f : 0.88f, alpha);
            GUI.DrawTexture(new Rect(r.x, r.y, 3f, r.height), _white);
            GUI.color = prev;

            _btnStyle.normal.textColor = new Color(textColor.r, textColor.g, textColor.b, alpha);
            GUI.Label(r, text, _btnStyle);

            if (hover && Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                Event.current.Use();
                return true;
            }
            return false;
        }

        private void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
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
            }
            if (_subStyle == null)
            {
                _subStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = subtitleFontSize,
                    alignment = TextAnchor.MiddleCenter
                };
            }
            if (_btnStyle == null)
            {
                _btnStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = buttonFontSize,
                    alignment = TextAnchor.MiddleCenter
                };
            }
            if (_hintStyle == null)
            {
                _hintStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = hintFontSize,
                    alignment = TextAnchor.MiddleCenter
                };
            }
            ChineseFont.Apply(_titleStyle);
            ChineseFont.Apply(_subStyle);
            ChineseFont.Apply(_btnStyle);
            ChineseFont.Apply(_hintStyle);
        }
    }
}
