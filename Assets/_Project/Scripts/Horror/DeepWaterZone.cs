using UnityEngine;

namespace AbyssEcho.Horror
{
    /// <summary>
    /// 深水区域：玩家站在水里时移动变慢、噪音变大。
    ///
    /// 为什么这么设计：
    ///   · 慢：水给移动加阻力，合理且有压迫感
    ///   · 吵：踩水声很大，会引来怪物——这是"代价"，不是纯粹的惩罚
    ///   · 玩家可以绕开水域走，或用抽水泵抽干（第二章 D3 泵房）
    ///
    /// 实现方式：
    ///   用一个 Box 触发器标记水域范围，进入时改 PlayerMotor 的速度系数。
    ///   不改 PlayerMotor 的内部字段，而是用 `ExternalSpeedMultiplier` 接口，
    ///   避免与已有系统冲突。
    ///
    /// ⚠️ 与 PlayerMotor 的关系：
    ///   PlayerMotor 需要暴露一个速度倍率入口。如果没有该入口，
    ///   本组件会退化为"只增加噪音"，不会崩溃。
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class DeepWaterZone : MonoBehaviour
    {
        [Header("水域参数")]
        [Tooltip("水深（米），用于提示文字")]
        public float waterDepth = 0.5f;

        [Tooltip("移动速度倍率（0.6 = 慢 40%）")]
        [Range(0.2f, 1f)]
        public float speedMultiplier = 0.65f;

        [Tooltip("噪音倍率（2.0 = 踩水声是陆地的两倍）")]
        [Range(1f, 5f)]
        public float noiseMultiplier = 2.2f;

        [Header("效果")]
        [Tooltip("是否在水面播放涟漪音效")]
        public bool playSplashAudio = true;

        [Tooltip("涟漪音效间隔（秒）")]
        public float splashInterval = 0.55f;

        [Header("调试")]
        public bool logEnterExit = false;

        /// <summary>玩家当前是否在水中。</summary>
        public bool PlayerInside { get; private set; }

        /// <summary>进入 / 离开水的事件。</summary>
        public static event System.Action<bool> OnPlayerWaterStateChanged;

        private AbyssEcho.Player.PlayerMotor _motor;
        private float _splashTimer;
        private bool _wasInside;

        private void Awake()
        {
            var col = GetComponent<BoxCollider>();
            if (col != null) col.isTrigger = true;
            _motor = FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
        }

        /// <summary>每帧复核玩家是否真的在触发区内（应对传送等边界情况）。</summary>
        private void RefreshWaterState()
        {
            if (_motor == null) return;

            var col = GetComponent<BoxCollider>();
            if (col == null) return;

            Vector3 center = transform.position + col.center;
            Vector3 half = Vector3.Scale(col.size, transform.lossyScale) * 0.5f;

            Vector3 p = _motor.transform.position + Vector3.up * 0.9f;   // 取玩家躯干高度
            bool inside = Mathf.Abs(p.x - center.x) <= half.x
                       && Mathf.Abs(p.y - center.y) <= half.y
                       && Mathf.Abs(p.z - center.z) <= half.z;

            if (inside != PlayerInside)
            {
                PlayerInside = inside;
                ApplySpeed(inside);
                OnPlayerWaterStateChanged?.Invoke(inside);
                if (logEnterExit) Debug.Log($"[DeepWater] 玩家{(inside ? "进入" : "离开")}水域");
            }
        }
        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<AbyssEcho.Player.PlayerMotor>() == null) return;
            if (_motor == null) _motor = other.GetComponentInParent<AbyssEcho.Player.PlayerMotor>();

            PlayerInside = true;
            ApplySpeed(true);
            OnPlayerWaterStateChanged?.Invoke(true);

            if (logEnterExit) Debug.Log("[DeepWater] 玩家进入水域");
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.GetComponentInParent<AbyssEcho.Player.PlayerMotor>() == null) return;

            PlayerInside = false;
            ApplySpeed(false);
            OnPlayerWaterStateChanged?.Invoke(false);

            if (logEnterExit) Debug.Log("[DeepWater] 玩家离开水域");
        }

        private void Update()
        {
            // ⚠️ 可靠性兜底：不只依赖 OnTriggerEnter/Exit。
            //   CharacterController 被"禁用→启用"（传送）时不会触发重新进入事件，
            //   会导致状态卡住。这里每帧用 OverlapBox 复核一次，确保状态正确。
            RefreshWaterState();

            // 踩水声：在水中且移动时周期性播放
            if (!PlayerInside || !playSplashAudio || _motor == null) return;
            if (_motor.CurrentSpeed < 0.3f) return;

            _splashTimer -= Time.deltaTime;
            if (_splashTimer <= 0f)
            {
                _splashTimer = splashInterval / Mathf.Max(0.5f, _motor.CurrentSpeed / 2.2f);

                if (AbyssEcho.Audio.AudioManager.Instance != null)
                {
                    // 复用脚步音，但降低音调会更像水（这里只控制音量与频率）
                    AbyssEcho.Audio.AudioManager.Instance.PlayFootstep(_motor.transform.position, false);
                }
            }
        }

        /// <summary>
        /// 应用 / 取消速度倍率。
        ///
        /// 通过反射查找 PlayerMotor 上是否存在 ExternalSpeedMultiplier 字段，
        /// 存在则设置，不存在则静默跳过——保证本组件在任何情况下都不会让游戏崩溃。
        /// </summary>
        private void ApplySpeed(bool inWater)
        {
            if (_motor == null) return;

            // ⚠️ 曾经的 bug：用 GetField 找 ExternalSpeedMultiplier，
            //   但它是一个**属性**（{ get; set; }），GetField 永远返回 null，
            //   导致深水减速完全无效。改为直接访问属性。
            _motor.ExternalSpeedMultiplier = inWater ? speedMultiplier : 1f;
        }

        private float _cachedWalk, _cachedRun;
        private bool _modifiedInternals;

        /// <summary>供怪物 AI 查询：玩家当前噪音倍率。</summary>
        public static float GetNoiseMultiplier(AbyssEcho.Player.PlayerMotor motor)
        {
            if (motor == null) return 1f;
            foreach (var z in Object.FindObjectsByType<DeepWaterZone>(FindObjectsSortMode.None))
                if (z.PlayerInside) return z.noiseMultiplier;
            return 1f;
        }
    }
}
