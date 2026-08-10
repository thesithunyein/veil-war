# Veil Fog — Shader Graph setup (URP)

Production path: use ready shader `VeilWar/FogOverlay` via `FogWorldQuadController`.  
Optional: rebuild as Shader Graph using the custom function below for art iteration.

## A) Instant (recommended for jam)

1. Create material **VeilFogOverlay** → shader `VeilWar/FogOverlay`.
2. Assign a tiling grayscale noise to **Cloud Noise** (or leave gray; controller can generate one).
3. Add empty GameObject → `FogWorldQuadController` → assign material + `FogOfWarManager`.
4. Play: FoW R8 texture is pushed every update; Bilinear filter is forced on.

## B) Shader Graph (custom node)

1. **Create → Shader Graph → URP → Unlit Shader Graph** named `VeilFogSG`.
2. Graph Inspector: **Surface = Transparent**, **Blending = Alpha**, **Cull = Off**, **ZWrite Off**.
3. Add properties:
   - `FogTex` (Texture2D)
   - `CloudTex` (Texture2D)
   - Colors: CloudTint, DeepShroud, EdgeGlow
   - Floats: Softness (0.18), Opacity (0.92), CloudScale (2.5), NoiseStrength (0.55)
   - Vector2: SpeedA, SpeedB
4. UV path:
   - `UV` → `Tiling And Offset` (CloudScale) → branch into two `Tiling And Offset` nodes driven by `Time` × SpeedA / SpeedB.
5. Fog sample:
   - `Sample Texture 2D` FogTex, **Filter = Bilinear**, **LOD = 0**, Mode = Clamp.  
   - Split → **R** = Visibility.
6. Cloud noise:
   - Sample CloudTex at UV-A and UV-B → lerp/add → Noise 0–1.
7. Custom Function node:
   - Type: **File**
   - Source: `Assets/Shaders/ShaderGraph/VeilFog_CustomFunction.hlsl`
   - Name: `VeilFogFromVisibility`
   - Inputs: Visibility, Noise, CloudTint, DeepShroud, EdgeGlow, Softness, Opacity
   - Outputs: Color (Vector3), Alpha (Float)
8. Connect Color → Base Color, Alpha → Alpha.
9. Save; create material; feed into `FogWorldQuadController`.

## C) Runtime property names expected by C#

| Property | Purpose |
|----------|---------|
| `_FogTex` | Dynamic FoW mask (R) |
| `_CloudTex` | Moving noise |
| `_CloudTint` / `_DeepShroud` / `_EdgeGlow` | Atmosphere |
| `_CloudScale` / `_CloudSpeed` | Scroll |
| `_NoiseStrength` / `_ShroudSoftness` / `_Opacity` | Feel |

`FogOfWarManager` builds a **128²+** R8 map with CPU bilinear upsample from the 5×5 logical grid, then sets `filterMode = Bilinear` so the shader’s `SAMPLE_TEXTURE2D` stays ultra-smooth on the world quad.
