using UnityEngine;
using VeilWar.Core;

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
        float _sat01 = 1f;
        GridCoord _lastCoord;
        bool _subscribed;
        bool _bootstrapped;

        public bool IsCurrentlyVisible => _visible;
        public float Visibility01 => _vis01;
        public GridCoord GridPosition => followTransform ? WorldToCoord() : manualCoord;

        public void Configure(bool friendly, FogAgentMode agentMode = FogAgentMode.HideOrDesaturate)
        {
            isFriendly = friendly;
            mode = agentMode;
            alwaysVisibleIfFriendly = true;
            _bootstrapped = true;
            EnsureFogRef();
            Refresh(force: true);
        }

        void Awake()
        {
            EnsureFogRef();
            if (meshRenderers == null || meshRenderers.Length == 0)
                meshRenderers = GetComponentsInChildren<MeshRenderer>(true);
            if (canvases == null || canvases.Length == 0)
                canvases = GetComponentsInChildren<Canvas>(true);

            CacheBaseColors();
            _block ??= new MaterialPropertyBlock();
        }

        void OnEnable()
        {
            EnsureFogRef();
            Subscribe();
            // Defer first paint to Start/Configure so UnitActor.Configure wins over default isFriendly=false.
            if (_bootstrapped) Refresh(force: true);
        }

        void Start()
        {
            _bootstrapped = true;
            EnsureFogRef();
            Subscribe();
            Refresh(force: true);
        }

        void OnDisable()
        {
            Unsubscribe();
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
            var y = transform.position.y;
            transform.position = fog != null
                ? fog.CoordToWorld(coord) + Vector3.up * y
                : new Vector3(coord.X, y, coord.Y);
            _lastCoord = coord;
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

        void Subscribe()
        {
            if (_subscribed || fog == null) return;
            fog.FogUpdated += OnFogUpdated;
            _subscribed = true;
        }

        void Unsubscribe()
        {
            if (!_subscribed || fog == null) return;
            fog.FogUpdated -= OnFogUpdated;
            _subscribed = false;
        }

        void EnsureFogRef()
        {
            if (fog != null) return;
            fog = FogOfWarManager.Instance;
            if (_subscribed || !isActiveAndEnabled || fog == null) return;
            Subscribe();
        }

        void OnFogUpdated() => Refresh(force: false);

        void Refresh(bool force)
        {
            if (isFriendly && alwaysVisibleIfFriendly)
            {
                ApplyVisible(1f, force);
                return;
            }

            if (fog == null || !fog.IsReady)
            {
                // Fail-open until FoW is ready so spawn flashes don't brick the scene.
                ApplyVisible(1f, force);
                return;
            }

            ApplyVisible(fog.SampleVisibility(GridPosition), force);
        }

        void ApplyVisible(float sample, bool force)
        {
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

            if (!force
                && showMeshes == _visible
                && Mathf.Abs(_vis01 - sample) < 0.001f
                && Mathf.Abs(_sat01 - sat) < 0.001f)
            {
                return;
            }

            _vis01 = sample;
            _sat01 = sat;
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
                // Optional smoothing when colorLerpSpeed > 0 (inspector-tunable).
                var t = colorLerpSpeed <= 0f ? 1f : 1f - Mathf.Exp(-colorLerpSpeed * Time.deltaTime);
                var target = Color.Lerp(shroudTint, _baseColors[i], sat);
                r.GetPropertyBlock(_block);
                var current = _block.GetColor(BaseColorId);
                if (current.a <= 0f && current.r <= 0f && current.g <= 0f && current.b <= 0f)
                    current = target;
                target = Color.Lerp(current, target, t);
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
