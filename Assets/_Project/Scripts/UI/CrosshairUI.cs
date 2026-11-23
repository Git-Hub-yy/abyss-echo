using AbyssEcho.Interaction;
using UnityEngine;

namespace AbyssEcho.UI
{
    /// <summary>
    /// 准星 + 交互提示 UI（纯代码绘制，无需预制体与贴图）。
    ///
    /// 为什么不用 uGUI/TMP：
    ///   - 第一章只需要一个点 + 一行字，用 OnGUI 绘制最轻量
    ///   - 避免引入 Canvas 层级、字体资源、缩放适配等额外复杂度
    ///   - 后续做完整 HUD 时再统一替换为 Canvas，界面逻辑已分离，替换成本低
    ///
    /// 挂载：任意常驻对象（建议挂在 Player 或 --- DEBUG --- 上）
    /// </summary>
    public class CrosshairUI : MonoBehaviour
    {
        [Header("引用")]
        [Tooltip("留空则自动在场景中查找")]
        public InteractionDetector detector;

        [Header("准星")]
        public Color idleColor = new Color(1f, 1f, 1f, 0.45f);
        public Color activeColor = new Color(0.45f, 0.85f, 1f, 0.95f);
        public float dotSize = 5f;
        public float dotSizeActive = 9f;

        [Header("提示文本")]
        public int fontSize = 18;
        public Color textColor = new Color(0.92f, 0.96f, 1f, 0.95f);
        public Color disabledTextColor = new Color(0.85f, 0.55f, 0.45f, 0.95f);
        [Tooltip("提示文字相对屏幕中心的下方偏移（像素）")]
        public float promptOffsetY = 46f;

        private GUIStyle _dotStyle;
        private GUIStyle _textStyle;

        private void Awake()
        {
            if (detector == null) detector = FindFirstObjectByType<InteractionDetector>();
        }

        private void OnGUI()
        {
            EnsureStyles();

            bool has = detector != null && detector.HasTarget;
            bool usable = has && detector.Current != null && detector.Current.CanInteract;

            // ① 准星圆点
            float size = has ? dotSizeActive : dotSize;
            var c = has ? activeColor : idleColor;
            var prev = GUI.color;
            GUI.color = c;
            var rect = new Rect((Screen.width - size) * 0.5f, (Screen.height - size) * 0.5f, size, size);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = prev;

            // ② 交互提示
            if (has)
            {
                string prompt = detector.CurrentPrompt;
                if (!string.IsNullOrEmpty(prompt))
                {
                    _textStyle.normal.textColor = usable ? textColor : disabledTextColor;
                    var content = new GUIContent(prompt);
                    Vector2 sz = _textStyle.CalcSize(content);
                    var textRect = new Rect(
                        (Screen.width - sz.x) * 0.5f,
                        Screen.height * 0.5f + promptOffsetY - sz.y * 0.5f,
                        sz.x, sz.y);
                    GUI.Label(textRect, content, _textStyle);
                }
            }
        }

        private void EnsureStyles()
        {
            if (_dotStyle == null)
                _dotStyle = new GUIStyle();

            if (_textStyle == null)
            {
                _textStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = Mathf.Max(10, fontSize),
                    wordWrap = false
                };
                _textStyle.normal.textColor = textColor;
            }
            ChineseFont.Apply(_dotStyle);
            ChineseFont.Apply(_textStyle);
        }
    }
}
