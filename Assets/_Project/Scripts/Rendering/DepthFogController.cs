using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AbyssEcho.Rendering
{
    /// <summary>
    /// 深海深度雾：通过 URP FullScreenPassRendererFeature 注入的自定义雾效。
    ///
    /// 为什么自己实现：
    ///   URP 15 的 Volume 中没有公开的 Fog 组件，RenderSettings.fog 在 URP 中无效。
    ///   （这是 S3B 阶段实测确认的：PackageCache 中 Fog 类为 internal）
    ///
    /// 工作原理：
    ///   全屏 Pass 采样深度缓冲 → 重建世界坐标 → 按距离/高度计算雾浓度 → 与原色混合。
    ///
    /// 使用：
    ///   该 Feature 需添加到 URP_Renderer.asset 的 Renderer Features 列表中（由 MaterialLibraryBuilder 自动完成）。
    /// </summary>
    [DisallowMultipleComponent]
    public class DepthFogController : MonoBehaviour
    {
        [Header("雾颜色（深海冷蓝灰）")]
        public Color fogColor = new Color(0.16f, 0.20f, 0.26f);

        [Header("距离雾")]
        [Tooltip("雾密度，越大越浓（建议 0.02~0.08）")]
        [Range(0f, 0.2f)] public float fogDensity = 0.018f;
        [Tooltip("雾开始出现的距离（米），近处保持清晰")]
        public float fogStart = 3f;
        [Tooltip("衰减指数：1 = 线性增长，2 = 二次增长（更浓）")]
        [Range(0.5f, 4f)] public float fogFalloff = 1.6f;

        [Header("高度雾（贴地水汽）")]
        [Tooltip("强度，0 = 关闭")]
        [Range(0f, 2f)] public float heightFogStrength = 0.35f;
        [Tooltip("高度雾基准高度（米）")]
        public float heightFogBase = 0f;
        [Tooltip("高度衰减：越大，雾只在越低处越浓")]
        [Range(0.05f, 2f)] public float heightFogFalloff = 0.6f;

        [Header("目标材质")]
        [Tooltip("FullScreenPassRendererFeature 使用的材质（自动查找）")]
        public Material fogMaterial;

        private static readonly int FogColorId = Shader.PropertyToID("_FogColor");
        private static readonly int FogDensityId = Shader.PropertyToID("_FogDensity");
        private static readonly int FogStartId = Shader.PropertyToID("_FogStart");
        private static readonly int FogFalloffId = Shader.PropertyToID("_FogFalloff");
        private static readonly int HeightStrengthId = Shader.PropertyToID("_HeightFogStrength");
        private static readonly int HeightBaseId = Shader.PropertyToID("_HeightFogBase");
        private static readonly int HeightFalloffId = Shader.PropertyToID("_HeightFogFalloff");

        private void Awake()
        {
            Apply();
        }

        private void OnValidate()
        {
            Apply();
        }

        /// <summary>把 Inspector 参数写入材质。</summary>
        public void Apply()
        {
            if (fogMaterial == null) return;

            fogMaterial.SetColor(FogColorId, fogColor);
            fogMaterial.SetFloat(FogDensityId, fogDensity);
            fogMaterial.SetFloat(FogStartId, fogStart);
            fogMaterial.SetFloat(FogFalloffId, fogFalloff);
            fogMaterial.SetFloat(HeightStrengthId, heightFogStrength);
            fogMaterial.SetFloat(HeightBaseId, heightFogBase);
            fogMaterial.SetFloat(HeightFalloffId, heightFogFalloff);
        }
    }
}
