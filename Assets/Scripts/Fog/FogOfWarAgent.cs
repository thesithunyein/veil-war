using UnityEngine;
using VeilWar.Core;
using VeilWar.Fog;

namespace VeilWar.Fog
{
    public enum FogAgentMode : byte
    {
        /// <summary>Fully hide MeshRenderers / Canvases while shrouded.</summary>
        Hide = 0,
        /// <summary>Keep meshes, lerp saturation toward grey while shrouded.</summary>
        Desaturate = 1,
        /// <summary>Hide only when fully shrouded; desaturate while explored-but-not-live.</summary>
        HideOrDesaturate = 2
    }

    /// <summary>
    /// Attach to enemy units/buildings. Samples <see cref="FogOfWarManager"/> each fog tick
    /// and toggles visuals (or desaturates) based on local grid visibility.
    /// </summary>
    public sealed class FogOfWarAgent : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        [Header("Identity")]
        [SerializeField] bool isFriendly;
        [SerializeField] bool alwaysVisibleIfFriendly = true;
        [SerializeField] FogAgentMode mode = FogAgentMode.HideOrDesaturate;

        [Header("Sampling")]
        [SerializeField] FogOfWarManager fog;
        [SerializeField] bool followTransform = true;
        [SerializeField] GridCoord manualCoord;
        [SerializeField] float visibleThreshold = 0.5f;
        [SerializeField] float exploredThreshold = 0.2f;

        [Header("Targets (auto-filled if empty)")]
        [SerializeField] MeshRenderer[] meshRenderers;
        [SerializeField] Canvas[] canvases;
        [SerializeField] Behaviour[] extraBehaviours;

        [Header("Desaturate")]
        [SerializeField] Color shroudTint = new(0.35f, 0.4f, 0.42f, 1f);
        [SerializeField] float colorLerpSpeed = 10f;

        MaterialPropertyBlock _block;
        Color[] _baseColors;
        bool _visible = true;
        float _vis01 = 1f;
        GridCoord _lastCoord;

        public bool IsCurrentlyVisible => _visible;
        public float Visibility01 => _vis01;
        public GridCoord GridPosition => followTransform ? WorldToCoord() : manualCoord;

        public void Configure(bool friendly, FogAgentMode agentMode = FogAgentMode.HideOrDesaturate)
        {
            isFriendly = friendly;
            mode = agentMode;
            alwaysVisibleIfFriendly = true;
            Refresh(force: true);
        }

        void Awake()
        {
            if (fog == null) fog = FogOfWarManager.Instance;
            if (meshRenderers == null || meshRenderers.Length == 0)
                meshRenderers = GetComponentsInChildren<MeshRenderer>(true);
            if (canvases == null || canvases.Length == 0)
                canvases = GetComponentsInChildren<Canvas>(true);

            CacheBaseColors();
            _block ??= new MaterialPropertyBlock();
        }

        void OnEnable()
        {
            if (fog == null) fog = FogOfWarManager.Instance;
            if (fog != null) fog.FogUpdated += OnFogUpdated;
            Refresh(force: true);
        }

        void OnDisable()
        {
            if (fog != null) fog.FogUpdated -= OnFogUpdated;
        }

        void LateUpdate()
        {
            if (!followTransform) return;
            var coord = WorldToCoord();
            if (coord.Equals(_lastCoord)) return;
            _lastCoord = coord;
            Refresh(force: false);
        }

        public void SetManualCoord(GridCoord coord)
        {
            followTransform = false;
            manualCoord = coord;
            transform.position = fog != null
                ? fog.CoordToWorld(coord) + Vector3.up * transform.position.y
                : new Vector3(coord.X, transform.position.y, coord.Y);
            Refresh(force: true);
        }

        public void TeleportToCoord(GridCoord coord)
        {
            followTransform = true;
            if (fog != null)
                transform.position = fog.CoordToWorld(coord) + Vector3.up * 0.55f;
            else
                transform.position = new Vector3(coord.X, 0.55f, coord.Y);
            _lastCoord = coord;
            Refresh(force: true);
        }

        void OnFogUpdated() => Refresh(force: false);

        void Refresh(bool force)
        {
            if (isFriendly && alwaysVisibleIfFriendly)
            {
                ApplyVisible(1f, force);
                return;
            }

            if (fog == null)
            {
                // Fail-open in editor so missing wiring does not brick the scene.
                ApplyVisible(1f, force);
                return;
            }

            var sample = fog.SampleVisibility(GridPosition);
            ApplyVisible(sample, force);
        }

        void ApplyVisible(float sample, bool force)
        {
            _vis01 = sample;
            var fullyVisible = sample >= visibleThreshold;
            var explored = sample >= exploredThreshold;

            bool showMeshes;
            float sat;

            switch (mode)
            {
                case FogAgentMode.Hide:
                    showMeshes = fullyVisible;
                    sat = fullyVisible ? 1f : 0f;
                    break;
                case FogAgentMode.Desaturate:
                    showMeshes = true;
                    sat = fullyVisible ? 1f : Mathf.InverseLerp(0f, visibleThreshold, sample);
                    break;
                default:
                    showMeshes = explored;
                    sat = fullyVisible ? 1f : 0.25f;
                    break;
            }

            if (!force && showMeshes == _visible && mode == FogAgentMode.Hide) return;
            _visible = showMeshes;

            SetRenderersEnabled(showMeshes);
            SetCanvasesEnabled(showMeshes && fullyVisible);
            SetExtrasEnabled(showMeshes);

            if (mode != FogAgentMode.Hide)
                ApplyDesaturation(sat);
        }

        void SetRenderersEnabled(bool enabled)
        {
            if (meshRenderers == null) return;
            for (var i = 0; i < meshRenderers.Length; i++)
            {
                if (meshRenderers[i] != null) meshRenderers[i].enabled = enabled;
            }
        }

        void SetCanvasesEnabled(bool enabled)
        {
            if (canvases == null) return;
            for (var i = 0; i < canvases.Length; i++)
            {
                if (canvases[i] != null) canvases[i].enabled = enabled;
            }
        }

        void SetExtrasEnabled(bool enabled)
        {
            if (extraBehaviours == null) return;
            for (var i = 0; i < extraBehaviours.Length; i++)
            {
                if (extraBehaviours[i] != null) extraBehaviours[i].enabled = enabled;
            }
        }

        void CacheBaseColors()
        {
            if (meshRenderers == null) return;
            _baseColors = new Color[meshRenderers.Length];
            for (var i = 0; i < meshRenderers.Length; i++)
            {
                var r = meshRenderers[i];
                if (r == null || r.sharedMaterial == null)
                {
                    _baseColors[i] = Color.white;
                    continue;
                }

                var mat = r.sharedMaterial;
                if (mat.HasProperty(BaseColorId)) _baseColors[i] = mat.GetColor(BaseColorId);
                else if (mat.HasProperty(ColorId)) _baseColors[i] = mat.GetColor(ColorId);
                else _baseColors[i] = Color.white;
            }
        }

        void ApplyDesaturation(float sat)
        {
            if (meshRenderers == null || _baseColors == null) return;
            _block ??= new MaterialPropertyBlock();
            sat = Mathf.Clamp01(sat);

            for (var i = 0; i < meshRenderers.Length; i++)
            {
                var r = meshRenderers[i];
                if (r == null) continue;
                var target = Color.Lerp(shroudTint, _baseColors[i], sat);
                r.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, target);
                _block.SetColor(ColorId, target);
                _block.SetColor(EmissionId, sat > 0.6f ? _baseColors[i] * 0.15f : Color.black);
                r.SetPropertyBlock(_block);
            }
        }

        GridCoord WorldToCoord()
        {
            if (fog != null && fog.TryWorldToCoord(transform.position, out var c))
                return c;
            return new GridCoord(
                Mathf.RoundToInt(transform.position.x),
                Mathf.RoundToInt(transform.position.z));
        }
    }
}
