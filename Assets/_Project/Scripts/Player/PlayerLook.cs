using System;
using UnityEngine;

namespace AbyssEcho.Player
{
    /// <summary>
    /// 第一人称视角控制（S2）。
    ///
    /// 结构约定：
    ///   Player (根)
    ///     └─ CameraPivot   ← 本脚本挂在它上面：只负责左右旋转（Yaw）
    ///          └─ Main Camera ← PlayerLook 内部旋转它的 X（Pitch）
    ///
    /// 为什么 Yaw 放在 Pivot、Pitch 放在 Camera：
    ///  - 移动方向由 Player 根节点的 forward 决定，Pivot 的 Yaw 与根节点旋转同步，避免"看哪走哪"的混淆；
    ///  - 相机仅上下旋转，不会因为俯仰影响水平朝向。
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerLook : MonoBehaviour
    {
        [Header("引用")]
        [Tooltip("玩家根节点（Player），用于左右旋转")]
        [SerializeField] private Transform playerRoot;
        [Tooltip("第一人称相机")]
        [SerializeField] private Camera playerCamera;
        [Tooltip("输入读取器（留空则自动从根节点获取）")]
        [SerializeField] private PlayerInputReader input;
        [Tooltip("蹲下时读取当前高度用（留空则自动从根节点获取）")]
        [SerializeField] private PlayerMotor motor;

        [Header("灵敏度")]
        [Tooltip("鼠标灵敏度（0.1 ~ 10），可在设置界面调整")]
        [Range(0.1f, 10f)]
        [SerializeField] private float mouseSensitivity = 2.0f;
        [Tooltip("Unity 的鼠标增量是像素，乘以该系数转换为角度")]
        [SerializeField] private float pixelsToDegrees = 0.05f;
        [Tooltip("是否反转 Y 轴")]
        [SerializeField] private bool invertY = false;

        [Header("视角限制")]
        [Tooltip("最大俯仰角（向上）")]
        [SerializeField] private float maxPitch = 85f;
        [Tooltip("最小俯仰角（向下）")]
        [SerializeField] private float minPitch = -85f;

        [Header("平滑")]
        [Tooltip("视角平滑时间（0 = 无平滑，越大越顺滑）")]
        [Range(0f, 0.15f)]
        [SerializeField] private float lookSmoothing = 0.02f;

        [Header("头部位置")]
        [Tooltip("站立时的眼睛高度（米）")]
        [SerializeField] private float standEyeHeight = 1.62f;
        [Tooltip("蹲下时的眼睛高度（米）")]
        [SerializeField] private float crouchEyeHeight = 1.02f;
        [Tooltip("蹲下/起身时相机高度过渡速度")]
        [SerializeField] private float eyeHeightLerpSpeed = 10f;

        /// <summary>当前灵敏度（供设置界面读写，设置结果写入 PlayerPrefs）。</summary>
        public float MouseSensitivity
        {
            get => mouseSensitivity;
            set => mouseSensitivity = Mathf.Clamp(value, 0.1f, 10f);
        }

        public bool InvertY
        {
            get => invertY;
            set => invertY = value;
        }

        private float _yaw;
        private float _pitch;
        private Vector2 _smoothedLook;
        private Vector2 _lookVelocity;
        private float _eyeHeightOffset;

        public const string SensPrefsKey = "settings.mouseSensitivity";
        public const string InvertPrefsKey = "settings.invertY";

        private void Awake()
        {
            if (playerRoot == null) playerRoot = transform;
            if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>();
            if (input == null) input = playerRoot.GetComponentInChildren<PlayerInputReader>();
            if (motor == null) motor = playerRoot.GetComponent<PlayerMotor>();

            // 从设置中恢复（首次运行使用 Inspector 默认值）
            mouseSensitivity = PlayerPrefs.GetFloat(SensPrefsKey, mouseSensitivity);
            invertY = PlayerPrefs.GetInt(InvertPrefsKey, invertY ? 1 : 0) == 1;

            // 初始化角度：以场景中当前朝向为准
            _yaw = playerRoot.eulerAngles.y;
            if (playerCamera != null)
            {
                float pitch = playerCamera.transform.localEulerAngles.x;
                _pitch = NormalizeAngle(pitch);
                _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
            }
            _eyeHeightOffset = standEyeHeight;
        }

        private void LateUpdate()
        {
            if (input == null || playerCamera == null) return;

            float dt = Time.deltaTime;

            // 1) 读取并平滑鼠标增量
            Vector2 raw = input.Look;
            if (lookSmoothing <= 0f)
            {
                _smoothedLook = raw;
            }
            else
            {
                _smoothedLook = Vector2.SmoothDamp(_smoothedLook, raw, ref _lookVelocity, lookSmoothing, Mathf.Infinity, dt);
            }

            if (!input.InputEnabled)
            {
                _smoothedLook = Vector2.zero;
                _lookVelocity = Vector2.zero;
            }

            float scale = mouseSensitivity * pixelsToDegrees;

            // 2) 旋转
            _yaw += _smoothedLook.x * scale;
            _pitch += (_smoothedLook.y * scale) * (invertY ? 1f : -1f);
            _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);

            playerRoot.localRotation = Quaternion.Euler(0f, _yaw, 0f);
            playerCamera.transform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);

            // 3) 眼睛高度随蹲下过渡
            float targetEye = (motor != null && motor.IsCrouching) ? crouchEyeHeight : standEyeHeight;
            _eyeHeightOffset = Mathf.Lerp(_eyeHeightOffset, targetEye, 1f - Mathf.Exp(-eyeHeightLerpSpeed * dt));

            Vector3 localPos = playerCamera.transform.localPosition;
            playerCamera.transform.localPosition = new Vector3(localPos.x, _eyeHeightOffset, localPos.z);
        }

        /// <summary>把 0~360 的角度转换为 -180~180。</summary>
        private static float NormalizeAngle(float a)
        {
            a %= 360f;
            if (a > 180f) a -= 360f;
            return a;
        }

        /// <summary>
        /// 设置水平朝向（Yaw）。
        /// 用途：传送玩家、剧情强制转向、自动化测试复位。
        /// 必须通过本方法设置，因为 PlayerLook 内部持有 _yaw 状态，直接改 Transform 会在下一帧被覆盖。
        /// </summary>
        public void SetYaw(float yawDegrees)
        {
            _yaw = yawDegrees;
            _smoothedLook = Vector2.zero;
            _lookVelocity = Vector2.zero;
            if (playerRoot != null)
                playerRoot.localRotation = Quaternion.Euler(0f, _yaw, 0f);
        }

        /// <summary>设置俯仰角（Pitch），会被自动夹紧到配置范围内。</summary>
        public void SetPitch(float pitchDegrees)
        {
            _pitch = Mathf.Clamp(pitchDegrees, minPitch, maxPitch);
            if (playerCamera != null)
                playerCamera.transform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        /// <summary>保存当前设置（设置界面关闭时调用），并通知外部刷新 UI。</summary>
        public void SaveSettings()
        {
            PlayerPrefs.SetFloat(SensPrefsKey, mouseSensitivity);
            PlayerPrefs.SetInt(InvertPrefsKey, invertY ? 1 : 0);
            PlayerPrefs.Save();
            OnSettingsChanged?.Invoke();
        }

        /// <summary>设置变化时触发（S7 设置界面会订阅；当前保留接口以便扩展）。</summary>
        public event Action OnSettingsChanged;
    }
}