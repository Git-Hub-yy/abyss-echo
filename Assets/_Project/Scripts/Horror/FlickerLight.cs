using UnityEngine;

namespace AbyssEcho.Horror
{
    /// <summary>
    /// 灯光闪烁控制器：让指定光源产生"接触不良"式的闪烁。
    ///
    /// 三种模式（可在 Inspector 里切换）：
    ///   · Steady    稳定（不闪）
    ///   · Flicker   随机抖动（模拟接触不良，最常见）
    ///   · Dying     濒死（越来越暗，偶尔爆亮，最后熄灭）
    ///
    /// 为什么用协程而不是每帧随机：
    ///   每帧随机会产生"高频噪声"式的抖，看起来像 bug；
    ///   真实的日光灯闪烁是"亮一段时间 → 突然暗一下 → 恢复"，
    ///   所以用状态切换 + 随机时长更真实。
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class FlickerLight : MonoBehaviour
    {
        public enum Mode { Steady, Flicker, Dying }

        [Header("模式")]
        public Mode mode = Mode.Flicker;

        [Tooltip("正常亮度（会记录为基准值）")]
        public float baseIntensity = 2.5f;

        [Header("闪烁参数（Flicker / Dying）")]
        [Tooltip("两次闪烁之间的最短间隔")]
        public float minInterval = 1.2f;

        [Tooltip("两次闪烁之间的最长间隔")]
        public float maxInterval = 4.5f;

        [Tooltip("单次闪烁持续时间")]
        public float flickerDuration = 0.12f;

        [Tooltip("闪烁时的亮度比例（0 = 全灭）")]
        [Range(0f, 1f)] public float dimRatio = 0.12f;

        [Header("濒死模式")]
        [Tooltip("濒死模式下的总寿命（秒）")]
        public float dyingLifetime = 25f;

        [Header("恐惧联动")]
        [Tooltip("恐惧值越高，闪烁越频繁")]
        public bool fearReactive = true;

        private Light _light;
        private Coroutine _routine;
        private float _startTime;

        private void Awake()
        {
            _light = GetComponent<Light>();
            // 记录当前亮度作为基准（若 Inspector 未手动设置）
            if (baseIntensity <= 0f) baseIntensity = _light.intensity;
        }

        private void OnEnable()
        {
            _startTime = Time.time;
            _routine = StartCoroutine(Run());
        }

        private void OnDisable()
        {
            if (_routine != null) StopCoroutine(_routine);
            if (_light != null) _light.intensity = baseIntensity;
        }

        private System.Collections.IEnumerator Run()
        {
            // 等一小段随机时间再开始，避免所有灯同时闪（那看起来像渲染故障）
            yield return new WaitForSeconds(Random.Range(0f, 2.5f));

            while (true)
            {
                float interval = Random.Range(minInterval, maxInterval);

                // 恐惧越高，间隔越短（最多缩短到 35%）
                if (fearReactive && FearSystem.Instance != null)
                {
                    float f = FearSystem.Instance.currentFear;
                    interval *= Mathf.Lerp(1f, 0.35f, f);
                }

                yield return new WaitForSeconds(interval);

                switch (mode)
                {
                    case Mode.Steady:
                        break;

                    case Mode.Flicker:
                        yield return DoFlicker();
                        break;

                    case Mode.Dying:
                        yield return DoDying();
                        // 濒死模式只执行一次
                        yield break;
                }
            }
        }

        /// <summary>标准的"暗一下再恢复"，可能连续闪两次。</summary>
        private System.Collections.IEnumerator DoFlicker()
        {
            int bursts = Random.value < 0.25f ? 2 : 1;

            for (int b = 0; b < bursts; b++)
            {
                _light.intensity = baseIntensity * dimRatio;
                yield return new WaitForSeconds(Random.Range(0.03f, flickerDuration));

                // 恢复时偶尔会有"过冲"（比正常还亮一瞬），更像真实电路
                float over = Random.value < 0.2f ? Random.Range(1.05f, 1.25f) : 1f;
                _light.intensity = baseIntensity * over;
                yield return new WaitForSeconds(Random.Range(0.04f, 0.10f));

                if (b < bursts - 1)
                    yield return new WaitForSeconds(Random.Range(0.05f, 0.15f));
            }

            _light.intensity = baseIntensity;
        }

        /// <summary>越来越暗，最后熄灭（可能在熄灭前爆亮一次）。</summary>
        private System.Collections.IEnumerator DoDying()
        {
            float t = 0f;
            float fadeOut = Mathf.Min(dyingLifetime * 0.7f, 8f);

            while (t < fadeOut)
            {
                t += Time.deltaTime;
                float k = 1f - (t / fadeOut);
                _light.intensity = baseIntensity * Mathf.Max(0f, k);

                // 偶发爆亮
                if (Random.value < 0.012f)
                {
                    _light.intensity = baseIntensity * Random.Range(1.2f, 1.8f);
                    yield return new WaitForSeconds(0.06f);
                }

                yield return null;
            }

            // 最后阶段：完全熄灭
            _light.intensity = 0f;
            _light.enabled = false;
        }

        /// <summary>重置为稳定亮度（例如电力恢复后）。</summary>
        public void SetSteady()
        {
            mode = Mode.Steady;
            if (_light != null)
            {
                _light.enabled = true;
                _light.intensity = baseIntensity;
            }
        }
    }
}