using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AbyssEcho.Horror
{
    /// <summary>
    /// 恐惧视觉表现：根据恐惧值驱动后处理与画面效果。
    ///
    /// 表现手段（全部运行时修改 Volume，不需要美术资源）：
    ///   1. Vignette 强度随恐惧加深（视野收窄感）
    ///   2. 色差（ChromaticAberration）随恐惧增强（视觉失真）
    ///   3. FilmGrain 颗粒增强（噪点焦虑感）
    ///   4. 相机轻微抖动（高恐惧时）
    ///   5. 呼吸节奏（相机 FOV 极缓慢起伏）
    ///
    /// 为什么不用全屏 shader：
    ///   S3B 已经踩过自定义全屏 Pass 的坑（会把画面盖成纯色）。
    ///   这里全部复用 URP 内置 Volume 组件，稳定可靠。
    /// </summary>
    public class FearVisuals : MonoBehaviour
    {
        [Header("引用")]
        public FearSystem fear;
        public Camera targetCamera;

        [Header("Vignette 暗角")]
        public float vignetteCalm = 0.22f;
        public float vignettePanic = 0.62f;

        [Header("色差")]
        public float aberrationCalm = 0.05f;
        public float aberrationPanic = 0.35f;

        [Header("颗粒")]
        public float grainCalm = 0.10f;
        public float grainPanic = 0.30f;

        [Header("相机抖动")]
        [Tooltip("高恐惧时的最大抖动幅度（度）")]
        public float maxShakeAngle = 1.2f;

        [Header("呼吸效果")]
        [Tooltip("FOV 起伏幅度（度）")]
        public float breathFovAmount = 0.55f;
        [Tooltip("呼吸频率（Hz）")]
        public float breathRate = 0.28f;

        private Vignette _vignette;
        private ChromaticAberration _aberration;
        private FilmGrain _grain;

        private float _baseFov;
        private Quaternion _baseRot;
        private bool _captured;

        private void Awake()
        {
            if (fear == null) fear = FindFirstObjectByType<FearSystem>();
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera == null) targetCamera = GetComponentInChildren<Camera>();

            if (targetCamera != null) _baseFov = targetCamera.fieldOfView;
        }

        private void Start()
        {
            ResolveVolumeComponents();
        }

        /// <summary>从场景 Volume 中找到需要的后处理组件（可能延迟初始化）。</summary>
        private void ResolveVolumeComponents()
        {
            var volumes = FindObjectsByType<Volume>(FindObjectsSortMode.None);
            foreach (var v in volumes)
            {
                if (v.sharedProfile == null) continue;
                if (_vignette == null && v.sharedProfile.TryGet(out _vignette)) { }
                if (_aberration == null && v.sharedProfile.TryGet(out _aberration)) { }
                if (_grain == null && v.sharedProfile.TryGet(out _grain)) { }
            }

            if (_vignette == null) Debug.LogWarning("[FearVisuals] 未找到 Vignette 组件，暗角效果不可用");
            if (_aberration == null) Debug.LogWarning("[FearVisuals] 未找到 ChromaticAberration 组件");
            if (_grain == null) Debug.LogWarning("[FearVisuals] 未找到 FilmGrain 组件");
        }

        private void LateUpdate()
        {
            if (fear == null) return;
            float f = Mathf.Clamp01(fear.currentFear);

            // ① 后处理强度
            if (_vignette != null)
                _vignette.intensity.Override(Mathf.Lerp(vignetteCalm, vignettePanic, f));

            if (_aberration != null)
                _aberration.intensity.Override(Mathf.Lerp(aberrationCalm, aberrationPanic, f));

            if (_grain != null)
                _grain.intensity.Override(Mathf.Lerp(grainCalm, grainPanic, f));

            if (targetCamera == null) return;

            // ② 呼吸：FOV 缓慢起伏，恐惧越高幅度越大
            float breathAmp = breathFovAmount * Mathf.Lerp(0.35f, 1.6f, f);
            float breath = Mathf.Sin(Time.time * Mathf.PI * 2f * breathRate) * breathAmp * 0.5f;
            targetCamera.fieldOfView = _baseFov + breath;

            // ③ 抖动：只在恐惧较高时出现，且用 Perlin 噪声避免机械感
            if (f > 0.5f)
            {
                float shakeAmp = maxShakeAngle * Mathf.InverseLerp(0.5f, 1f, f);
                float t = Time.time * 6f;
                float ox = (Mathf.PerlinNoise(t, 0f) - 0.5f) * 2f;
                float oy = (Mathf.PerlinNoise(0f, t) - 0.5f) * 2f;

                targetCamera.transform.localRotation = Quaternion.Euler(oy * shakeAmp, ox * shakeAmp, 0f);
            }
        }
    }
}