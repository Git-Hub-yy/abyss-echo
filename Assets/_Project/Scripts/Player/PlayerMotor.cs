using UnityEngine;

namespace AbyssEcho.Player
{
    /// <summary>
    /// 第一人称移动（S2）。基于 CharacterController，不使用 Rigidbody。
    ///
    /// 为什么用 CharacterController：
    ///  恐怖游戏需要不弹跳、不穿墙、行走手感可调的移动；
    ///  Rigidbody 的物理插值会让第一人称镜头在狭窄走廊里抖动。
    ///
    /// 职责边界：
    ///  本类只负责水平移动、重力、蹲下、体力。
    ///  视角由 PlayerLook 负责；输入由 PlayerInputReader 提供。
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInputReader))]
    [DisallowMultipleComponent]
    public class PlayerMotor : MonoBehaviour
    {
        [Header("移动速度")]
        [Tooltip("行走速度（m/s）")]
        [SerializeField] private float walkSpeed = 2.2f;
        [Tooltip("奔跑速度（m/s）")]
        [SerializeField] private float runSpeed = 4.2f;
        [Tooltip("蹲行速度（m/s）")]
        [SerializeField] private float crouchSpeed = 1.1f;

        [Header("加速度（越大越硬，越小越滑）")]
        [Tooltip("地面加速（m/s²）")]
        [SerializeField] private float groundAcceleration = 18f;
        [Tooltip("地面减速（m/s²）")]
        [SerializeField] private float groundDeceleration = 22f;
        [Tooltip("空中加速度（m/s²）")]
        [SerializeField] private float airAcceleration = 4f;

        [Header("重力")]
        [SerializeField] private float gravity = -20f;
        [Tooltip("贴地检测的额外向下的速度，避免下坡时抖动")]
        [SerializeField] private float groundSnapSpeed = -2f;

        [Header("蹲下")]
        [Tooltip("站立时胶囊体高度")]
        [SerializeField] private float standHeight = 1.78f;
        [Tooltip("蹲下时胶囊体高度")]
        [SerializeField] private float crouchHeight = 1.15f;
        [Tooltip("蹲下/起身过渡时间（秒）")]
        [SerializeField] private float crouchTransitionTime = 0.18f;

        [Header("体力")]
        [Tooltip("最大体力")]
        [SerializeField] private float maxStamina = 6f;
        [Tooltip("奔跑时体力消耗（每秒）")]
        [SerializeField] private float staminaDrainPerSecond = 1f;
        [Tooltip("恢复速度（每秒）")]
        [SerializeField] private float staminaRegenPerSecond = 0.6f;
        [Tooltip("体力耗尽后需等待多久才能再次奔跑（秒）")]
        [SerializeField] private float staminaRecoverDelay = 1.5f;

        [Header("跳跃（S2 默认关闭，按需开启）")]
        [SerializeField] private bool allowJump = false;
        [SerializeField] private float jumpHeight = 1.1f;

        [Header("状态（只读，供调试查看）")]
        [SerializeField] private bool isGrounded;
        [SerializeField] private bool isCrouching;
        [SerializeField] private bool isSprinting;
        [SerializeField] private float currentStamina;
        [SerializeField] private float currentSpeed;

        // ── 公开只读属性（供其它系统读取） ──
        /// <summary>
        /// 外部速度倍率（默认 1）。用于深水、泥泞等环境减速。
        /// 由 DeepWaterZone 等组件设置，不改动 walkSpeed/runSpeed 原始值。
        /// </summary>
        public float ExternalSpeedMultiplier { get; set; } = 1f;
        public bool IsGrounded => isGrounded;
        public bool IsCrouching => isCrouching;
        public bool IsSprinting => isSprinting;
        public float CurrentStamina => currentStamina;
        public float MaxStamina => maxStamina;
        public float NormalizedStamina => maxStamina <= 0f ? 0f : Mathf.Clamp01(currentStamina / maxStamina);
        /// <summary>当前水平速度（m/s），供脚步声/怪物听觉使用。</summary>
        public float CurrentSpeed => currentSpeed;
        /// <summary>蹲下时的眼睛高度（由 PlayerLook 读取）。</summary>
        public float CurrentStandHeight =>
            isCrouching || _crouchT > 0.5f ? crouchHeight : standHeight;

        private CharacterController _controller;
        private PlayerInputReader _input;

        private Vector3 _horizontalVelocity;
        private float _verticalVelocity;
        private float _crouchT;              // 0 = 站立，1 = 蹲下
        private float _staminaExhaustedTimer;
        private float _originalCenterY;
        private float _originalHeight;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _input = GetComponent<PlayerInputReader>();

            _originalHeight = _controller.height;
            _originalCenterY = _controller.center.y;

            // 统一按配置初始化，避免场景里手改胶囊体导致高度不一致
            standHeight = Mathf.Max(standHeight, 1.0f);
            crouchHeight = Mathf.Clamp(crouchHeight, 0.6f, standHeight);
            ApplyControllerHeight(standHeight);

            currentStamina = maxStamina;
        }

        private void OnEnable()
        {
            if (_input != null) _input.OnJump += HandleJump;
        }

        private void OnDisable()
        {
            if (_input != null) _input.OnJump -= HandleJump;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (_input == null)
            {
                Debug.LogError("[PlayerMotor] 缺少 PlayerInputReader 组件。");
                enabled = false;
                return;
            }

            UpdateGrounded();
            UpdateCrouch(dt);
            UpdateStamina(dt);

            Vector2 moveInput = _input.Move;
            bool wantsRun = _input.RunHeld && !isCrouching && CanSprint();
            bool wantsCrouch = _input.CrouchHeld;

            isSprinting = wantsRun && moveInput.sqrMagnitude > 0.01f && isGrounded;

            float targetSpeed = isCrouching ? crouchSpeed : (isSprinting ? runSpeed : walkSpeed);
            Vector3 desired = transform.right * moveInput.x + transform.forward * moveInput.y;
            if (desired.sqrMagnitude > 1f) desired.Normalize();
            desired *= targetSpeed;
            desired *= ExternalSpeedMultiplier;   // 外部倍率（深水等），默认 1

            float accel = isGrounded
                ? (desired.sqrMagnitude > _horizontalVelocity.sqrMagnitude ? groundAcceleration : groundDeceleration)
                : airAcceleration;

            _horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, desired, accel * dt);

            // 重力
            if (isGrounded && _verticalVelocity < 0f)
                _verticalVelocity = groundSnapSpeed;
            else
                _verticalVelocity += gravity * dt;

            Vector3 motion = _horizontalVelocity * dt;
            motion.y = _verticalVelocity * dt;
            _controller.Move(motion);

            // 记录实际水平速度（被墙挡住时会小于期望速度，供脚步声使用）
            currentSpeed = new Vector3(_horizontalVelocity.x, 0f, _horizontalVelocity.z).magnitude;
        }

        // ────────────────────────── 子步骤 ──────────────────────────

        private void UpdateGrounded()
        {
            // CharacterController.isGrounded 在台阶/斜坡上有时不稳定，
            // 这里叠加一个极短的向下的 SphereCast 作为补充判断。
            bool ccGrounded = _controller.isGrounded;
            bool probeGrounded = Physics.SphereCast(
                transform.position + Vector3.up * (_controller.radius + 0.05f),
                _controller.radius * 0.95f,
                Vector3.down,
                out _,
                0.12f,
                ~0,
                QueryTriggerInteraction.Ignore);

            isGrounded = ccGrounded || probeGrounded;
        }

        private void UpdateCrouch(float dt)
        {
            bool wantCrouch = _input.CrouchHeld;

            if (wantCrouch && !isCrouching)
            {
                isCrouching = true;      // 蹲下总是允许
            }
            else if (!wantCrouch && isCrouching)
            {
                // 起身需要空间：头顶有障碍时保持蹲下
                if (HasHeadroom())
                    isCrouching = false;
            }

            float target = isCrouching ? 1f : 0f;
            _crouchT = Mathf.MoveTowards(_crouchT, target, dt / Mathf.Max(0.01f, crouchTransitionTime));

            float h = Mathf.Lerp(standHeight, crouchHeight, _crouchT);
            ApplyControllerHeight(h);
        }

        /// <summary>头顶是否有足够空间起身。</summary>
        private bool HasHeadroom()
        {
            float radius = _controller.radius * 0.95f;
            Vector3 origin = transform.position + Vector3.up * (crouchHeight - radius);
            float distance = standHeight - crouchHeight;

            return !Physics.SphereCast(
                origin,
                radius,
                Vector3.up,
                out _,
                distance + 0.05f,
                ~0,
                QueryTriggerInteraction.Ignore);
        }

        private void ApplyControllerHeight(float height)
        {
            float clamped = Mathf.Clamp(height, 0.6f, standHeight);
            _controller.height = clamped;
            // 保持脚底位置不变：中心 = 高度/2
            _controller.center = new Vector3(_controller.center.x, clamped * 0.5f, _controller.center.z);
        }

        private void UpdateStamina(float dt)
        {
            if (_staminaExhaustedTimer > 0f)
                _staminaExhaustedTimer -= dt;

            if (isSprinting)
                currentStamina = Mathf.Max(0f, currentStamina - staminaDrainPerSecond * dt);
            else
                currentStamina = Mathf.Min(maxStamina, currentStamina + staminaRegenPerSecond * dt);

            if (currentStamina <= 0.001f && _staminaExhaustedTimer <= 0f)
                _staminaExhaustedTimer = staminaRecoverDelay;
        }

        private bool CanSprint()
        {
            return currentStamina > 0.05f && _staminaExhaustedTimer <= 0f;
        }

        private void HandleJump()
        {
            if (!allowJump || !isGrounded || isCrouching) return;
            _verticalVelocity = Mathf.Sqrt(-2f * gravity * jumpHeight);
        }

        // ────────────────────────── 编辑器辅助 ──────────────────────────

        private void OnValidate()
        {
            standHeight = Mathf.Max(1.0f, standHeight);
            crouchHeight = Mathf.Clamp(crouchHeight, 0.6f, standHeight);
            maxStamina = Mathf.Max(1f, maxStamina);
            if (_controller != null)
                ApplyControllerHeight(isCrouching ? crouchHeight : standHeight);
        }
    }
}
