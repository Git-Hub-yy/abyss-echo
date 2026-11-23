using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// S63：第四章 G5 出口的方向指引。
    ///
    /// ══════════════════ 为什么需要 ══════════════════
    ///
    ///   G5 北墙有通往 G7 的门洞（宽 2.4m 高 3m），几何上完全通畅，
    ///   195 个采样点验证通过。但玩家反馈"找不到门"。
    ///
    ///   实测原因（真实渲染截图 + 像素分析）：
    ///     · 第四章 G 区原本一盏灯都没有（已修复，补了 31 盏）
    ///     · 即使补了灯，G5 的墙和门洞后面的通道都是同一材质的暗红，
    ///       在昏暗环境下轮廓很接近，不容易分辨"那是个能走的洞口"
    ///
    /// ══════════════════ 本组件做什么 ══════════════════
    ///
    ///   纯 OnGUI 绘制（不依赖 Canvas / Prefab / 贴图），规则：
    ///     · 玩家进入 G5 房间范围时显示
    ///     · 玩家朝向门洞（大致朝北且在门口附近）时高亮
    ///     · 玩家已经离开 G5 后自动隐藏
    ///
    ///   不影响任何玩法逻辑，只是屏幕上的文字。
    /// </summary>
    public class G5ExitGuide : MonoBehaviour
    {
        [Header("显示范围")]
        [Tooltip("玩家进入这个矩形（世界 XZ）才显示指引")]
        public Vector2 showZoneMin = new Vector2(-25f, -5f);   // G5 西南角
        public Vector2 showZoneMax = new Vector2(-13f, 8f);    // 含过渡区

        [Header("门洞位置")]
        public Vector3 doorCenter = new Vector3(-19f, 1.5f, 5f);

        [Header("样式")]
        public int fontSize = 18;
        public Color textColor = new Color(1f, 0.82f, 0.55f);
        public Color subColor = new Color(0.92f, 0.72f, 0.50f);
        public Color bgColor = new Color(0f, 0f, 0f, 0.55f);

        private AbyssEcho.Player.PlayerMotor _motor;
        private Camera _cam;
        private GUIStyle _mainStyle, _subStyle;
        private bool _ready;

        private void Start()
        {
            _motor = FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
            _cam = Camera.main;
            BuildStyles();
        }

        private void BuildStyles()
        {
            try
            {
                _mainStyle = AbyssEcho.UI.ChineseFont.CreateStyle(fontSize, FontStyle.Bold);
                _subStyle = AbyssEcho.UI.ChineseFont.CreateStyle(Mathf.Max(12, fontSize - 5), FontStyle.Normal);
                _mainStyle.alignment = TextAnchor.MiddleCenter;
                _subStyle.alignment = TextAnchor.MiddleCenter;
                _ready = true;
            }
            catch { _ready = false; }
        }

        private void OnGUI()
        {
            if (!_ready || _motor == null || _cam == null) return;

            Vector3 p = _motor.transform.position;

            // 1. 是否在显示区
            if (p.x < showZoneMin.x || p.x > showZoneMax.x ||
                p.z < showZoneMin.y || p.z > showZoneMax.y) return;

            // 2. 计算方向提示
            Vector3 toDoor = new Vector3(doorCenter.x - p.x, 0f, doorCenter.z - p.z);
            float dist = toDoor.magnitude;
            float facing = Vector3.Dot(_cam.transform.forward, toDoor.normalized);   // 1=正对

            string main, sub;
            if (dist < 1.6f)
            {
                main = "↑ 通道口";
                sub = "向前走，进入后向东（右手边）";
            }
            else if (facing > 0.55f)
            {
                // 已经朝着门洞
                main = "↑ 前方是通往核心腔的通道";
                sub = $"{dist:F0} 米";
            }
            else
            {
                // 没朝向门洞 —— 给方位提示
                float ang = Vector3.SignedAngle(_cam.transform.forward, toDoor.normalized, Vector3.up);
                string dir = ang > 45f ? "向右手边转" : (ang < -45f ? "向左手边转" : "前方");
                main = $"通道在{dir}";
                sub = $"距通道口 {dist:F0} 米";
            }

            // 3. 绘制（屏幕上方靠中，不挡准星）
            float w = 520f, h = 76f;
            float x = (Screen.width - w) * 0.5f;
            float y = Screen.height * 0.10f;

            var oldBg = GUI.color;
            GUI.color = bgColor;
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = oldBg;

            _mainStyle.normal.textColor = textColor;
            _subStyle.normal.textColor = subColor;

            GUI.Label(new Rect(x, y + 8f, w, 32f), main, _mainStyle);
            GUI.Label(new Rect(x, y + 42f, w, 26f), sub, _subStyle);
        }
    }
}