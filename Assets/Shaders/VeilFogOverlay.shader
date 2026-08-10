// Veil Fog Overlay — URP Unlit
// Samples dynamic FoW R8 texture (Bilinear) + scrolling cloud noise over shroud.
Shader "VeilWar/FogOverlay"
{
    Properties
    {
        [MainTexture] _FogTex ("Fog Mask (R = visibility)", 2D) = "black" {}
        _CloudTex ("Cloud Noise", 2D) = "gray" {}
        _CloudTint ("Cloud Tint", Color) = (0.04, 0.08, 0.1, 1)
        _DeepShroud ("Deep Shroud", Color) = (0.01, 0.02, 0.03, 1)
        _EdgeGlow ("Edge Mist Glow", Color) = (0.2, 0.45, 0.48, 0.35)
        _CloudScale ("Cloud Scale", Float) = 2.5
        _CloudSpeed ("Cloud Speed", Vector) = (0.03, 0.02, -0.015, 0.025)
        _NoiseStrength ("Noise Strength", Range(0, 1)) = 0.55
        _ShroudSoftness ("Shroud Softness", Range(0.01, 0.5)) = 0.18
        _ExploredGrey ("Explored Grey", Range(0, 1)) = 0.28
        _Opacity ("Overlay Opacity", Range(0, 1)) = 0.92
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "VeilFogOverlay"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_FogTex);
            SAMPLER(sampler_FogTex);
            TEXTURE2D(_CloudTex);
            SAMPLER(sampler_CloudTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _FogTex_ST;
                float4 _CloudTex_ST;
                float4 _CloudTint;
                float4 _DeepShroud;
                float4 _EdgeGlow;
                float _CloudScale;
                float4 _CloudSpeed;
                float _NoiseStrength;
                float _ShroudSoftness;
                float _ExploredGrey;
                float _Opacity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 cloudUvA : TEXCOORD1;
                float2 cloudUvB : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _FogTex);

                float2 cloudBase = input.uv * _CloudScale;
                float t = _Time.y;
                output.cloudUvA = cloudBase + t * _CloudSpeed.xy;
                output.cloudUvB = cloudBase * 1.37 + t * _CloudSpeed.zw;
                return output;
            }

            // Custom Function–compatible core (also in VeilFog_CustomFunction.hlsl)
            void VeilFog_float(
                float2 FogUV,
                float2 CloudUVA,
                float2 CloudUVB,
                float Visibility,
                out float3 Color,
                out float Alpha)
            {
                float n0 = SAMPLE_TEXTURE2D(_CloudTex, sampler_CloudTex, CloudUVA).r;
                float n1 = SAMPLE_TEXTURE2D(_CloudTex, sampler_CloudTex, CloudUVB).g;
                float noise = saturate(n0 * 0.65 + n1 * 0.45);

                // 1 = clear vision, 0 = full shroud. Soft edge via smoothstep band.
                float clear = smoothstep(0.5 - _ShroudSoftness, 0.5 + _ShroudSoftness, Visibility);
                float explored = smoothstep(0.15, 0.45, Visibility);

                float shroudAmount = 1.0 - clear;
                float3 shroudCol = lerp(_DeepShroud.rgb, _CloudTint.rgb, noise * _NoiseStrength);
                // Absolute black wells where visibility ~ 0
                shroudCol = lerp(_DeepShroud.rgb * 0.2, shroudCol, saturate(noise + Visibility));

                float3 edge = _EdgeGlow.rgb * (1.0 - clear) * explored * (0.35 + noise * 0.65);
                Color = shroudCol + edge;

                float exploredVeil = (1.0 - clear) * explored * _ExploredGrey;
                Alpha = saturate(shroudAmount * _Opacity + exploredVeil * 0.35);
                // Fully clear => invisible overlay
                Alpha *= shroudAmount;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Bilinear sampling comes from FogTex.filterMode = Bilinear on the Texture2D.
                float visibility = SAMPLE_TEXTURE2D(_FogTex, sampler_FogTex, input.uv).r;

                float3 color;
                float alpha;
                VeilFog_float(input.uv, input.cloudUvA, input.cloudUvB, visibility, color, alpha);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
