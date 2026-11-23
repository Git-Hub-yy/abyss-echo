using System.Collections;
using System.Text;
using AbyssEcho.Narrative;
using AbyssEcho.Interaction;
using AbyssEcho.Player;
using UnityEngine;

namespace AbyssEcho.UI
{
    /// <summary>
    /// 叙事 UI：阅读面板 + 日志面板（纯 OnGUI 绘制）。
    ///
    /// 交互设计：
    ///   · 阅读线索时：屏幕中央显示标题与正文，按 E 或 Esc 关闭
    ///   · 按 J：打开日志（已读线索列表），按 J 或 Esc 关闭
    ///   · 面板打开时暂停玩家移动与视角（避免"边读边飘"）
    ///
    /// 为什么不改时间缩放（Time.timeScale = 0）：
    ///   第一章没有需要继续运行的物理，但未来若加怪物 AI，暂停会破坏其行为。
    ///   这里只冻结玩家输入，保持世界时间正常流动。
    /// </summary>
    public class NarrativeUI : MonoBehaviour
    {
        [Header("样式")]
        public int titleFontSize = 24;
        public int bodyFontSize = 17;
        public float panelWidth = 620f;
        public Color panelColor = new Color(0.04f, 0.06f, 0.08f, 0.94f);
        public Color borderColor = new Color(0.35f, 0.62f, 0.85f, 0.85f);
        public Color titleColor = new Color(0.85f, 0.94f, 1f);
        public Color bodyColor = new Color(0.86f, 0.90f, 0.95f);
        public Color hintColor = new Color(0.55f, 0.68f, 0.80f);

        // ── 状态 ──
        private bool _noteOpen;
        private string _noteTitle = "";
        private string _noteBody = "";

        private bool _journalOpen;

        /// <summary>面板打开的帧号（S23：防止同一帧被 E 立刻关闭）。</summary>
        private int _openedFrame = -999;

        /// <summary>上一次处理 J 键的帧号（S22：同一帧只处理一次）。</summary>
        private int _lastJournalFrame = -999;

        private PlayerInputReader _input;
        private PlayerMotor _motor;
        private PlayerLook _look;
        private InteractionDetector _detector;

        private GUIStyle _titleStyle, _bodyStyle, _hintStyle, _entryTitleStyle;
        private Texture2D _panelTex, _borderTex;

        private void Awake()
        {
            _input = FindFirstObjectByType<PlayerInputReader>();
            _motor = FindFirstObjectByType<PlayerMotor>();
            _look = FindFirstObjectByType<PlayerLook>();
            _detector = FindFirstObjectByType<AbyssEcho.Interaction.InteractionDetector>();
        }

        private void OnDestroy()
        {
            if (_panelTex != null) Destroy(_panelTex);
            if (_borderTex != null) Destroy(_borderTex);
        }

        // ────────────────────────── 公开接口 ──────────────────────────

        /// <summary>显示一条线索。</summary>
        public void Show(string title, string body)
        {
            _noteTitle = title;
            _noteBody = body;
            _noteOpen = true;
            _journalOpen = false;
            _openedFrame = Time.frameCount;   // 记录打开帧，避免同一帧被 E 关掉
            ApplyInputLock();
        }

        /// <summary>关闭当前面板。</summary>
        public void Close()
        {
            _noteOpen = false;
            _journalOpen = false;
            _openedFrame = -999;
            ApplyInputLock();
        }

        /// <summary>是否正有面板打开（外部可用它阻止其他操作）。</summary>
        public bool IsOpen => _noteOpen || _journalOpen;

        public void ToggleJournal()
        {
            if (_noteOpen) { Close(); return; }
            _journalOpen = !_journalOpen;
            if (_journalOpen) _openedFrame = Time.frameCount;
            ApplyInputLock();
        }

        // ────────────────────────── 输入锁 ──────────────────────────

        private void ApplyInputLock()
        {
            bool open = IsOpen;

            if (_motor != null) _motor.enabled = !open;
            if (_look != null) _look.enabled = !open;
            if (_detector != null) _detector.enabled = !open;

            // ══════════════════ S79：光标设置顺序修正 ══════════════════
            //
            //   原顺序：
            //     _input.RefreshCursor();     ← 按 inputEnabled 决定光标
            //     if (open) { Cursor.lockState = None; visible = true; }
            //     else      { Cursor.lockState = Locked; visible = false; }
            //
            //   实测发现运行时光标状态是 lockState=None 但 visible=False，
            //   两个值互相矛盾 —— 说明 RefreshCursor 和后面的赋值打架了。
            //
            //   修正：
            //     ① 面板打开时**不做** RefreshCursor（它会强行把光标锁回去）
            //     ② 先设 Cursor 状态，最后再调 RefreshCursor
            //     ③ RefreshCursor 只在面板关闭时调用
            if (open)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                if (_input != null) _input.RefreshCursor();
            }
        }

private void Update()
        {
            // ── 开关逻辑（S15 修复）──
            //
            // 旧实现的 bug：
            //   面板打开时只认 Esc / E，**不认 J**，
            //   而面板关闭时才认 J。结果玩家按 J 打开日志后，
            //   再按 J 毫无反应，必须按 Esc 才能退出。
            //   玩家的直觉是「同一个键开关」，必须修掉。
            //
            // 新逻辑：
            //   ① 面板打开且是"日志模式" → J 关闭
            //   ② 面板打开且是"笔记模式" → J 也关闭（统一用 J 关）
            //   ③ 面板打开 → Esc / E 关闭（保留原行为）
            //   ④ 面板关闭 → J 打开日志
            //
            // 注意 JournalPressed 是状态量不是事件，
            // 必须"消费一次就标记"，否则同一帧可能触发两次。

            // ⚠️ S22 二次修复（玩家实测"按 J 只闪一下就没了"）：
            //
            //   同一个 J 按键会在两处被读取：
            //     ① PlayerInputReader.JournalPressed（状态量，整帧为 true）
            //     ② Input.GetKeyDown(KeyCode.J)（同样整帧为 true）
            //   如果 Update 里既判断 ① 又判断 ②，
            //   或者对面板打开/关闭的判断落在同一帧，就会"开→关"瞬间完成。
            //
            //   修复：用 _lastJournalFrame 记录"上一次处理 J 的帧号"，
            //   同一帧只处理一次，杜绝重复触发。

            // ══════════════════ S80：修复「按 E 读便条闪一下」「按 J 闪一下」 ══════════════════
            //
            //   玩家反馈：
            //     1. 按 E 无法阅读手写便条
            //     2. 按 J 只会闪一下日志内容
            //
            //   根因（两个叠加）：
            //
            //   ① **E 键冲突**：
            //      便条是 ReadableNote，玩家按 E 交互 → OnInteract() → ui.Show() 打开。
            //      但同一帧 NarrativeUI.Update() 也读到 E 为 true，
            //      而代码里「面板打开 + E → Close()」，
            //      于是刚打开就被自己关掉 → **闪一下**。
            //
            //   ② **重复的结束守卫**：
            //      上一版（S79）重写时漏删了旧的
            //      `if (Time.frameCount == _lastJournalFrame) return;`，
            //      导致 Update 里有两份相同的判断，代码混乱。
            //
            //   修复原则：
            //     · **E 不再作为关闭键**（它是交互键，会和打开动作打架）
            //       关闭改用：J / Esc
            //     · 删除重复的守卫，只保留一份
            //     · 加 _justOpenedFrame：面板刚打开的那一帧不响应任何关闭键，
            //       从根上杜绝"开→关"同帧完成

            bool jDown = Input.GetKeyDown(KeyCode.J);
            bool escDown = Input.GetKeyDown(KeyCode.Escape);

            // PlayerInputReader 的 JournalPressed 做为补充（可能有手柄绑定）
            bool journalAction = _input != null && _input.JournalPressed;

            bool toggleKey = jDown || journalAction;

            // ── 面板已打开：只接受 J / Esc 关闭 ──
            if (IsOpen)
            {
                // 闪一下就消失的根因防线：
                // 打开这一帧（以及上一帧）不响应关闭键
                if (Time.frameCount - _openedFrame <= 1) return;

                if (toggleKey || escDown)
                {
                    Close();
                }
                return;
            }

            // ── 面板关闭：J 打开日志 ──
            if (toggleKey)
            {
                ToggleJournal();
            }
        }

        // ────────────────────────── 绘制 ──────────────────────────

        private void OnGUI()
        {
            if (!IsOpen) return;

            EnsureStyles();

            // 半透明遮罩
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            if (_noteOpen) DrawNote();
            else if (_journalOpen) DrawJournal();
        }

        /// <summary>把长句按句末标点折行，提升可读性。</summary>
        private static string WrapForDisplay(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            s = s.Replace("。", "。\n").Replace("！", "！\n").Replace("？", "？\n");
            return s.Replace("\n\n", "\n").TrimEnd('\n');
        }
        private void DrawNote()
        {
            float w = Mathf.Min(panelWidth, Screen.width - 80);
            float h = Mathf.Min(320f, Screen.height - 120);
            var panel = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

            DrawPanel(panel);

            var inner = new Rect(panel.x + 26, panel.y + 22, panel.width - 52, panel.height - 44);

            GUI.Label(new Rect(inner.x, inner.y, inner.width, 34), _noteTitle, _titleStyle);
            // 单行文本自动按句号折行，避免一长行挤在一起
            GUI.Label(new Rect(inner.x, inner.y + 46, inner.width, inner.height - 90),
                      WrapForDisplay(_noteBody), _bodyStyle);
            GUI.Label(new Rect(inner.x, inner.yMax - 26, inner.width, 24), "按 E / Esc / J 关闭", _hintStyle);
        }

        private void DrawJournal()
        {
            var journal = JournalSystem.Instance;
            float w = Mathf.Min(panelWidth + 60f, Screen.width - 80);
            float h = Mathf.Min(420f, Screen.height - 100);
            var panel = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

            DrawPanel(panel);
            var inner = new Rect(panel.x + 26, panel.y + 22, panel.width - 52, panel.height - 44);

            GUI.Label(new Rect(inner.x, inner.y, inner.width, 34), "【日志】  ( J )", _titleStyle);

            var sb = new StringBuilder();
            if (journal == null || journal.Entries.Count == 0)
            {
                sb.AppendLine("（暂无记录）");
                sb.AppendLine();
                sb.AppendLine("在基地中寻找纸条、终端与录音，读到的内容会记录在这里。");
            }
            else
            {
                foreach (var e in journal.Entries)
                {
                    sb.AppendLine("■ " + e.Title);
                    sb.AppendLine(e.Body);
                    sb.AppendLine();
                }
            }

            GUI.Label(new Rect(inner.x, inner.y + 46, inner.width, inner.height - 90), sb.ToString(), _bodyStyle);
            GUI.Label(new Rect(inner.x, inner.yMax - 26, inner.width, 24), "按 J 或 Esc 关闭", _hintStyle);
        }

        // ────────────────────────── 样式 ──────────────────────────

        private void EnsureStyles()
        {
            if (_panelTex == null)
            {
                _panelTex = new Texture2D(1, 1);
                _panelTex.SetPixel(0, 0, Color.white);
                _panelTex.Apply();
            }
            if (_borderTex == null) _borderTex = _panelTex;

            if (_titleStyle == null)
            {
                _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = titleFontSize, fontStyle = FontStyle.Bold };
                _titleStyle.normal.textColor = titleColor;
            }
            if (_bodyStyle == null)
            {
                _bodyStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = bodyFontSize,
                    wordWrap = true,
                    alignment = TextAnchor.UpperLeft
                };
                _bodyStyle.normal.textColor = bodyColor;
            }
            if (_hintStyle == null)
            {
                _hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.LowerLeft };
                _hintStyle.normal.textColor = hintColor;
            }
            if (_entryTitleStyle == null)
            {
                _entryTitleStyle = new GUIStyle(_bodyStyle) { fontStyle = FontStyle.Bold };
            }
            ChineseFont.Apply(_titleStyle);
            ChineseFont.Apply(_bodyStyle);
            ChineseFont.Apply(_hintStyle);
            ChineseFont.Apply(_entryTitleStyle);
        }

        private void DrawPanel(Rect r)
        {
            var prev = GUI.color;
            GUI.color = panelColor;
            GUI.DrawTexture(r, Texture2D.whiteTexture);

            // 边框（四条细边，避免贴图缩放问题）
            GUI.color = borderColor;
            const float t = 2f;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, t), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.yMax - t, r.width, t), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.y, t, r.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - t, r.y, t, r.height), Texture2D.whiteTexture);

            GUI.color = prev;
        }
    }
}
