using AbyssEcho.Player;
using AbyssEcho.Tools;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// S2 调试 HUD：在游戏画面左上角显示移动/视角/体力/手电筒状态。
    ///
    /// 用途：
    ///  1) 让玩家（你）能直观看到每个功能是否在工作；
    ///  2) 自动化测试时可通过读取这些值做断言。
    ///
    /// 正式版会由 UIManager 接管（S7），届时本组件只需移除即可，不影响其它系统。
    /// </summary>
    public class PlayerDebugHUD : MonoBehaviour
    {
        [Tooltip("是否显示调试 HUD（正式版关闭）")]
        public bool showHud = true;

        [Tooltip("字体大小")]
        public int fontSize = 15;

        private PlayerMotor _motor;
        private PlayerLook _look;
        private PlayerInputReader _input;
        private FlashlightSystem _flash;
        private IDebugStatusSource[] _extras;

        private GUIStyle _style;
        private float _fpsSmooth = 60f;

        private void Awake()
        {
            // 使用 2023.1 推荐的非过时 API（FindFirstObjectByType / FindObjectsByType）
            _motor = FindFirstObjectByType<PlayerMotor>();
            _look = FindFirstObjectByType<PlayerLook>();
            _input = FindFirstObjectByType<PlayerInputReader>();
            _flash = FindFirstObjectByType<FlashlightSystem>();

            var all = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            var list = new System.Collections.Generic.List<IDebugStatusSource>();
            foreach (var mb in all)
                if (mb is IDebugStatusSource src) list.Add(src);
            _extras = list.ToArray();
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt > 0f)
                _fpsSmooth = Mathf.Lerp(_fpsSmooth, 1f / dt, 0.05f);
        }

        private void OnGUI()
        {
            if (!showHud) return;

            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = fontSize,
                    richText = true,
                    alignment = TextAnchor.UpperLeft
                };
                AbyssEcho.UI.ChineseFont.Apply(_style);
            }

            var sb = new System.Text.StringBuilder(512);
            sb.AppendLine("<b>ABYSS ECHO · S2 调试</b>");
            sb.AppendLine($"FPS: {_fpsSmooth:F0}");

            if (_motor != null)
            {
                sb.AppendLine($"位置: {_motor.transform.position.x:F2}, {_motor.transform.position.y:F2}, {_motor.transform.position.z:F2}");
                sb.AppendLine($"速度: {_motor.CurrentSpeed:F2} m/s   {( _motor.IsSprinting ? "<color=#ffcc44>[奔跑]</color>" : "")}");
                sb.AppendLine($"体力: {_motor.NormalizedStamina * 100f:F0}%");
                sb.AppendLine($"地面: {(_motor.IsGrounded ? "是" : "否")}   蹲下: {(_motor.IsCrouching ? "<color=#66ccff>是</color>" : "否")}");
            }

            if (_input != null)
            {
                sb.AppendLine($"输入: {(_input.InputEnabled ? "启用" : "禁用")}");
                sb.AppendLine($"移动输入: {_input.Move.x:F2}, {_input.Move.y:F2}");
            }

            if (_flash != null)
            {
                sb.AppendLine($"手电: {(_flash.IsOn ? "<color=#aaccff>开</color>" : "关")}  电量: {_flash.BatteryPercent:F0}%{(_flash.IsLowBattery ? " <color=#ff8844>[低]</color>" : "")}");
            }

            if (_extras != null)
            {
                foreach (var e in _extras)
                {
                    var line = e?.GetDebugLine();
                    if (!string.IsNullOrEmpty(line)) sb.AppendLine(line);
                }
            }

            sb.AppendLine("<color=#889099>F 手电 / Shift 跑 / Ctrl 蹲 / E 伸手</color>");

            var rect = new Rect(12f, 12f, 460f, 260f);
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(rect.x + 10f, rect.y + 8f, rect.width - 20f, rect.height - 16f), sb.ToString(), _style);
        }
    }

    /// <summary>让其它系统也能向调试 HUD 输出一行状态（接口便于扩展）。</summary>
    public interface IDebugStatusSource
    {
        string GetDebugLine();
    }
}
