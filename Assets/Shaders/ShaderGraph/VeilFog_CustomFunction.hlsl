// Unity Shader Graph → Custom Function node
// File mode: path to this HLSL. Name: VeilFogSG
// Precision: Half. Check "Use Material Override Properties" as needed.
//
// Inputs:
//   FogUV            (Vector2)
//   CloudUVA         (Vector2)
//   CloudUVB         (Vector2)
//   Visibility       (Float)   // optional if you Sample Texture 2D outside and pass R
//   FogTex           (Texture2D) — OR sample outside and pass Visibility only
//   CloudTex         (Texture2D)
// Outputs:
//   Color (Vector3), Alpha (Float)

#ifndef VEIL_FOG_CUSTOM_INCLUDED
#define VEIL_FOG_CUSTOM_INCLUDED

// When used from Shader Graph Custom Function, declare textures as properties on the graph
// and bind them via the Custom Function's Texture2D inputs.

void VeilFogSG_float(
    UnityTexture2D FogTex,
    UnitySamplerState FogSampler,
    UnityTexture2D CloudTex,
    UnitySamplerState CloudSampler,
    float2 FogUV,
    float2 CloudUVA,
    float2 CloudUVB,
    float CloudTintR,
    float CloudTintG,
    float CloudTintB,
    float DeepR,
    float DeepG,
    float DeepB,
    float EdgeR,
    float EdgeG,
    float EdgeB,
    float NoiseStrength,
    float Softness,
    float Opacity,
    out float3 Color,
    out float Alpha)
{
    // Bilinear: set FogTex Filter Mode = Bilinear on the Texture2D asset / runtime Texture.
    float visibility = SAMPLE_TEXTURE2D(FogTex.tex, FogSampler.samplerstate, FogUV).r;

    float n0 = SAMPLE_TEXTURE2D(CloudTex.tex, CloudSampler.samplerstate, CloudUVA).r;
    float n1 = SAMPLE_TEXTURE2D(CloudTex.tex, CloudSampler.samplerstate, CloudUVB).g;
    float noise = saturate(n0 * 0.65 + n1 * 0.45);

    float clear = smoothstep(0.5 - Softness, 0.5 + Softness, visibility);
    float explored = smoothstep(0.15, 0.45, visibility);
    float shroudAmount = 1.0 - clear;

    float3 deep = float3(DeepR, DeepG, DeepB);
    float3 tint = float3(CloudTintR, CloudTintG, CloudTintB);
    float3 edge = float3(EdgeR, EdgeG, EdgeB);

    float3 shroudCol = lerp(deep, tint, noise * NoiseStrength);
    shroudCol = lerp(deep * 0.2, shroudCol, saturate(noise + visibility));
    Color = shroudCol + edge * (1.0 - clear) * explored * (0.35 + noise * 0.65);

    Alpha = saturate(shroudAmount * Opacity);
}

// Simpler overload: Visibility already sampled in graph (Sample Texture 2D → R).
void VeilFogFromVisibility_float(
    float Visibility,
    float Noise,
    float3 CloudTint,
    float3 DeepShroud,
    float3 EdgeGlow,
    float Softness,
    float Opacity,
    out float3 Color,
    out float Alpha)
{
    float clear = smoothstep(0.5 - Softness, 0.5 + Softness, Visibility);
    float explored = smoothstep(0.15, 0.45, Visibility);
    float shroudAmount = 1.0 - clear;

    float3 shroudCol = lerp(DeepShroud, CloudTint, Noise);
    shroudCol = lerp(DeepShroud * 0.2, shroudCol, saturate(Noise + Visibility));
    Color = shroudCol + EdgeGlow * (1.0 - clear) * explored * (0.35 + Noise * 0.65);
    Alpha = saturate(shroudAmount * Opacity);
}

#endif
