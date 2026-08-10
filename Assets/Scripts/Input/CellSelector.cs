using UnityEngine;
using VeilWar.Grid;
using VeilWar.Match;

namespace VeilWar.Input
{
    /// <summary>
    /// Mobile/desktop cell picking for attack / deploy.
    /// </summary>
    public sealed class CellSelector : MonoBehaviour
    {
        [SerializeField] Camera cam;
        [SerializeField] MatchController match;
        [SerializeField] LayerMask cellMask = ~0;

        CellView _selected;

        void Awake()
        {
            if (cam == null) cam = Camera.main;
        }

        void Update()
        {
            if (match == null || match.Phase != Core.MatchPhase.Playing) return;
            if (!WasPrimaryPressed(out var screenPos)) return;
            if (!TryPickCell(screenPos, out var cell)) return;

            if (_selected != null) _selected.SetSelected(false);
            _selected = cell;
            _selected.SetSelected(true);
            match.TryAttack(cell.Coord);
        }

        bool TryPickCell(Vector3 screenPos, out CellView cell)
        {
            cell = null;
            var ray = cam.ScreenPointToRay(screenPos);
            if (!Physics.Raycast(ray, out var hit, 200f, cellMask)) return false;
            cell = hit.collider.GetComponentInParent<CellView>();
            return cell != null;
        }

        static bool WasPrimaryPressed(out Vector3 screenPos)
        {
            screenPos = default;
#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL
            if (UnityEngine.Input.GetMouseButtonDown(0))
            {
                screenPos = UnityEngine.Input.mousePosition;
                return true;
            }
#endif
            if (UnityEngine.Input.touchCount > 0)
            {
                var t = UnityEngine.Input.GetTouch(0);
                if (t.phase == TouchPhase.Began)
                {
                    screenPos = t.position;
                    return true;
                }
            }
            return false;
        }
    }
}
