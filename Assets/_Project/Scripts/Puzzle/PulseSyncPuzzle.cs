using AbyssEcho.Interaction;
using UnityEngine;

namespace AbyssEcho.Puzzle
{
    /// <summary>
    /// 脉搏同步机关（第四章 G 区核心谜题）。
    ///
    /// ══════════════════════ 叙事依据 ══════════════════════
    ///
    ///  第四章的主题是"这里不是建筑，是某种东西的内部"。
    ///  所以 G 区的谜题不该是键盘、开关这类"人造物"，
    ///  而应该是**跟这个生物同步** —— 你要跟上它的心跳。
    ///
    /// ══════════════════════ 玩法 ══════════════════════
    ///
    ///  核心腔中央有一块"心瓣"（软的地面凸起），它会按固定节奏搏动。
    ///
    ///  玩家必须在**搏动的瞬间**站到心瓣上（或对它按 E），
    ///  连续同步 3 次才算成功，才能打开通往 G7 深层的门。
    ///
    ///    · 太早 → 没有反应，计时器清零
    ///    · 太晚 → 没反应
    ///    · 正好 → "同步"计数 +1，播放低沉的脉动声
    ///
    /// ══════════════════════ 为什么这个谜题好 ══════════════════════
    ///
    ///  对比前几章：
    ///    第一章 = 找东西（保险丝）
    ///    第二章 = 调数值（频率 220）
    ///    第三章 = 做选择（供奉什么）
    ///    第四章 = **跟上节奏**（时机类）
    ///
    ///  这是四种完全不同的能力，玩家不会觉得"又是开门"。
    ///
    /// ══════════════════════ 宽容度 ══════════════════════
    ///
    ///  窗口 = ±0.35 秒。这个值反复斟酌过：
    ///    · 太窄（±0.15）→ 玩家会烦
    ///    · 太宽（±0.6）→ 没有紧张感
    ///
    ///  另外加了"节奏预告"：每次搏动前 0.4 秒，心瓣会先亮一下，
    ///  让玩家有预判的依据，而不是纯靠猜。
    /// </summary>
    public class PulseSyncPuzzle : InteractableBase
    {
        [Header("节奏")]
        [Tooltip("两次搏动之间的间隔（秒）")]
        public float beatInterval = 2.0f;

        [Tooltip("同步判定窗口（±秒）。0.35 是反复试过的手感上限。")]
        public float syncWindow = 0.35f;

        [Tooltip("需要连续同步几次")]
        public int requiredSyncs = 3;

        [Header("视觉")]
        [Tooltip("心瓣本体的 Renderer（搏动时会亮）")]
        public Renderer pulseRenderer;

        [Tooltip("搏动时的亮色")]
        public Color pulseColor = new Color(1f, 0.35f, 0.30f);

        [Tooltip("静止时的暗色")]
        public Color idleColor = new Color(0.32f, 0.14f, 0.14f);

        [Tooltip("预告提前量（秒）—— 搏动前多久开始发亮")]
        public float telegraphLead = 0.4f;

        [Header("联动")]
        [Tooltip("同步成功后解锁的门")]
        public DoorInteractable doorToUnlock;

        [Tooltip("同步成功后点亮的灯")]
        public Light[] lightsToEnable;

        [Header("提示")]
        public string promptIdle = "心瓣在缓慢搏动 —— 跟着它的节奏";
        public string promptDone = "心瓣已经平静了";

        // ────────── 运行时 ──────────

        /// <summary>已连续同步次数。</summary>
        public int SyncCount { get; private set; }

        /// <summary>是否已完成。</summary>
        public bool IsSolved { get; private set; }

        private float _phase;          // 当前节奏相位 0~beatInterval
        private float _pulseGlow;      // 0~1，视觉发光强度
        private MaterialPropertyBlock _mpb;

        /// <summary>完成事件。</summary>
        public static event System.Action OnPulseSolved;

        /// <summary>当前距离下一次搏动还有多久（秒）。</summary>
        public float TimeToNextBeat =>
            IsSolved ? 999f : Mathf.Max(0f, beatInterval - _phase);

        protected override string DefaultPrompt =>
            IsSolved ? promptDone : promptIdle;

        protected override void Awake()
        {
            base.Awake();
            _mpb = new MaterialPropertyBlock();
        }

        private void Start()
        {
            ApplyColor(idleColor);
        }

        private void Update()
        {
            if (IsSolved) return;

            _phase += Time.deltaTime;
            if (_phase >= beatInterval) _phase -= beatInterval;

            // 视觉：临近搏动时逐渐发亮，搏动后快速回落
            float toBeat = beatInterval - _phase;
            if (toBeat <= telegraphLead)
            {
                // 预告期：渐亮
                float t = 1f - (toBeat / Mathf.Max(0.01f, telegraphLead));
                _pulseGlow = Mathf.Lerp(0f, 1f, t);
            }
            else
            {
                // 回落
                _pulseGlow = Mathf.MoveTowards(_pulseGlow, 0f, Time.deltaTime * 3f);
            }

            ApplyColor(Color.Lerp(idleColor, pulseColor, _pulseGlow * 0.9f));
        }

        public override void OnInteract(GameObject interactor)
        {
            if (IsSolved) return;

            // 判定：距离最近一次搏动（相位 0 或 beatInterval）有多近
            float toBeat = TimeToNextBeat;
            float sinceBeat = beatInterval - toBeat;

            float distanceToBeat = Mathf.Min(toBeat, sinceBeat);

            if (distanceToBeat > syncWindow)
            {
                // 没对上
                SyncCount = 0;
                if (AbyssEcho.Audio.AudioManager.Instance != null)
                    AbyssEcho.Audio.AudioManager.Instance.PlayError();

                Debug.Log($"[Pulse] 没跟上（偏差 {distanceToBeat:F2}s > 窗口 {syncWindow}s），计数清零");
                return;
            }

            // 对上了
            SyncCount++;
            if (AbyssEcho.Audio.AudioManager.Instance != null)
                AbyssEcho.Audio.AudioManager.Instance.PlayUiBlip();

            // 同步成功时恐惧略升（身体感）
            if (AbyssEcho.Horror.FearSystem.Instance != null)
                AbyssEcho.Horror.FearSystem.Instance.AddFear(0.05f);

            Debug.Log($"[Pulse] 同步成功 {SyncCount}/{requiredSyncs}（偏差 {distanceToBeat:F2}s）");

            if (SyncCount >= requiredSyncs) Solve();
        }

        private void Solve()
        {
            IsSolved = true;

            if (doorToUnlock != null) doorToUnlock.locked = false;

            if (lightsToEnable != null)
                foreach (var l in lightsToEnable)
                    if (l != null) l.enabled = true;

            ApplyColor(new Color(0.55f, 0.60f, 0.62f));

            if (AbyssEcho.Audio.AudioManager.Instance != null)
                AbyssEcho.Audio.AudioManager.Instance.PlayStinger(0.5f);

            Debug.Log("[Pulse] 脉搏同步完成，门已解锁");
            OnPulseSolved?.Invoke();
        }

        private void ApplyColor(Color c)
        {
            if (pulseRenderer == null) return;

            pulseRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor("_BaseColor", c);
            _mpb.SetColor("_Color", c);
            if (pulseRenderer.sharedMaterial != null &&
                pulseRenderer.sharedMaterial.HasProperty("_EmissionColor"))
            {
                _mpb.SetColor("_EmissionColor", c * (0.4f + _pulseGlow * 1.6f));
            }
            pulseRenderer.SetPropertyBlock(_mpb);
        }

        /// <summary>重置（测试用）。</summary>
        public void ResetPuzzle()
        {
            IsSolved = false;
            SyncCount = 0;
            _phase = 0f;
            _pulseGlow = 0f;
            ApplyColor(idleColor);
        }

        /// <summary>测试用：把相位直接设到某个值。</summary>
        public void SetPhase(float p)
        {
            _phase = Mathf.Clamp(p, 0f, beatInterval);
        }
    }
}