// ABYSS ECHO - Screen Space Depth Fog (S3B)
//
// Why custom:
//   URP 15 has no public Fog Volume component (Fog is internal),
//   and RenderSettings.fog is ignored by URP.
//   So we inject a fullscreen pass via FullScreenPassRendererFeature.
//
// Pipeline:
//   Sample depth -> reconstruct world position -> distance/height based
//   exponential fog -> blend with scene color.

Shader "ABYSS ECHO/DepthFog"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "ABYSS_DEPTH_FOG"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _FogColor;
            float  _FogDensity;
            float  _FogStart;
            float  _FogFalloff;
            float  _HeightFogStrength;
            float  _HeightFogBase;
            float  _HeightFogFalloff;

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;

                float4 sceneColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                float rawDepth = SampleSceneDepth(uv);
                if (rawDepth <= 0.0001)
                {
                    return sceneColor;
                }

                float3 worldPos = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
                float dist = distance(worldPos, _WorldSpaceCameraPos);

                // 防护：距离异常（NaN / 无穷 / 超出远平面）时不加雾，
                // 避免因为深度重建失败导致整屏被雾色覆盖（画面全黑）。
                if (!(dist > 0.0) || dist > 500.0)
                {
                    return sceneColor;
                }

                float fogAmount = saturate((dist - _FogStart) * _FogDensity);
                fogAmount = pow(fogAmount, _FogFalloff);
                fogAmount = saturate(fogAmount);

                // 上限保护：无论参数如何，雾最多覆盖 92%，始终保留 8% 原画面。
                // 实测踩坑：fogAmount 一旦达到 1.0，整个屏幕会变成纯雾色（看起来像全黑）。
                fogAmount = min(fogAmount, 0.92);

                if (_HeightFogStrength > 0.0001)
                {
                    float h = saturate((worldPos.y - _HeightFogBase) * _HeightFogFalloff);
                    float heightFactor = 1.0 - h;
                    float distFactor = saturate(dist / 20.0);
                    fogAmount = saturate(fogAmount + heightFactor * _HeightFogStrength * distFactor);
                    fogAmount = min(fogAmount, 0.92);
                }

                float3 finalColor = lerp(sceneColor.rgb, _FogColor.rgb, fogAmount);
                return float4(finalColor, sceneColor.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}