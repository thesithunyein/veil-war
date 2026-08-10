using UnityEngine;
using VeilWar.Fog;

namespace VeilWar.Presentation
{
    /// <summary>
    /// Spawns / scales a massive world-space fog quad and binds the VeilFog material
    /// to <see cref="FogOfWarManager.FogTexture"/> for any grid size.
    /// </summary>
    public sealed class FogWorldQuadController : MonoBehaviour
    {
        static readonly int FogTexId = Shader.PropertyToID("_FogTex");
        static readonly int CloudTexId = Shader.PropertyToID("_CloudTex");
        static readonly int CloudSpeedId = Shader.PropertyToID("_CloudSpeed");

        [Header("Refs")]
        [SerializeField] FogOfWarManager fog;
        [SerializeField] Material fogMaterialTemplate;
        [SerializeField] Transform boardCenterOverride;

        [Header("Quad")]
        [SerializeField] float heightOffset = 0.08f;
        [SerializeField] float sizePadding = 1.08f;
        [SerializeField] bool createQuadIfMissing = true;
        [SerializeField] MeshRenderer fogRenderer;

        [Header("Cloud noise")]
        [SerializeField] Texture2D cloudNoiseOverride;
        [SerializeField] bool generateRuntimeNoise = true;
        [SerializeField] int noiseResolution = 256;

        Material _instance;
        Texture2D _runtimeNoise;

        public Material LiveMaterial => _instance;
        public MeshRenderer FogRenderer => fogRenderer;

        void Awake()
        {
            if (fog == null) fog = FogOfWarManager.Instance;
            EnsureMaterial();
            EnsureQuad();
            EnsureNoise();
            ApplyScaleAndBind();
        }

        void OnEnable()
        {
            if (fog == null) fog = FogOfWarManager.Instance;
            if (fog != null) fog.FogUpdated += OnFogUpdated;
            OnFogUpdated();
        }

        void OnDisable()
        {
            if (fog != null) fog.FogUpdated -= OnFogUpdated;
        }

        void OnDestroy()
        {
            if (_instance != null) Destroy(_instance);
            if (_runtimeNoise != null) Destroy(_runtimeNoise);
        }

        void OnFogUpdated()
        {
            if (fog == null || _instance == null) return;
            var tex = fog.FogTexture;
            if (tex == null) return;
            tex.filterMode = FilterMode.Bilinear;
            _instance.SetTexture(FogTexId, tex);
            fog.BindFogPlane(fogRenderer);
            ApplyScaleAndBind();
        }

        public void ApplyScaleAndBind()
        {
            if (fog == null || fogRenderer == null) return;

            var extent = fog.BoardWorldExtent * sizePadding;
            var center = boardCenterOverride != null
                ? boardCenterOverride.position
                : fog.BoardCenterWorld;

            var t = fogRenderer.transform;
            t.position = new Vector3(center.x, heightOffset, center.z);
            t.rotation = Quaternion.Euler(90f, 0f, 0f);
            // Unity quad is 1×1 in local XY; after 90° X rot it covers XZ.
            t.localScale = new Vector3(extent, extent, 1f);

            if (_instance != null)
            {
                fogRenderer.sharedMaterial = _instance;
                if (fog.FogTexture != null)
                    _instance.SetTexture(FogTexId, fog.FogTexture);
            }
        }

        void EnsureMaterial()
        {
            if (_instance != null) return;

            if (fogMaterialTemplate != null)
            {
                _instance = new Material(fogMaterialTemplate) { name = "VeilFogOverlay_Runtime" };
                return;
            }

            var shader = Shader.Find("VeilWar/FogOverlay");
            if (shader == null)
            {
                Debug.LogError("[VeilWar] Shader VeilWar/FogOverlay not found. Import Assets/Shaders/VeilFogOverlay.shader");
                return;
            }

            _instance = new Material(shader) { name = "VeilFogOverlay_Runtime" };
            _instance.SetColor("_CloudTint", new Color(0.05f, 0.1f, 0.12f, 1f));
            _instance.SetColor("_DeepShroud", new Color(0.01f, 0.015f, 0.02f, 1f));
            _instance.SetColor("_EdgeGlow", new Color(0.22f, 0.48f, 0.5f, 0.35f));
            _instance.SetVector(CloudSpeedId, new Vector4(0.03f, 0.02f, -0.015f, 0.025f));
        }

        void EnsureQuad()
        {
            if (fogRenderer != null) return;
            if (!createQuadIfMissing) return;

            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "FogWorldQuad";
            go.transform.SetParent(transform, false);
            Object.Destroy(go.GetComponent<Collider>());
            fogRenderer = go.GetComponent<MeshRenderer>();
            fogRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            fogRenderer.receiveShadows = false;
        }

        void EnsureNoise()
        {
            if (_instance == null) return;

            if (cloudNoiseOverride != null)
            {
                _instance.SetTexture(CloudTexId, cloudNoiseOverride);
                return;
            }

            if (!generateRuntimeNoise) return;
            if (_runtimeNoise == null)
                _runtimeNoise = GenerateCloudNoise(noiseResolution);
            _instance.SetTexture(CloudTexId, _runtimeNoise);
        }

        static Texture2D GenerateCloudNoise(int res)
        {
            res = Mathf.ClosestPowerOfTwo(Mathf.Clamp(res, 64, 512));
            var tex = new Texture2D(res, res, TextureFormat.RGBA32, false, true)
            {
                name = "VeilCloudNoise",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color32[res * res];
            for (var y = 0; y < res; y++)
            for (var x = 0; x < res; x++)
            {
                var u = x / (float)res;
                var v = y / (float)res;
                float n = 0f;
                float amp = 0.55f;
                float freq = 1f;
                for (var o = 0; o < 4; o++)
                {
                    n += amp * HashNoise(u * freq * 4f, v * freq * 4f);
                    amp *= 0.5f;
                    freq *= 2.03f;
                }

                n = Mathf.Clamp01(n);
                var b = (byte)Mathf.RoundToInt(n * 255f);
                pixels[y * res + x] = new Color32(b, b, b, 255);
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            return tex;
        }

        static float HashNoise(float x, float y)
        {
            var ix = Mathf.Floor(x);
            var iy = Mathf.Floor(y);
            var fx = x - ix;
            var fy = y - iy;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);

            float Hash(float a, float b)
            {
                var n = Mathf.Sin(a * 127.1f + b * 311.7f) * 43758.5453f;
                return n - Mathf.Floor(n);
            }

            var a = Hash(ix, iy);
            var b = Hash(ix + 1f, iy);
            var c = Hash(ix, iy + 1f);
            var d = Hash(ix + 1f, iy + 1f);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }
    }
}
