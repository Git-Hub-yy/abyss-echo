using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// 和平模式（仅用于测试/调试）。
    ///
    /// ══════════════════ 做什么 ══════════════════
    ///
    ///   按 M 键切换。
    ///   开启时：怪物照常移动、巡逻、追击、消失、重生，
    ///          但**不会攻击玩家，也不会造成死亡**。
    ///   关闭时：一切恢复原样。
    ///
    /// ══════════════════ 为什么这样设计 ══════════════════
    ///
    ///   ① 不关怪物的 GameObject
    ///      任务要求"不要简单地关闭所有怪物，也不要让怪物全部消失"。
    ///      所以怪物照常活动，只是在"扣下扳机的那一刻"被拦住。
    ///
    ///   ② 不修改怪物脚本的核心逻辑
    ///      只在两个**攻击判定点**前插入一个静态开关检查：
    ///        · MimicStalker.UpdateApproach() 的扑击分支
    ///        · MonsterAI.CatchPlayer()
    ///      这两处是代码检索确认的全部死亡入口。
    ///
    ///   ③ 拦截点放在"攻击发生之前"而不是"死亡发生之后"
    ///      如果只拦 PlayerDeathSystem.Kill()，怪物仍会进入 Dead 状态被冻结，
    ///      玩家会看到怪物莫名其妙定住 —— 那不是"和平模式"，是"怪物坏了"。
    ///      在判定前拦截，怪物会继续追击，只是扑上来不杀人，体验更符合预期。
    ///
    /// ══════════════════ 输入方式 ══════════════════
    ///
    ///   ProjectSettings.activeInputHandler = 2（Both），
    ///   项目里 FlashlightSystem 也用 Input.GetKeyDown(KeyCode.F) 轮询，
    ///   所以这里沿用旧 Input API 轮询 M 键，与项目现有做法一致，
    ///   不会和玩家输入封装（PlayerInputReader）抢输入。
    ///
    ///   M 键在 AbyssEchoPlayer.inputactions 里没有绑定，不会冲突。
    /// </summary>
    public class PeaceModeManager : MonoBehaviour
    {
        [Header("开关")]
        [Tooltip("切换按键")]
        public KeyCode toggleKey = KeyCode.M;

        [Tooltip("启动时是否默认开启和平模式")]
        public bool startEnabled = false;

        [Header("提示")]
        [Tooltip("提示文字停留时间（秒）")]
        public float toastDuration = 2.0f;

        public int toastFontSize = 20;
        public Color onColor = new Color(0.55f, 0.90f, 0.65f, 0.95f);   // 绿
        public Color offColor = new Color(0.95f, 0.72f, 0.45f, 0.95f);  // 橙
        public Color panelColor = new Color(0.03f, 0.05f, 0.07f, 0.78f);

        /// <summary>当前是否处于和平模式。</summary>
        public static bool IsEnabled { get; private set; }

        /// <summary>单例（供外部查询）。</summary>
        public static PeaceModeManager Instance { get; private set; }

        private float _toastTimer;
        private bool _toastIsOn;
        private GUIStyle _toastStyle;
        private GUIStyle _hintStyle;
        private Texture2D _white;

        private void Awake()
        {
            // ⚠️ 防重复：场景重载时旧实例可能残留（若被 DontDestroyOnLoad 过）
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            _white = Texture2D.whiteTexture;

            // 状态**跨场景保留**：切章节后和平模式不自动关闭
            // （测试时通常希望它一直开着）
            IsEnabled = startEnabled;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                // 不重置 IsEnabled —— 允许跨场景保持
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                Toggle();
            }

            if (_toastTimer > 0f) _toastTimer -= Time.deltaTime;
        }

        /// <summary>切换和平模式。</summary>
        public void Toggle()
        {
            SetEnabled(!IsEnabled);
        }

        /// <summary>显式设置状态。</summary>
        public void SetEnabled(bool on)
        {
            IsEnabled = on;
            _toastIsOn = on;
            _toastTimer = toastDuration;

            Debug.Log($"[PeaceMode] 和平模式 = {(on ? "开启" : "关闭")}");
        }

        /// <summary>
        /// 怪物攻击前的检查入口。
        ///
        /// 返回 true 表示"允许攻击"，false 表示"被和平模式拦住"。
        /// 怪物脚本只需在扣扳机前调用它。
        /// </summary>
        public static bool AllowAttack => !IsEnabled;

        // ── 绘制提示 ──
        private void OnGUI()
        {
            if (_toastTimer <= 0f) return;
            EnsureStyles();

            float alpha = Mathf.Clamp01(_toastTimer / Mathf.Min(0.5f, toastDuration));

            string text = _toastIsOn ? "和平模式：开启" : "和平模式：关闭";
            var color = _toastIsOn ? onColor : offColor;

            var content = new GUIContent(text);
            Vector2 sz = _toastStyle.CalcSize(content);

            float padX = 22f, padY = 10f;
            float w = sz.x + padX * 2f;
            float h = sz.y + padY * 2f;

            // 位置：屏幕**上方靠中**，避开准星（屏幕正中）和右上角目标面板
            float x = (Screen.width - w) * 0.5f;
            float y = Screen.height * 0.14f;

            var prev = GUI.color;

            // 底板
            GUI.color = new Color(panelColor.r, panelColor.g, panelColor.b, panelColor.a * alpha);
            GUI.DrawTexture(new Rect(x, y, w, h), _white);

            // 左侧色条
            GUI.color = new Color(color.r, color.g, color.b, 0.9f * alpha);
            GUI.DrawTexture(new Rect(x, y, 3f, h), _white);

            // 文字
            GUI.color = prev;
            _toastStyle.normal.textColor = new Color(color.r, color.g, color.b, color.a * alpha);
            GUI.Label(new Rect(x, y, w, h), content, _toastStyle);

            GUI.color = prev;
        }

        private void EnsureStyles()
        {
            if (_toastStyle == null)
            {
                _toastStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = toastFontSize,
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold
                };
                AbyssEcho.UI.ChineseFont.Apply(_toastStyle);
            }
            if (_hintStyle == null)
            {
                _hintStyle = new GUIStyle(GUI.skin.label);
                AbyssEcho.UI.ChineseFont.Apply(_hintStyle);
            }
        }
    }
}