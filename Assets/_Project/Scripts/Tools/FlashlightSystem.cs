using System;
using AbyssEcho.Player;
using UnityEngine;

namespace AbyssEcho.Tools
{
    /// <summary>
    /// 手电筒系统（S2）。
    ///
    /// 职责：
    ///  开关（F 键，由 PlayerInputReader 事件驱动）
    ///  电量消耗 / 低电量闪烁 / 耗尽熄灭
    ///  更换电池（由外部调用 AddBattery，S4 的拾取系统会接上）
    ///
    /// 设计：
    ///  光照使用子物体的 Spot Light，强度与角度可配；
    ///  电量耗尽时无法再开启，但手电筒本身不消失（恐怖游戏常见做法，避免玩家卡死）。
    /// </summary>
    [DisallowMultipleComponent]
    public class FlashlightSystem : MonoBehaviour
    {
        [Header("引用")]
        [Tooltip("手电筒光源（Spot Light），留空则自动在子物体中查找")]
        [SerializeField] private Light spotLight;
        [Tooltip("输入源（留空则自动在父级中查找）")]
        [SerializeField] private PlayerInputReader input;
        [Tooltip("手电筒模型根节点（用于摆动，可留空）")]
        [SerializeField] private Transform modelRoot;

        [Header("电量")]
        [Tooltip("初始电量百分比（0~100）")]
        [Range(0f, 100f)]
        [SerializeField] private float batteryPercent = 100f;
        [Tooltip("满电可用时长（秒）")]
        [SerializeField] private float batteryLifetimeSeconds = 900f;
        [Tooltip("低电量阈值（低于该值开始闪烁）")]
        [Range(0f, 50f)]
        [SerializeField] private float lowBatteryThreshold = 20f;
        [Tooltip("一次更换电池补充的电量（百分比）")]
        [Range(1f, 100f)]
        [SerializeField] private float batteryRechargeAmount = 100f;

        [Header("光照")]
        [Tooltip("正常亮度")]
        // ══════════════════ S52 修复：3.2 → 15 ══════════════════
        //
        //   实测数据（Ch3 黑暗环境，真实 GPU 渲染截图，中心区域亮度）：
        //     intensity=3.2  → 提升 0.0390（3.2/255 看不出光斑）❌
        //     intensity=8    → 提升 0.0762（勉强可见）△
        //     intensity=15   → 提升 0.1150（清晰可见光斑）✅ ← 采用
        //     intensity=25   → 提升 0.1602（更亮，但可能过曝）
        //
        //   原因：URP 的附加灯光（Additional Light）使用物理光照模型，
        //   Spot Light 的 intensity=3.2 在 range=22 的衰减下，
        //   2~5 米处照度不足以在画面中形成可见光斑。
        //
        //   注意：range 缩短反而更暗（实测 range=8 时提升仅 0.04），
        //   因为 range 是衰减上限距离，缩短它不会提升近距离照度。
        //   所以只调 intensity，range/spotAngle 保持原值。
        [SerializeField] private float normalIntensity = 15f;
        [Tooltip("光锥角度")]
        [SerializeField] private float spotAngle = 55f;
        [Tooltip("照射距离")]
        [SerializeField] private float range = 22f;

        [Header("开关手感")]
        [Tooltip("开灯时的轻微延迟（秒），模拟按钮响应")]
        [SerializeField] private float turnOnDelay = 0.04f;

        [Header("运行时状态（只读）")]
        [SerializeField] private bool isOn;
        [SerializeField] private bool hasBatteryInstalled = true;

        public bool IsOn => isOn;
        public float BatteryPercent => batteryPercent;
        public bool IsLowBattery => batteryPercent > 0f && batteryPercent <= lowBatteryThreshold;
        public bool HasBattery => batteryPercent > 0f && hasBatteryInstalled;

        /// <summary>手电亮度是否正在影响世界（供怪物视觉检测使用）。</summary>
        public bool IsEmittingLight => isOn && SpotLightIntensity01 > 0.05f;

        /// <summary>当前光强的归一化值（0~1），供 AI 判断"玩家是否被照亮"。</summary>
        public float SpotLightIntensity01 =>
            normalIntensity <= 0f ? 0f : Mathf.Clamp01((spotLight != null ? spotLight.intensity : 0f) / normalIntensity);

        public event Action<bool> OnFlashlightToggled;
        public event Action<float> OnBatteryChanged;
        public event Action OnBatteryDepleted;
        public event Action OnBatteryRecharged;

        private float _onDelayTimer;
        private float _flickerSeed;

        private void Awake()
        {
            if (input == null) input = GetComponentInParent<PlayerInputReader>();
            _flickerSeed = UnityEngine.Random.Range(0f, 100f);

            // ⚠️ 注意：不在这里查找 Spot Light。
            //   S3B 实测：PlayerHands 会在自己的 Awake 中生成程序化手部与 SpotLight，
            //   而 FlashlightSystem 挂在玩家根节点上，其 Awake 可能先执行，
            //   此时子物体的灯还不存在 → 日志报"未找到 Spot Light"，手电筒全程不亮。
            //   因此改为在 Start（所有 Awake 执行完毕）中查找。
        }

        private void Start()
        {
            ResolveSpotLight();
        }

        /// <summary>查找并初始化手电筒光源。可被重复调用（幂等）。</summary>
        private void ResolveSpotLight()
        {
            if (spotLight == null)
                spotLight = GetComponentInChildren<Light>(true);

            if (spotLight == null) return;

            spotLight.type = LightType.Spot;
            spotLight.spotAngle = spotAngle;
            spotLight.range = range;
            spotLight.shadows = LightShadows.Soft;
            spotLight.intensity = 0f;
            spotLight.enabled = false;
            _lightResolved = true;

            // ══════════════════ S51 修复：状态与灯光不同步 ══════════════════
            //
            //   ResolveSpotLight() 无条件把灯关掉（intensity=0, enabled=false），
            //   但**没有同步 isOn 字段**。
            //
            //   后果：
            //     · 若场景里 isOn = true（第一章就是这样），
            //       开局 isOn=true 而灯是灭的 → 状态错位。
            //         Toggle() 看到 isOn=true → 走"关灯"分支 → 灯本来就灭，看不出变化，
            //       玩家以为"第一次按 F 没反应"，必须按第二次才亮。
            //     · 若场景里 isOn = false（Ch2~Ch5），
            //       状态是对的，但叠加下面的"F 键双触发"后依然表现为切换错位。
            //
            //   修复：初始化完成后，把 isOn 显式置为 false，
            //   保证"状态 = 灯实际状态"，且开局统一为关闭。
            isOn = false;
        }

        private bool _lightResolved;

        private void OnEnable()
        {
            // ⚠️ S51 修复（第三章黑暗环境手电筒失效的主因）：
            //
            //   问题：这里订阅了 input.OnFlashlightToggle 事件，
            //        同时 Update() 里又轮询 Input.GetKeyDown(KeyCode.F)。
            //        两条路径**同时生效**，按一次 F 会执行两次 Toggle：
            //            第 1 次：灯亮
            //            第 2 次：灯灭
            //        结果玩家看到"按 F 没反应"，黑暗里彻底看不见东西。
            //
            //   S21 当初加轮询兜底，是因为 OnEnable 早于 Awake 执行，
            //   此时 input 还是 null，订阅失败，只能用轮询。
            //   但后来 input 在 Awake 里赋值成功，订阅也开始生效，
            //   两条路径就叠加成了双触发。
            //
            //   修复：**只保留 Update() 里的轮询**，删掉事件订阅。
            //
            //   为什么保留轮询而不是保留事件：
            //     · 轮询用 Input.GetKeyDown，不依赖 input 引用是否成功赋值
            //     · 已实测五章 input 引用都能拿到，但轮询更稳（不依赖组件初始化顺序）
            //     · PlayerInputReader 的 OnFlashlightToggle 仍保留给其他系统订阅，
            //       不影响其他章节或其它功能
            //
            //   注意：不能在 OnDisable 里再 -= Toggle（那里也没订阅了），
            //        所以 OnDisable 保持为空。
        }

        private void OnDisable()
        {
            // S51：已改为纯轮询输入，无需退订。
            // （保留方法本身，避免将来若恢复订阅时忘记配对）
        }

        private void Update()
        {
            // ⚠️ S21 修复（玩家实测"按 F 根本没啥用"）：
            //   OnEnable 里订阅 input.OnFlashlightToggle 时，
            //   input 可能还是 null（Awake 才赋值），事件永远订阅不上。
            //   这里直接轮询 F 键兜底，保证手电筒一定能开关。
            if (Input.GetKeyDown(KeyCode.F))
            {
                Toggle();
            }

            float dt = Time.deltaTime;

            // 开灯延迟
            if (_onDelayTimer > 0f)
            {
                _onDelayTimer -= dt;
                if (_onDelayTimer <= 0f) ApplyLightOn();
            }

            if (isOn && hasBatteryInstalled && batteryPercent > 0f)
            {
                float lifetime = Mathf.Max(1f, batteryLifetimeSeconds);
                float delta = (100f / lifetime) * dt;
                batteryPercent = Mathf.Max(0f, batteryPercent - delta);
                OnBatteryChanged?.Invoke(batteryPercent);

                if (batteryPercent <= 0f)
                {
                    batteryPercent = 0f;
                    SetLightOn(false);
                    OnBatteryDepleted?.Invoke();
                    Debug.Log("[FlashlightSystem] 电量耗尽，手电筒熄灭。需要更换电池。");
                }
            }

            UpdateLightIntensity();
            UpdateModelSway(dt);
        }

        // ────────────────────────── 公开接口 ──────────────────────────

        public void Toggle()
        {
            // 兜底：若光源此前未解析成功（例如手部在更晚的帧才生成），这里再试一次
            if (!_lightResolved || spotLight == null)
                ResolveSpotLight();

            if (isOn)
            {
                SetLightOn(false);
                return;
            }

            if (!hasBatteryInstalled || batteryPercent <= 0f)
            {
                // 电量耗尽：给出声音/提示的时机由 UI 层处理，这里只拒绝开启并派发事件
                OnBatteryDepleted?.Invoke();
                Debug.Log("[FlashlightSystem] 电量耗尽，无法开启。请更换电池（E 拾取电池）。");
                return;
            }

            if (turnOnDelay > 0f)
            {
                _onDelayTimer = turnOnDelay;
                isOn = true;              // 立即记录状态，避免连按导致重复
                OnFlashlightToggled?.Invoke(true);
            }
            else
            {
                SetLightOn(true);
            }
        }

        /// <summary>更换/安装电池（S4 拾取电池后调用）。</summary>
        public void AddBattery(float percent = -1f)
        {
            float add = percent < 0f ? batteryRechargeAmount : percent;
            hasBatteryInstalled = true;
            batteryPercent = Mathf.Clamp(batteryPercent + add, 0f, 100f);
            OnBatteryChanged?.Invoke(batteryPercent);
            OnBatteryRecharged?.Invoke();
            Debug.Log("[FlashlightSystem] 已更换电池，当前电量：" + batteryPercent.ToString("F0") + "%");
        }

        /// <summary>是否拥有可更换的电池（供 UI 显示）。</summary>
        public bool CanRecharge => batteryPercent < 99.9f;

        // ────────────────────────── 内部 ──────────────────────────

        private void SetLightOn(bool on)
        {
            _onDelayTimer = 0f;
            if (isOn == on && (spotLight == null || spotLight.enabled == on))
            {
                ApplyLightOn();
                return;
            }

            isOn = on;
            if (on) ApplyLightOn();
            else if (spotLight != null)
            {
                spotLight.enabled = false;
                spotLight.intensity = 0f;
            }

            OnFlashlightToggled?.Invoke(isOn);
        }

        private void ApplyLightOn()
        {
            if (spotLight == null) return;
            spotLight.enabled = true;
            spotLight.intensity = normalIntensity;
        }

        private void UpdateLightIntensity()
        {
            if (spotLight == null || !spotLight.enabled) return;

            if (!isOn)
            {
                spotLight.intensity = 0f;
                return;
            }

            float intensity = normalIntensity;

            if (IsLowBattery)
            {
                // 低电量：20% 以下逐渐不稳定，5% 以下剧烈闪烁
                float severity = Mathf.InverseLerp(lowBatteryThreshold, 5f, batteryPercent);
                severity = Mathf.Clamp01(severity);

                float noise = Mathf.PerlinNoise(_flickerSeed, Time.time * (6f + severity * 14f));
                float dip = Mathf.Lerp(1f, 0.55f, severity);
                intensity = normalIntensity * Mathf.Lerp(dip, 1f, noise);

                // 偶发短暂熄灭
                if (severity > 0.6f && Mathf.PerlinNoise(_flickerSeed + 17f, Time.time * 2.5f) > 0.82f)
                    intensity = 0f;
            }

            spotLight.intensity = intensity;
        }

        private void UpdateModelSway(float dt)
        {
            if (modelRoot == null) return;

            // 极轻微的呼吸式摆动，避免画面死板；幅度很小以免造成眩晕
            float t = Time.time;
            Quaternion target = Quaternion.Euler(
                Mathf.Sin(t * 1.1f) * 0.35f,
                Mathf.Sin(t * 0.7f) * 0.5f,
                Mathf.Sin(t * 1.3f) * 0.25f);

            modelRoot.localRotation = Quaternion.Slerp(modelRoot.localRotation, target, 1f - Mathf.Exp(-6f * dt));
        }
    }
}