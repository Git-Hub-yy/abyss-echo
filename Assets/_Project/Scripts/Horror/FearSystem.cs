using UnityEngine;

namespace AbyssEcho.Horror
{
    /// <summary>
    /// 恐惧值系统：玩家的心理压力值，驱动心跳、视野、灯光等恐怖表现。
    ///
    /// 数值设计：
    ///   0.0 ~ 0.3  平静：心跳轻微，视野正常
    ///   0.3 ~ 0.6  紧张：心跳加快，画面轻微暗角加深
    ///   0.6 ~ 0.85 恐惧：心跳明显，呼吸声，手电筒开始不稳
    ///   0.85 ~ 1.0 濒临崩溃：强烈暗角、画面抖动、呼吸急促
    ///
    /// 恐惧值来源（由其他系统调用 AddFear）：
    ///   · 黑暗中待得越久 → 缓慢上升
    ///   · 怪物在附近 → 快速上升
    ///   · 触发惊吓事件 → 瞬间跳升
    ///   · 手电筒打开 + 处于光下 → 缓慢下降
    ///
    /// 本类不直接操作画面，只维护数值并派发事件，
    /// 由 FearVisuals 等表现层订阅，保证逻辑与表现解耦。
    /// </summary>
    public class FearSystem : MonoBehaviour
    {
        public static FearSystem Instance { get; private set; }

        [Header("恐惧值")]
        [Range(0f, 1f)] public float currentFear = 0f;

        [Tooltip("自然恢复速度（每秒，在安全环境下）")]
        public float recoveryRate = 0.035f;

        [Tooltip("黑暗中的累积速度（每秒）")]
        public float darkRiseRate = 0.012f;

        [Header("状态阈值")]
        public float tenseThreshold = 0.30f;
        public float fearThreshold = 0.60f;
        public float panicThreshold = 0.85f;

        [Header("引用")]
        public AbyssEcho.Player.PlayerInputReader input;
        public AbyssEcho.Tools.FlashlightSystem flashlight;

        /// <summary>恐惧值变化事件（新值）。</summary>
        public event System.Action<float> OnFearChanged;

        /// <summary>跨越阈值时触发（阈值等级 0-3）。</summary>
        public event System.Action<int> OnFearLevelChanged;

        public int CurrentLevel { get; private set; }

        /// <summary>是否处于黑暗中（由外部每帧设置；默认不黑）。</summary>
        public bool IsInDarkness { get; set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;

            if (input == null) input = FindFirstObjectByType<AbyssEcho.Player.PlayerInputReader>();
            if (flashlight == null) flashlight = FindFirstObjectByType<AbyssEcho.Tools.FlashlightSystem>();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            // 自然变化：黑暗中上升，否则恢复
            if (IsInDarkness)
                currentFear += darkRiseRate * dt;
            else
                currentFear -= recoveryRate * dt;

            currentFear = Mathf.Clamp01(currentFear);
            OnFearChanged?.Invoke(currentFear);

            // 等级变化检测（与 AddFear/SetFear 共用同一处逻辑）
            RefreshLevel();

            // 心跳跟随恐惧
            if (AbyssEcho.Audio.AudioManager.Instance != null)
                AbyssEcho.Audio.AudioManager.Instance.SetHeartbeatIntensity(
                    Mathf.InverseLerp(tenseThreshold * 0.5f, panicThreshold, currentFear));
        }

        private int ComputeLevel(float v)
        {
            if (v >= panicThreshold) return 3;
            if (v >= fearThreshold) return 2;
            if (v >= tenseThreshold) return 1;
            return 0;
        }

        // ────────────────────────── 公开接口 ──────────────────────────

        /// <summary>增加恐惧值（自动夹紧到 0-1）。</summary>
        public void AddFear(float amount)
        {
            if (Mathf.Approximately(amount, 0f)) return;
            currentFear = Mathf.Clamp01(currentFear + amount);
            RefreshLevel();
            OnFearChanged?.Invoke(currentFear);
        }

        /// <summary>立即设置恐惧值。</summary>
        public void SetFear(float value)
        {
            currentFear = Mathf.Clamp01(value);
            RefreshLevel();
            OnFearChanged?.Invoke(currentFear);
        }

        /// <summary>
        /// 立即重算当前等级。
        ///
        /// ⚠️ 修复：原实现只在 Update() 里更新 CurrentLevel，
        ///   若外部在同一帧内连续调用 SetFear（例如自动化测试、剧本事件），
        ///   等级会滞后一帧，读到的是旧值。
        ///   现在所有改变恐惧值的入口都会立刻刷新等级。
        /// </summary>
        private void RefreshLevel()
        {
            int level = ComputeLevel(currentFear);
            if (level == CurrentLevel) return;
            CurrentLevel = level;
            OnFearLevelChanged?.Invoke(level);
        }

        /// <summary>恐惧等级的文字描述（用于调试 HUD）。</summary>
        public string GetLevelName()
        {
            switch (CurrentLevel)
            {
                case 3: return "濒临崩溃";
                case 2: return "恐惧";
                case 1: return "紧张";
                default: return "平静";
            }
        }
    }
}