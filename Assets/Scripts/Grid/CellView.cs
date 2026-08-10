using System;
using UnityEngine;
using VeilWar.Core;

namespace VeilWar.Grid
{
    /// <summary>
    /// Single board cell presentation + hit target for selection.
    /// Fog is a readable overlay (shader/material lerp), not a Dark Forest lighting sim.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class CellView : MonoBehaviour
    {
        [SerializeField] Renderer fogRenderer;
        [SerializeField] Renderer tileRenderer;
        [SerializeField] Transform highlight;
        [SerializeField] float revealFlashSeconds = 0.28f;

        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        GameConfig _config;
        MaterialPropertyBlock _block;
        float _flashUntil;

        public GridCoord Coord { get; private set; }
        public CellVisibility Visibility { get; private set; } = CellVisibility.Unknown;
        public event Action<CellView> Clicked;

        public void Initialize(GridCoord coord, Vector3 worldPos, GameConfig config)
        {
            Coord = coord;
            _config = config;
            transform.position = worldPos;
            transform.localScale = Vector3.one * config.CellWorldSize;
            _block ??= new MaterialPropertyBlock();
            if (highlight != null) highlight.gameObject.SetActive(false);
            SetVisibility(CellVisibility.Unknown);
        }

        public void SetVisibility(CellVisibility visibility)
        {
            var changed = Visibility != visibility && Visibility == CellVisibility.Unknown &&
                          visibility != CellVisibility.Unknown;
            Visibility = visibility;
            ApplyMaterials();
            if (changed) PlayRevealFlash();
        }

        public void SetSelected(bool selected)
        {
            if (highlight != null) highlight.gameObject.SetActive(selected);
        }

        public void PlayHitShake(float amplitude = 0.06f, float duration = 0.18f)
        {
            StopAllCoroutines();
            StartCoroutine(ShakeRoutine(amplitude, duration));
        }

        void PlayRevealFlash()
        {
            _flashUntil = Time.time + revealFlashSeconds;
        }

        void LateUpdate()
        {
            if (Time.time < _flashUntil) ApplyMaterials(flash: true);
        }

        void ApplyMaterials(bool flash = false)
        {
            if (_config == null) return;
            _block ??= new MaterialPropertyBlock();

            var mist = Visibility == CellVisibility.Unknown;
            var color = mist ? _config.MistTeal : Color.white;
            if (flash) color = Color.Lerp(color, _config.AccentSignal, 0.75f);

            if (fogRenderer != null)
            {
                fogRenderer.enabled = mist;
                fogRenderer.GetPropertyBlock(_block);
                _block.SetColor(BaseColor, mist ? _config.MistTeal : Color.clear);
                fogRenderer.SetPropertyBlock(_block);
            }

            if (tileRenderer != null)
            {
                tileRenderer.GetPropertyBlock(_block);
                _block.SetColor(BaseColor, mist ? _config.MistDeep : new Color(0.12f, 0.16f, 0.18f, 1f));
                if (Visibility is CellVisibility.RevealedFriendly or CellVisibility.Hq)
                    _block.SetColor(EmissionColor, _config.AccentSignal * 0.35f);
                else if (Visibility == CellVisibility.RevealedEnemy)
                    _block.SetColor(EmissionColor, new Color(1f, 0.35f, 0.25f) * 0.35f);
                else
                    _block.SetColor(EmissionColor, Color.black);
                tileRenderer.SetPropertyBlock(_block);
            }
        }

        System.Collections.IEnumerator ShakeRoutine(float amplitude, float duration)
        {
            var origin = transform.position;
            var t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                var w = 1f - (t / duration);
                transform.position = origin + UnityEngine.Random.insideUnitSphere * (amplitude * w);
                yield return null;
            }
            transform.position = origin;
        }

        void OnMouseUpAsButton()
        {
            Clicked?.Invoke(this);
        }
    }
}
