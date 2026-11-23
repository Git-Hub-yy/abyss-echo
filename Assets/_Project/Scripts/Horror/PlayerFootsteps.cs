using UnityEngine;

namespace AbyssEcho.Horror
{
    /// <summary>
    /// 玩家脚步音频：根据移动速度与地面类型播放脚步声。
    ///
    /// 节奏设计：
    ///   · 走路：约每 0.52 秒一步
    ///   · 跑步：约每 0.34 秒一步
    ///   · 蹲行：约每 0.85 秒一步，音量更低
    ///   · 静止：不播放
    ///
    /// 地面类型：默认按金属地面处理（第一章走廊是花纹钢板），
    /// 后续可用射线检测区分混凝土/金属/水面。
    /// </summary>
    public class PlayerFootsteps : MonoBehaviour
    {
        [Header("引用")]
        public AbyssEcho.Player.PlayerMotor motor;

        [Header("节奏（秒/步）")]
        public float walkInterval = 0.52f;
        public float runInterval = 0.34f;
        public float crouchInterval = 0.85f;

        [Header("地面")]
        [Tooltip("当前是否在金属地面上（影响音色）")]
        public bool onMetalSurface = true;

        [Tooltip("是否自动通过射线检测地面材质")]
        public bool autoDetectSurface = false;
        public LayerMask surfaceLayers = ~0;

        private float _timer;
        private AbyssEcho.Audio.AudioManager _audio;

        private void Awake()
        {
            if (motor == null) motor = GetComponent<AbyssEcho.Player.PlayerMotor>();
            if (motor == null) motor = GetComponentInParent<AbyssEcho.Player.PlayerMotor>();
        }

        private void Update()
        {
            if (motor == null) return;

            _audio = AbyssEcho.Audio.AudioManager.Instance;
            if (_audio == null) return;

            float speed = motor.CurrentSpeed;

            // 几乎静止 → 不算步，重置计时
            if (speed < 0.15f || !motor.IsGrounded)
            {
                _timer = 0f;
                return;
            }

            float interval;
            float baseVolume;
            if (motor.IsCrouching) { interval = crouchInterval; baseVolume = 0.30f; }
            else if (motor.IsSprinting) { interval = runInterval; baseVolume = 0.85f; }
            else { interval = walkInterval; baseVolume = 0.60f; }

            // 速度越快节奏越紧凑
            interval *= Mathf.Clamp01(2.2f / Mathf.Max(0.1f, speed));

            _timer += Time.deltaTime;
            if (_timer >= interval)
            {
                _timer = 0f;

                if (autoDetectSurface) DetectSurface();

                _audio.PlayFootstep(transform.position, onMetalSurface);
            }
        }

        /// <summary>向下射线检测地面类型（金属材质的名字里含 Metal/Plate/Diamond）。</summary>
        private void DetectSurface()
        {
            if (Physics.Raycast(transform.position + Vector3.up * 0.3f, Vector3.down,
                                out var hit, 2f, surfaceLayers, QueryTriggerInteraction.Ignore))
            {
                var mat = hit.collider.GetComponent<Renderer>();
                if (mat != null && mat.sharedMaterial != null)
                {
                    string n = mat.sharedMaterial.name;
                    onMetalSurface = n.Contains("Metal") || n.Contains("Plate") ||
                                     n.Contains("Diamond") || n.Contains("Pipe");
                }
            }
        }
    }
}