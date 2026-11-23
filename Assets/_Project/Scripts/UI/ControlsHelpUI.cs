using System.Collections;
using UnityEngine;

namespace AbyssEcho.UI
{
    /// <summary>
    /// 操作提示面板：告诉玩家能做什么、按什么键。
    ///
    /// 为什么需要：
    ///   游戏里所有操作都靠键位，但玩家进游戏时**看不到任何提示**。
    ///   这不是"硬核"，是设计缺失——玩家不知道能跑、能蹲、
    ///   能开手电筒、能看日志，等于把功能藏起来了。
    ///
    /// 出现时机：
    ///   ① 第一次进入关卡时自动显示（约 8 秒后自动淡出）
    ///   ② 任何时候按 H 打开 / 关闭
    ///
    /// 显示方式：
    ///   左侧一列分组列出按键，右侧保持场景可见（半透明背景），
    ///   不像全屏模态框那样打断沉浸感。
    /// </summary>
    public class ControlsHelpUI : MonoBehaviour
    {
        [Header("自动显示")]
        [Tooltip("进入关卡时是否自动显示一次")]
        public bool showOnFirstEnter = true;

        [Tooltip("自动显示停留多久（秒）")]
        public float autoShowDuration = 9f;

        [Header("外观")]
        public float panelWidth = 430f;
        public Color panelColor = new Color(0.03f, 0.05f, 0.07f, 0.90f);
        public Color borderColor = new Color(0.35f, 0.62f, 0.85f, 0.75f);
        public Color headerColor = new Color(0.55f, 0.82f, 1f);
        public Color keyColor = new Color(1f, 0.78f, 0.35f);
        public Color descColor = new Color(0.85f, 0.89f, 0.93f);
        public Color hintColor = new Color(0.45f, 0.52f, 0.60f);

        /// <summary>面板是否打开。</summary>
        public bool IsOpen { get; private set; }

        // ── 按键表 ──
        private static readonly (string group, (string key, string desc)[] items)[] Sections =
        {
            ("移动", new[]
            {
                ("W A S D", "行走"),
                ("鼠标", "环视"),
                ("Shift", "奔跑（消耗体力）"),
                ("Ctrl", "蹲下（更安静）"),
            }),
            ("交互", new[]
            {
                ("E", "拾取 / 开门 / 使用设备"),
                ("F", "手电筒开关"),
            }),
            ("界面", new[]
            {
                ("Tab", "物品栏"),
                ("J", "日志（已发现的线索）"),
                ("H", "操作说明（本面板）"),
                ("Esc", "暂停菜单"),
            }),
            ("特殊", new[]
            {
                ("空格", "跳过结局（结局播放中）"),
                ("M", "和平模式（关闭怪物攻击）"),
                ("R", "死亡后重新开始"),
                ("↑ ↓", "在某些面板中调节数值"),
                ("回车", "在面板中确认"),
            }),
        };

        private float _autoHideAt = -1f;
        private bool _shownOnce;

        private AbyssEcho.Player.PlayerMotor _motor;
        private AbyssEcho.Player.PlayerLook _look;
        private AbyssEcho.Player.PlayerInputReader _input;
        private AbyssEcho.Interaction.InteractionDetector _detector;

        private GUIStyle _headerStyle, _keyStyle, _descStyle, _titleStyle, _hintStyle;
        private Texture2D _white;

        private const string PrefsKey = "ui.controlsShown";

        private void Awake()
        {
            _white = Texture2D.whiteTexture;
            _motor = FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
            _look = FindFirstObjectByType<AbyssEcho.Player.PlayerLook>();
            _input = FindFirstObjectByType<AbyssEcho.Player.PlayerInputReader>();
            _detector = FindFirstObjectByType<AbyssEcho.Interaction.InteractionDetector>();
        }

        private IEnumerator Start()
        {
            yield return null;   // 等一帧，确保所有系统就位

            if (showOnFirstEnter && PlayerPrefs.GetInt(PrefsKey, 0) == 0)
            {
                // 延迟 1.2 秒再出现，避免和开场标题卡撞在一起
                yield return new WaitForSeconds(1.2f);
                Open(autoHide: true);
                PlayerPrefs.SetInt(PrefsKey, 1);
                PlayerPrefs.Save();
            }
        }

        private void Update()
        {
            // H 键开关
            if (Input.GetKeyDown(KeyCode.H))
            {
                if (IsOpen) Close();
                else Open(autoHide: false);
                return;
            }

            // 自动隐藏倒计时
            if (IsOpen && _autoHideAt > 0f && Time.time >= _autoHideAt)
            {
                Close();
                return;
            }

            // 自动显示期间，玩家开始移动就提前收起（不打断操作）
            if (IsOpen && _autoHideAt > 0f && _motor != null && _motor.CurrentSpeed > 0.5f)
            {
                Close();
            }
        }

        public void Open(bool autoHide)
        {
            if (IsOpen) return;
            IsOpen = true;
            _shownOnce = true;
            _autoHideAt = autoHide ? Time.time + autoShowDuration : -1f;

            // 提示面板不冻结玩家（只是参考信息），但要释放鼠标吗？
            // 结论：不释放。玩家看着提示就能继续走，
            // 想关掉时按 H 即可。这样最不打断操作。
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            _autoHideAt = -1f;
        }

        // ────────────────────────── 绘制 ──────────────────────────

        private void OnGUI()
        {
            if (!IsOpen) return;
            EnsureStyles();

            // 计算面板高度
            int totalRows = 0;
            foreach (var s in Sections) totalRows += s.items.Length + 1;   // +1 是分组标题
            float rowH = 26f;
            float h = totalRows * rowH + 96f;
            float w = Mathf.Min(panelWidth, Screen.width - 80f);

            var panel = new Rect(28f, 120f, w, h);

            var prev = GUI.color;
            GUI.color = panelColor;
            GUI.DrawTexture(panel, _white);

            GUI.color = borderColor;
            const float t = 2f;
            GUI.DrawTexture(new Rect(panel.x, panel.y, panel.width, t), _white);
            GUI.DrawTexture(new Rect(panel.x, panel.yMax - t, panel.width, t), _white);
            GUI.DrawTexture(new Rect(panel.x, panel.y, t, panel.height), _white);
            GUI.DrawTexture(new Rect(panel.xMax - t, panel.y, t, panel.height), _white);
            GUI.color = prev;

            float x = panel.x + 22f;
            float y = panel.y + 18f;
            float cw = panel.width - 44f;

            _titleStyle.normal.textColor = headerColor;
            GUI.Label(new Rect(x, y, cw, 30f), "操作说明", _titleStyle);
            y += 38f;

            foreach (var section in Sections)
            {
                // 分组标题
                _headerStyle.normal.textColor = headerColor;
                GUI.Label(new Rect(x, y, cw, 24f), "— " + section.group + " —", _headerStyle);
                y += rowH;

                foreach (var (key, desc) in section.items)
                {
                    _keyStyle.normal.textColor = keyColor;
                    GUI.Label(new Rect(x + 8f, y, 96f, rowH), key, _keyStyle);

                    _descStyle.normal.textColor = descColor;
                    GUI.Label(new Rect(x + 108f, y, cw - 108f, rowH), desc, _descStyle);

                    y += rowH;
                }

                y += 4f;
            }

            _hintStyle.normal.textColor = hintColor;
            GUI.Label(new Rect(x, panel.yMax - 28f, cw, 20f), "按 H 关闭", _hintStyle);
        }

        private void EnsureStyles()
        {
            if (_titleStyle == null)
            {
                _titleStyle = AbyssEcho.UI.ChineseFont.CreateStyle(20, FontStyle.Bold);
                AbyssEcho.UI.ChineseFont.Apply(_titleStyle);
            }
            if (_headerStyle == null)
            {
                _headerStyle = AbyssEcho.UI.ChineseFont.CreateStyle(14, FontStyle.Bold);
                AbyssEcho.UI.ChineseFont.Apply(_headerStyle);
            }
            if (_keyStyle == null)
            {
                _keyStyle = AbyssEcho.UI.ChineseFont.CreateStyle(15, FontStyle.Bold);
                _keyStyle.alignment = TextAnchor.MiddleLeft;
                AbyssEcho.UI.ChineseFont.Apply(_keyStyle);
            }
            if (_descStyle == null)
            {
                _descStyle = AbyssEcho.UI.ChineseFont.CreateStyle(15);
                _descStyle.alignment = TextAnchor.MiddleLeft;
                AbyssEcho.UI.ChineseFont.Apply(_descStyle);
            }
            if (_hintStyle == null)
            {
                _hintStyle = AbyssEcho.UI.ChineseFont.CreateStyle(13);
                _hintStyle.alignment = TextAnchor.MiddleRight;
                AbyssEcho.UI.ChineseFont.Apply(_hintStyle);
            }
        }
    }
}