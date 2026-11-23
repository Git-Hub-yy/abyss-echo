using System;
using AbyssEcho.Interaction;
using UnityEngine;

namespace AbyssEcho.Puzzle
{
    /// <summary>
    /// 抽水泵开关（第二章 D3 泵房）。
    ///
    /// 作用：抽干 D2 大厅的积水，露出地面——地面上刻着下一阶段需要的线索。
    ///
    /// 设计意图：
    ///   · 给玩家一个"改造环境"的动作，比单纯开门更有参与感
    ///   · 抽水后场景发生**永久性视觉变化**，强化"世界会响应我"的感受
    ///   · 同时降低 D2 的行走噪音（水没了，脚步变轻）
    ///
    /// 兼容性：完全复用 InteractableBase，与第一章的 PowerRestorePanel 同构。
    /// </summary>
    public class WaterPumpSwitch : InteractableBase
    {
        [Header("抽水设置")]
        [Tooltip("抽水需要多久（秒）")]
        public float pumpDuration = 3.5f;

        [Tooltip("水位从 100% 降到 0% 是否平滑")]
        public bool smoothDrain = true;

        [Header("目标对象")]
        [Tooltip("要移除的水面对象（抽水后隐藏）")]
        public GameObject waterSurface;

        [Tooltip("抽水过程中播放的声音源（可选）")]
        public AudioSource pumpAudio;

        [Header("联动")]
        [Tooltip("抽水后点亮的指示灯")]
        public Renderer[] indicatorRenderers;
        public Color indicatorOffColor = new Color(0.9f, 0.25f, 0.15f);
        public Color indicatorOnColor = new Color(0.2f, 1f, 0.5f);

        [Tooltip("抽水后解锁的门")]
        public DoorInteractable[] doorsToUnlock;

        [Header("提示文案")]
        public string promptIdle = "启动抽水泵";
        public string promptRunning = "抽水中…";
        public string promptDone = "抽水泵已启动";

        /// <summary>是否已经抽干。</summary>
        public bool IsDrained { get; private set; }

        /// <summary>抽水进度 0-1。</summary>
        public float DrainProgress { get; private set; }

        /// <summary>抽水完成事件。</summary>
        public static event Action OnWaterDrained;

        private float _timer;
        private bool _running;

        protected override string DefaultPrompt => promptIdle;

        public override string GetPrompt()
        {
            if (IsDrained) return promptDone;
            if (_running) return promptRunning;
            return base.GetPrompt();
        }

        public override void OnInteract(GameObject interactor)
        {
            if (IsDrained || _running) return;

            _running = true;
            _timer = 0f;

            if (pumpAudio != null) pumpAudio.Play();

            if (AbyssEcho.Audio.AudioManager.Instance != null)
                AbyssEcho.Audio.AudioManager.Instance.PlayUiBlip();

            // 启动时恐惧略升（机器轰鸣 + 不知道会发生什么）
            if (AbyssEcho.Horror.FearSystem.Instance != null)
                AbyssEcho.Horror.FearSystem.Instance.AddFear(0.06f);

            UpdateIndicator(false);
            Debug.Log("[WaterPump] 开始抽水");
        }

        private void Update()
        {
            if (!_running || IsDrained) return;

            _timer += Time.deltaTime;
            DrainProgress = Mathf.Clamp01(_timer / Mathf.Max(0.1f, pumpDuration));

            // 水面高度：从原位置向下沉
            if (waterSurface != null)
            {
                if (smoothDrain)
                {
                    var t = waterSurface.transform;
                    // 每秒下沉约 0.2 米（3.5 秒共 0.7 米，覆盖过膝水深）
                    var p = t.position;
                    p.y -= Time.deltaTime * 0.2f;
                    t.position = p;
                }
            }

            if (DrainProgress >= 1f) Finish();
        }

        private void Finish()
        {
            _running = false;
            IsDrained = true;

            if (waterSurface != null) waterSurface.SetActive(false);
            if (pumpAudio != null) pumpAudio.Stop();

            UpdateIndicator(true);

            if (doorsToUnlock != null)
                foreach (var d in doorsToUnlock)
                    if (d != null) d.locked = false;

            OnWaterDrained?.Invoke();

            Debug.Log("[WaterPump] 抽水完成");
        }

        private void UpdateIndicator(bool on)
        {
            if (indicatorRenderers == null) return;

            var c = on ? indicatorOnColor : indicatorOffColor;
            foreach (var r in indicatorRenderers)
            {
                if (r == null) continue;

                // 用材质实例，避免修改共享材质影响其它物体
                var mat = r.material;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
                mat.color = c;
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", c * 1.8f);
                }
            }
        }

        /// <summary>重置（调试用）。</summary>
        public void ResetPump()
        {
            IsDrained = false;
            _running = false;
            DrainProgress = 0f;
            if (waterSurface != null) waterSurface.SetActive(true);
            UpdateIndicator(false);
        }
    }
}