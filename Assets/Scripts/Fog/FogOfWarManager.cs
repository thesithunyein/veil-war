using System;
using UnityEngine;
using VeilWar.Core;

namespace VeilWar.Fog
{
    /// <summary>
    /// Runtime fog authority for the local observer.
    /// Logical grid stays 5×5/6×6; GPU texture is upsampled (Bilinear) for smooth shroud edges.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class FogOfWarManager : MonoBehaviour
    {
        public static FogOfWarManager Instance { get; private set; }

        [Header("Config")]
        [SerializeField] GameConfig config;
        [SerializeField, Range(5, 6)] int fallbackGridSize = 5;
        [SerializeField] float visionRadiusCells = 1.35f;
        [SerializeField] bool persistExplored = true;

        [Header("GPU fog map")]
        [SerializeField, Range(32, 512)] int textureResolution = 128;
        [SerializeField] FilterMode fogFilterMode = FilterMode.Bilinear;

        [Header("Optional legacy bind")]
        [SerializeField] Renderer fogPlaneRenderer;
        [SerializeField] string fogTextureProperty = "_FogTex";

        Texture2D _fogTexture;
        Color32[] _pixels;
        float[] _vision;
        float[] _explored;
        int _size;
        bool _dirty;
        bool _initialized;
        MaterialPropertyBlock _block;

        public bool IsReady => _initialized && _fogTexture != null && _vision != null;
        public int Size => _size;
        public int TextureResolution => _fogTexture != null ? _fogTexture.width : textureResolution;
        public Texture2D FogTexture => _fogTexture;
        public GameConfig Config => config;
        public float VisionRadiusCells => visionRadiusCells;

        public event Action FogUpdated;
        public event Action<GridCoord, bool> CellVisionChanged;

        public float BoardWorldExtent
        {
            get
            {
                if (!_initialized) return fallbackGridSize;
                if (config == null) return _size;
                return _size * config.CellStride + config.CellWorldSize * 0.25f;
            }
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[VeilWar] Duplicate FogOfWarManager — destroying extra.");
                Destroy(gameObject);
                return;
            }

            Instance = this;
            _block = new MaterialPropertyBlock();
            Initialize(config != null ? config.GridSize : fallbackGridSize);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            ReleaseTexture();
        }

        void LateUpdate()
        {
            if (_dirty && IsReady) CommitTexture();
        }

        public void Initialize(int size)
        {
            _size = Mathf.Clamp(size, 5, 6);
            var count = _size * _size;
            _vision = new float[count];
            _explored = new float[count];

            var res = Mathf.ClosestPowerOfTwo(Mathf.Clamp(textureResolution, 32, 512));
            textureResolution = res;
            if (_pixels == null || _pixels.Length != res * res)
                _pixels = new Color32[res * res];

            ReleaseTexture();
            _fogTexture = new Texture2D(res, res, TextureFormat.R8, false, true)
            {
                name = "VeilFogMap_HiRes",
                filterMode = fogFilterMode,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 0
            };

            Array.Clear(_vision, 0, _vision.Length);
            Array.Clear(_explored, 0, _explored.Length);
            _initialized = true;

            // Commit immediately so consumers never sample an uninitialized GPU texture.
            CommitTexture();
        }

        public void ClearVision(bool keepExplored)
        {
            if (!IsReady) return;
            Array.Clear(_vision, 0, _vision.Length);
            if (!keepExplored) Array.Clear(_explored, 0, _explored.Length);
            MarkAllDirty();
        }

        public void ApplyDecryptedVision(GridCoord center, float radiusCells, string sourceId = null)
        {
            if (!IsReady) return;
            if (!InBounds(center)) return;

            var radius = radiusCells > 0f ? radiusCells : visionRadiusCells;
            var r2 = radius * radius;
            var changed = false;

            for (var y = 0; y < _size; y++)
            for (var x = 0; x < _size; x++)
            {
                var dx = x - center.X;
                var dy = y - center.Y;
                var dist2 = dx * dx + dy * dy;
                if (dist2 > r2) continue;

                var idx = Index(x, y);
                var falloff = 1f - Mathf.Sqrt(dist2) / Mathf.Max(0.001f, radius);
                falloff = falloff * falloff * (3f - 2f * falloff);
                var next = Mathf.Max(_vision[idx], falloff);
                if (!Mathf.Approximately(next, _vision[idx]))
                {
                    var wasVisible = IsVisibleValue(SampleDisplay(idx));
                    _vision[idx] = next;
                    if (persistExplored) _explored[idx] = Mathf.Max(_explored[idx], next);
                    var nowVisible = IsVisibleValue(SampleDisplay(idx));
                    if (wasVisible != nowVisible)
                        CellVisionChanged?.Invoke(new GridCoord(x, y), nowVisible);
                    changed = true;
                }
            }

            if (changed)
            {
                _dirty = true;
#if UNITY_EDITOR
                if (!string.IsNullOrEmpty(sourceId))
                    Debug.Log($"[FoW] Vision from {sourceId} @ {center} r={radius:0.00}");
#endif
            }
        }

        public void ClearEphemeralVision()
        {
            if (!IsReady) return;

            var any = false;
            for (var i = 0; i < _vision.Length; i++)
            {
                if (_vision[i] <= 0f) continue;
                var wasVisible = IsVisibleValue(SampleDisplay(i));
                _vision[i] = 0f;
                var nowVisible = IsVisibleValue(SampleDisplay(i));
                if (wasVisible != nowVisible)
                    CellVisionChanged?.Invoke(CoordFromIndex(i), nowVisible);
                any = true;
            }

            if (any) _dirty = true;
        }

        public void SetCellRevealed(GridCoord coord, bool revealed)
        {
            if (!IsReady || !InBounds(coord)) return;
            var idx = Index(coord.X, coord.Y);
            var wasVisible = IsVisibleValue(SampleDisplay(idx));
            _vision[idx] = revealed ? 1f : 0f;
            if (revealed && persistExplored) _explored[idx] = 1f;
            if (!revealed && !persistExplored) _explored[idx] = 0f;
            var nowVisible = IsVisibleValue(SampleDisplay(idx));
            if (wasVisible != nowVisible) CellVisionChanged?.Invoke(coord, nowVisible);
            _dirty = true;
        }

        public bool IsVisible(GridCoord coord)
        {
            if (!IsReady || !InBounds(coord)) return false;
            return IsVisibleValue(SampleDisplay(Index(coord.X, coord.Y)));
        }

        public bool IsVisibleWorld(Vector3 worldPosition)
        {
            if (!TryWorldToCoord(worldPosition, out var coord)) return false;
            return IsVisible(coord);
        }

        public float SampleVisibility(GridCoord coord)
        {
            if (!IsReady || !InBounds(coord)) return 0f;
            return SampleDisplay(Index(coord.X, coord.Y));
        }

        public float SampleVisibilityWorld(Vector3 worldPosition)
        {
            if (!TryWorldToCoord(worldPosition, out var coord)) return 0f;
            return SampleVisibility(coord);
        }

        public bool TryWorldToCoord(Vector3 world, out GridCoord coord)
        {
            coord = default;
            if (!_initialized) return false;

            if (config == null)
            {
                var x = Mathf.RoundToInt(world.x);
                var y = Mathf.RoundToInt(world.z);
                coord = new GridCoord(x, y);
                return InBounds(coord);
            }

            var stride = config.CellStride;
            var origin = -(config.GridSize - 1) * stride * 0.5f;
            var gx = Mathf.RoundToInt((world.x - origin) / stride);
            var gy = Mathf.RoundToInt((world.z - origin) / stride);
            coord = new GridCoord(gx, gy);
            return config.InBounds(coord);
        }

        public Vector3 CoordToWorld(GridCoord coord)
        {
            if (config != null) return config.CellToWorld(coord);
            return new Vector3(coord.X, 0f, coord.Y);
        }

        public Vector3 BoardCenterWorld =>
            config != null && _initialized
                ? config.CellToWorld(new GridCoord((_size - 1) / 2, (_size - 1) / 2))
                : Vector3.zero;

        float SampleDisplay(int idx)
        {
            var v = _vision[idx];
            if (v > 0.001f) return v;
            if (persistExplored && _explored[idx] > 0.001f) return 0.35f * _explored[idx];
            return 0f;
        }

        float SampleDisplayBilinear(float u, float v)
        {
            var fx = u * (_size - 1);
            var fy = v * (_size - 1);
            var x0 = Mathf.Clamp(Mathf.FloorToInt(fx), 0, _size - 1);
            var y0 = Mathf.Clamp(Mathf.FloorToInt(fy), 0, _size - 1);
            var x1 = Mathf.Min(x0 + 1, _size - 1);
            var y1 = Mathf.Min(y0 + 1, _size - 1);
            var tx = fx - x0;
            var ty = fy - y0;

            var a = SampleDisplay(Index(x0, y0));
            var b = SampleDisplay(Index(x1, y0));
            var c = SampleDisplay(Index(x0, y1));
            var d = SampleDisplay(Index(x1, y1));
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        static bool IsVisibleValue(float sample) => sample >= 0.5f;

        void CommitTexture()
        {
            if (_fogTexture == null || _pixels == null || _vision == null) return;

            var res = _fogTexture.width;
            // Bounded O(res²). res is clamped to ≤512 → max 262k texels, once per dirty frame.
            for (var y = 0; y < res; y++)
            for (var x = 0; x < res; x++)
            {
                var u = (x + 0.5f) / res;
                var v = (y + 0.5f) / res;
                var sample = SampleDisplayBilinear(u, v);
                var b = (byte)Mathf.Clamp(Mathf.RoundToInt(sample * 255f), 0, 255);
                _pixels[y * res + x] = new Color32(b, b, b, 255);
            }

            _fogTexture.filterMode = fogFilterMode;
            _fogTexture.SetPixels32(_pixels);
            _fogTexture.Apply(false, false);
            PushTextureToRenderer();
            _dirty = false;
            FogUpdated?.Invoke();
        }

        public void PushTextureToRenderer()
        {
            if (fogPlaneRenderer == null || _fogTexture == null) return;
            _block ??= new MaterialPropertyBlock();
            fogPlaneRenderer.GetPropertyBlock(_block);
            _block.SetTexture(fogTextureProperty, _fogTexture);
            _block.SetTexture("_FogTex", _fogTexture);
            _block.SetTexture("_BaseMap", _fogTexture);
            _block.SetTexture("_MainTex", _fogTexture);
            fogPlaneRenderer.SetPropertyBlock(_block);
        }

        public void BindFogPlane(Renderer renderer)
        {
            fogPlaneRenderer = renderer;
            PushTextureToRenderer();
        }

        void MarkAllDirty()
        {
            if (!IsReady) return;
            for (var i = 0; i < _vision.Length; i++)
                CellVisionChanged?.Invoke(CoordFromIndex(i), IsVisibleValue(SampleDisplay(i)));
            _dirty = true;
        }

        void ReleaseTexture()
        {
            if (_fogTexture == null) return;
            Destroy(_fogTexture);
            _fogTexture = null;
        }

        bool InBounds(GridCoord c) => c.X >= 0 && c.Y >= 0 && c.X < _size && c.Y < _size;
        int Index(int x, int y) => y * _size + x;

        GridCoord CoordFromIndex(int idx)
        {
            var y = idx / _size;
            var x = idx - y * _size;
            return new GridCoord(x, y);
        }
    }
}
