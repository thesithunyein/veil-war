using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VeilWar.Core;
using VeilWar.Fog;
using VeilWar.Units;

namespace VeilWar.UI.Strategy
{
    /// <summary>
    /// Corner strategy minimap: mirrors FoW texture + friendly (green) / revealed enemy (flashing red) dots.
    /// </summary>
    public sealed class FogOfWarMinimap : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] FogOfWarManager fog;
        [SerializeField] RawImage fogRawImage;
        [SerializeField] RectTransform dotsRoot;
        [SerializeField] RectTransform minimapRoot;

        [Header("Units")]
        [SerializeField] List<UnitActor> trackedUnits = new();
        [SerializeField] bool autoFindUnits = true;
        [SerializeField] float refreshUnitsSeconds = 0.5f;

        [Header("Dots")]
        [SerializeField] Color friendlyColor = new(0.35f, 0.95f, 0.55f, 1f);
        [SerializeField] Color enemyColor = new(1f, 0.25f, 0.22f, 1f);
        [SerializeField] float dotSize = 10f;
        [SerializeField] float enemyFlashHz = 3.5f;

        [Header("Frame")]
        [SerializeField] bool buildChromeIfMissing = true;

        readonly List<Image> _dotPool = new();
        float _findTimer;

        void Awake()
        {
            if (fog == null) fog = FogOfWarManager.Instance;
            EnsureChrome();
        }

        void OnEnable()
        {
            if (fog == null) fog = FogOfWarManager.Instance;
            if (fog != null) fog.FogUpdated += OnFogUpdated;
            RefreshUnitCache();
            OnFogUpdated();
        }

        void OnDisable()
        {
            if (fog != null) fog.FogUpdated -= OnFogUpdated;
        }

        void Update()
        {
            if (autoFindUnits)
            {
                _findTimer -= Time.deltaTime;
                if (_findTimer <= 0f)
                {
                    _findTimer = refreshUnitsSeconds;
                    RefreshUnitCache();
                }
            }

            DrawDots();
        }

        public void RegisterUnit(UnitActor unit)
        {
            if (unit != null && !trackedUnits.Contains(unit))
                trackedUnits.Add(unit);
        }

        void OnFogUpdated()
        {
            if (fog == null || fogRawImage == null) return;
            var tex = fog.FogTexture;
            if (tex == null) return;
            tex.filterMode = FilterMode.Bilinear;
            fogRawImage.texture = tex;
            // Invert feel optional: dark shroud on UI — shader already dark; R channel bright = visible.
            fogRawImage.uvRect = new Rect(0f, 0f, 1f, 1f);
            fogRawImage.color = new Color(0.15f, 0.55f, 0.55f, 0.85f);
        }

        void DrawDots()
        {
            if (dotsRoot == null || fog == null) return;

            var needed = 0;
            for (var i = 0; i < trackedUnits.Count; i++)
            {
                var u = trackedUnits[i];
                if (u == null || !u.Alive) continue;

                var friendly = u.Owner == PlayerId.Local;
                if (!friendly && !fog.IsVisible(u.Coord)) continue;
                needed++;
            }

            EnsureDotPool(needed);

            var used = 0;
            var flash = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * Mathf.PI * 2f * enemyFlashHz));

            for (var i = 0; i < trackedUnits.Count; i++)
            {
                var u = trackedUnits[i];
                if (u == null || !u.Alive) continue;

                var friendly = u.Owner == PlayerId.Local;
                if (!friendly && !fog.IsVisible(u.Coord)) continue;

                var img = _dotPool[used++];
                img.enabled = true;
                img.color = friendly ? friendlyColor : new Color(enemyColor.r, enemyColor.g, enemyColor.b, flash);

                var rt = img.rectTransform;
                rt.anchoredPosition = GridToMinimap(u.Coord);
                rt.sizeDelta = Vector2.one * dotSize;
            }

            for (var i = used; i < _dotPool.Count; i++)
                _dotPool[i].enabled = false;
        }

        Vector2 GridToMinimap(GridCoord coord)
        {
            var size = fog.Size;
            var root = dotsRoot.rect;
            // UV 0..1 from cell centers
            var u = size <= 1 ? 0.5f : (coord.X + 0.5f) / size;
            var v = size <= 1 ? 0.5f : (coord.Y + 0.5f) / size;
            return new Vector2(
                Mathf.Lerp(root.xMin, root.xMax, u),
                Mathf.Lerp(root.yMin, root.yMax, v));
        }

        void EnsureDotPool(int count)
        {
            while (_dotPool.Count < count)
            {
                var go = new GameObject("Dot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(dotsRoot, false);
                var img = go.GetComponent<Image>();
                img.raycastTarget = false;
                var rt = img.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                _dotPool.Add(img);
            }
        }

        void RefreshUnitCache()
        {
            if (!autoFindUnits) return;
            trackedUnits.Clear();
            trackedUnits.AddRange(FindObjectsOfType<UnitActor>());
        }

        void EnsureChrome()
        {
            if (minimapRoot == null && buildChromeIfMissing)
            {
                var canvas = GetComponentInParent<Canvas>();
                if (canvas == null)
                {
                    var canvasGo = new GameObject("StrategyCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                    canvas = canvasGo.GetComponent<Canvas>();
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    var scaler = canvasGo.GetComponent<CanvasScaler>();
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.referenceResolution = new Vector2(1080, 1920);
                    canvasGo.transform.SetParent(transform, false);
                }

                var rootGo = new GameObject("MinimapRoot", typeof(RectTransform), typeof(Image));
                rootGo.transform.SetParent(canvas.transform, false);
                minimapRoot = rootGo.GetComponent<RectTransform>();
                minimapRoot.anchorMin = new Vector2(1f, 1f);
                minimapRoot.anchorMax = new Vector2(1f, 1f);
                minimapRoot.pivot = new Vector2(1f, 1f);
                minimapRoot.anchoredPosition = new Vector2(-24f, -24f);
                minimapRoot.sizeDelta = new Vector2(180f, 180f);
                var frame = rootGo.GetComponent<Image>();
                frame.color = new Color(0.05f, 0.08f, 0.1f, 0.85f);
                frame.raycastTarget = false;
            }

            if (fogRawImage == null && minimapRoot != null && buildChromeIfMissing)
            {
                var fogGo = new GameObject("FogRaw", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                fogGo.transform.SetParent(minimapRoot, false);
                var rt = fogGo.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = new Vector2(8f, 8f);
                rt.offsetMax = new Vector2(-8f, -8f);
                fogRawImage = fogGo.GetComponent<RawImage>();
                fogRawImage.raycastTarget = false;
            }

            if (dotsRoot == null && minimapRoot != null && buildChromeIfMissing)
            {
                var dotsGo = new GameObject("Dots", typeof(RectTransform));
                dotsGo.transform.SetParent(minimapRoot, false);
                dotsRoot = dotsGo.GetComponent<RectTransform>();
                dotsRoot.anchorMin = Vector2.zero;
                dotsRoot.anchorMax = Vector2.one;
                dotsRoot.offsetMin = new Vector2(8f, 8f);
                dotsRoot.offsetMax = new Vector2(-8f, -8f);
            }
        }
    }
}
